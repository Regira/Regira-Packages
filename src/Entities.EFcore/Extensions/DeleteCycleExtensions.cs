using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Transactions;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Saves a delete EF Core cannot order on its own: two rows deleted together that reference each other, the
/// shape an entity produces when it carries a foreign key to one of its own children.
/// <para>
/// EF builds the delete order from the <b>original</b> foreign-key values and refuses the whole save with
/// <c>Unable to save changes because a circular dependency was detected in the data to be saved</c>. Nothing
/// inside the single <c>SaveChanges</c> can resolve it: a primer or prepper nulling the current value is
/// ignored (the graph reads original values), and nulling the original value only moves the failure to the
/// database, which still holds the reference while the row it points at is deleted. Dropping the reference
/// needs an <c>UPDATE</c> before the <c>DELETE</c>s, so it has to happen around <c>SaveChanges</c> rather
/// than inside it.
/// </para>
/// </summary>
/// <example>
/// Wire it into the <c>DbContext</c>, overriding <b>both</b> save methods — overriding only the async one
/// leaves every synchronous caller (seeding, jobs, <c>EnsureCreated</c> tooling) broken:
/// <code>
/// public override int SaveChanges(bool acceptAllChangesOnSuccess)
///     => this.SaveChangesBreakingDeleteCycles(base.SaveChanges, acceptAllChangesOnSuccess);
///
/// public override Task&lt;int&gt; SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken token = default)
///     => this.SaveChangesBreakingDeleteCyclesAsync(base.SaveChangesAsync, acceptAllChangesOnSuccess, token);
/// </code>
/// </example>
public static class DeleteCycleExtensions
{
    /// <summary>
    /// Runs <paramref name="save"/> once, first dropping with a direct <c>UPDATE</c> any reference that would
    /// make EF Core reject the delete as a circular dependency. A save with no such pair opens no transaction
    /// and starts no execution strategy. The change tracker is read only when the model has a pair of entity
    /// types referencing each other and the provider is relational — on any other context, and that is most
    /// of them, the call costs a cached lookup and nothing else.
    /// </summary>
    /// <param name="dbContext">The context whose change tracker holds the pending delete.</param>
    /// <param name="save">The real save — <c>base.SaveChanges</c> from an override. Called exactly once; the
    /// reference-dropping <c>UPDATE</c> runs as a direct statement before it, so the value returned is the
    /// save's own count and every row is counted once.</param>
    /// <param name="acceptAllChangesOnSuccess">The caller's flag, passed to the save unchanged. Nothing is
    /// accepted before that save returns, so a save the database rejects leaves the change tracker holding
    /// every pending change for the retry EF's strategy or the caller makes.</param>
    public static int SaveChangesBreakingDeleteCycles(this DbContext dbContext, Func<bool, int> save,
        bool acceptAllChangesOnSuccess = true)
    {
        var breaks = FindBreakableDeleteCycles(dbContext);
        if (breaks.Count == 0)
        {
            return save(acceptAllChangesOnSuccess);
        }

        if (CallerOwnsTheTransaction(dbContext))
        {
            return DropReferencesAndSave(dbContext, breaks, save, acceptAllChangesOnSuccess);
        }

        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();
            var affected = DropReferencesAndSave(dbContext, breaks, save, acceptAllChangesOnSuccess);
            transaction.Commit();
            return affected;
        });
    }

    /// <inheritdoc cref="SaveChangesBreakingDeleteCycles"/>
    public static async Task<int> SaveChangesBreakingDeleteCyclesAsync(this DbContext dbContext,
        Func<bool, CancellationToken, Task<int>> save, bool acceptAllChangesOnSuccess = true,
        CancellationToken token = default)
    {
        var breaks = FindBreakableDeleteCycles(dbContext);
        if (breaks.Count == 0)
        {
            return await save(acceptAllChangesOnSuccess, token);
        }

        if (CallerOwnsTheTransaction(dbContext))
        {
            return await DropReferencesAndSaveAsync(dbContext, breaks, save, acceptAllChangesOnSuccess, token);
        }

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var affected = await DropReferencesAndSaveAsync(dbContext, breaks, save, acceptAllChangesOnSuccess, ct);
            await transaction.CommitAsync(ct);
            return affected;
        }, token);
    }


    /// <summary>
    /// True when the caller already owns the transaction — one it began itself, or an ambient
    /// <see cref="TransactionScope"/>. Either already spans both saves, so opening another is wrong: under
    /// <c>EnableRetryOnFailure()</c> the caller's arrangement is EF's documented recipe,
    /// <c>strategy.Execute(() =&gt; { BeginTransaction(); SaveChanges(); Commit(); })</c>, and a second
    /// transaction inside it is a second unit of work the retry cannot replay.
    /// <para>
    /// The ambient half is the one that cannot be inferred: <c>Database.CurrentTransaction</c> is null inside a
    /// <see cref="TransactionScope"/>, so without <see cref="Transaction.Current"/> this would begin a
    /// transaction on a connection already enlisted in one, which EF refuses.
    /// </para>
    /// <para>
    /// The execution strategy is not what the check protects. Inside the caller's own strategy a nested one is
    /// suspended: it neither retries nor throws. Under a bare <c>BeginTransaction()</c> with no strategy around
    /// it, a retrying strategy refuses to start — and so does EF's own <c>SaveChanges</c>, so that arrangement
    /// fails identically with or without this method. Skipping the strategy is a tidiness; skipping the
    /// transaction is not.
    /// </para>
    /// </summary>
    private static bool CallerOwnsTheTransaction(DbContext dbContext)
        => dbContext.Database.CurrentTransaction != null || Transaction.Current != null;

    /// <summary>
    /// Drops the references with a direct <c>UPDATE</c>, tells the change tracker the database no longer holds
    /// them, and runs the caller's save once. Nothing is accepted before that save returns: if the database
    /// rejects it, the rollback and the tracker agree and every change is still pending. The tracker's view of
    /// the references is put back on failure for the same reason — a replay, by EF's strategy or by the caller,
    /// must find the cycle again and drop it again on a database where the rollback restored the reference.
    /// </summary>
    private static int DropReferencesAndSave(DbContext dbContext, IReadOnlyList<CycleBreak> breaks,
        Func<bool, int> save, bool acceptAllChangesOnSuccess)
    {
        var undo = new List<Action>();
        try
        {
            foreach (var (entry, properties) in GroupByEntry(breaks))
            {
                var (sql, parameters) = DropReferenceStatement(dbContext, entry, properties);
                ThrowIfNoRowMatched(entry, dbContext.Database.ExecuteSqlRaw(sql, parameters));
                var databaseValues = HasStoreGeneratedToken(entry) ? entry.GetDatabaseValues() : null;
                ForgetReference(entry, properties, databaseValues, undo);
            }
            return save(acceptAllChangesOnSuccess);
        }
        catch
        {
            undo.ForEach(action => action());
            throw;
        }
    }

    /// <inheritdoc cref="DropReferencesAndSave"/>
    private static async Task<int> DropReferencesAndSaveAsync(DbContext dbContext, IReadOnlyList<CycleBreak> breaks,
        Func<bool, CancellationToken, Task<int>> save, bool acceptAllChangesOnSuccess, CancellationToken token)
    {
        var undo = new List<Action>();
        try
        {
            foreach (var (entry, properties) in GroupByEntry(breaks))
            {
                var (sql, parameters) = DropReferenceStatement(dbContext, entry, properties);
                ThrowIfNoRowMatched(entry, await dbContext.Database.ExecuteSqlRawAsync(sql, parameters, token));
                var databaseValues = HasStoreGeneratedToken(entry) ? await entry.GetDatabaseValuesAsync(token) : null;
                ForgetReference(entry, properties, databaseValues, undo);
            }
            return await save(acceptAllChangesOnSuccess, token);
        }
        catch
        {
            undo.ForEach(action => action());
            throw;
        }
    }

    /// <summary>One <c>UPDATE</c> per row: an owner pointing at the same child through two references
    /// (a cover and a thumbnail) drops both in a single statement.</summary>
    private static IEnumerable<(EntityEntry Entry, IReadOnlyList<IProperty> Properties)> GroupByEntry(IReadOnlyList<CycleBreak> breaks)
        => breaks.GroupBy(b => b.Entry).Select(g => (g.Key, (IReadOnlyList<IProperty>)g.SelectMany(b => b.Properties).Distinct().ToList()));

    /// <summary>
    /// A token the store moves on every <c>UPDATE</c> — a rowversion — which the reference drop therefore
    /// changed. An application-owned token (<c>[ConcurrencyCheck]</c>) is deliberately not included: the
    /// <c>UPDATE</c> never touched it, and re-reading it would adopt another writer's value and defeat the
    /// very check the column exists for.
    /// </summary>
    private static bool IsStoreGeneratedToken(IProperty property)
        => property.IsConcurrencyToken && property.IsStoreGeneratedOnUpdate();

    private static bool HasStoreGeneratedToken(EntityEntry entry)
        => entry.Metadata.GetProperties().Any(IsStoreGeneratedToken);

    /// <summary>
    /// The change tracker's side of the <c>UPDATE</c>: the reference's original value becomes null, so EF no
    /// longer orders this delete after the row it pointed at — the original values are what the delete order
    /// is read from. A token the store moved on that <c>UPDATE</c> (a rowversion) is refreshed from the row so
    /// the <c>DELETE</c>'s <c>WHERE</c> carries the value the database now holds. Every original value touched
    /// is recorded in <paramref name="undo"/>, to be put back if the save fails.
    /// </summary>
    private static void ForgetReference(EntityEntry entry, IReadOnlyList<IProperty> properties, PropertyValues? databaseValues, List<Action> undo)
    {
        foreach (var property in properties)
        {
            Remember(entry.Property(property.Name), undo).OriginalValue = null;
        }

        if (databaseValues is null)
        {
            return;
        }
        foreach (var token in entry.Metadata.GetProperties().Where(IsStoreGeneratedToken))
        {
            Remember(entry.Property(token.Name), undo).OriginalValue = databaseValues[token.Name];
        }
    }

    private static PropertyEntry Remember(PropertyEntry property, List<Action> undo)
    {
        var previous = property.OriginalValue;
        undo.Add(() => property.OriginalValue = previous);
        return property;
    }

    /// <summary>
    /// The <c>UPDATE</c> matched no row: another writer changed or removed it since this unit of work loaded
    /// it. Surfaced the way EF's own update would surface it, before anything of the other writer's is
    /// overwritten or adopted, and with the entry in <see cref="DbUpdateException.Entries"/> so the recovery
    /// EF documents — reload or reconcile each entry, then save again — works on this exception too.
    /// </summary>
    private static void ThrowIfNoRowMatched(EntityEntry entry, int affected)
    {
        if (affected == 0)
        {
            // The entries collection takes EF's internal entry type, reached through the infrastructure
            // accessor. That type has implemented IUpdateEntry since EF Core 2; if a major ever changes that,
            // this cast is the one line to revisit, and the message stays the whole signal in the meantime.
#pragma warning disable EF1001
            throw new DbUpdateConcurrencyException(
                $"The {entry.Metadata.DisplayName()} row being deleted was modified or deleted since it was loaded, "
                + "so the reference to its child could not be dropped. Reload the entity and retry.",
                [(IUpdateEntry)entry.GetInfrastructure()]);
#pragma warning restore EF1001
        }
    }

    /// <summary>
    /// <c>UPDATE table SET fk = NULL WHERE key = @p0 AND token = @p1</c> for one entry, against the table the
    /// foreign key is mapped to, with values converted the way the provider stores them. The <c>WHERE</c>
    /// carries the row's concurrency tokens from their original values, as EF's own <c>UPDATE</c> would, so
    /// another writer's change is detected here rather than overwritten — which is also what makes refreshing
    /// a store-generated token afterwards sound: the row was proven untouched before its new value is adopted.
    /// A column the statement needs that is not mapped to that table (a token declared on a derived type in a
    /// TPT hierarchy) is refused rather than guessed at.
    /// </summary>
    private static (string Sql, object[] Parameters) DropReferenceStatement(DbContext dbContext, EntityEntry entry, IReadOnlyList<IProperty> properties)
    {
        var entityType = properties[0].DeclaringType as IEntityType ?? entry.Metadata;
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{entityType.DisplayName()} is not mapped to a table, so a delete cycle through it cannot be broken.");
        var schema = entityType.GetSchema();
        var table = StoreObjectIdentifier.Table(tableName, schema);
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.DisplayName()} has no primary key, so a delete cycle through it cannot be broken.");

        var helper = dbContext.GetService<ISqlGenerationHelper>();
        string Column(IProperty property) => helper.DelimitIdentifier(property.GetColumnName(table)
            ?? throw new InvalidOperationException(
                $"{property.DeclaringType.DisplayName()}.{property.Name} is not mapped to table {tableName}, so a delete cycle through it cannot be broken."));

        var set = string.Join(", ", properties.Select(p => $"{Column(p)} = NULL"));

        var guards = key.Properties.Concat(entry.Metadata.GetProperties().Where(p => p.IsConcurrencyToken && !p.IsKey()));
        var parameters = new List<object>();
        var where = string.Join(" AND ", guards.Select(p =>
        {
            var value = ToProviderValue(p, entry.Property(p.Name).OriginalValue);
            if (value is null)
            {
                return $"{Column(p)} IS NULL";
            }
            parameters.Add(value);
            return $"{Column(p)} = {{{parameters.Count - 1}}}";
        }));

        return ($"UPDATE {helper.DelimitIdentifier(tableName, schema)} SET {set} WHERE {where}", parameters.ToArray());
    }

    private static object? ToProviderValue(IProperty property, object? value)
        => property.FindTypeMapping()?.Converter is { } converter ? converter.ConvertToProvider(value) : value;


    /// <summary>
    /// The foreign keys to null before the delete: for each pair of entries that are both being deleted and
    /// both reference the other, every reference in the one direction that can be dropped. Only an
    /// <b>optional</b> foreign key can be, which is also the one to drop — the required side is the child's
    /// link to its owner, and that row is going away anyway.
    /// <para>
    /// Deliberately limited to direct pairs. A longer ring (<c>A → B → C → A</c>) is left to EF's own
    /// exception rather than resolved by a guess at which link is the incidental one.
    /// </para>
    /// </summary>
    private static IReadOnlyList<CycleBreak> FindBreakableDeleteCycles(DbContext dbContext)
    {
        // Settled before the change tracker is read, which runs DetectChanges over every tracked entity: a
        // pair can only form between entity types the model links both ways, and only a relational store
        // holds the reference the UPDATE drops — the in-memory provider enforces no foreign keys and orders
        // no deletes, so EF saves the pair there on its own.
        if (!dbContext.Database.IsRelational())
        {
            return [];
        }
        var cyclable = CyclableTypes.GetValue(dbContext.Model, FindCyclableTypes);
        if (cyclable.Count == 0)
        {
            return [];
        }

        // Under the default Immediate timing the cascade already ran inside Remove(); under OnSaveChanges the
        // dependents are still Unchanged at this point and there would be no cycle to find yet.
        if (dbContext.ChangeTracker.CascadeDeleteTiming == CascadeTiming.OnSaveChanges)
        {
            dbContext.ChangeTracker.CascadeChanges();
        }

        var deleted = dbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted && cyclable.Contains(e.Metadata.Name))
            .ToArray();
        if (deleted.Length < 2)
        {
            return [];
        }

        var index = new DeletedIndex(deleted);
        var edges = deleted.SelectMany(entry => OutgoingEdges(entry, index)).ToArray();
        var byDirection = edges
            .GroupBy(e => new Pair(e.Dependent.Entity, e.Principal.Entity))
            .ToDictionary(g => g.Key, g => g.ToArray());
        var handled = new HashSet<Pair>();
        var breaks = new List<CycleBreak>();
        foreach (var edge in edges)
        {
            var forward = new Pair(edge.Dependent.Entity, edge.Principal.Entity);
            if (handled.Contains(forward) || !byDirection.TryGetValue(forward.Reversed, out var back))
            {
                continue;
            }
            handled.Add(forward);
            handled.Add(forward.Reversed);

            // Every reference in one direction goes, or the pair stays linked: an owner pointing at the same
            // child twice (a cover and a thumbnail) is two cycles, and dropping one reference leaves the other.
            // The direction whose references are all optional is the one that can go; where both qualify the
            // first encountered is taken, which keeps the choice deterministic (change-tracker order, then the
            // entity type's foreign keys). A pair required both ways is left to EF's own exception.
            var ahead = byDirection[forward];
            var side = ahead.All(e => IsOptional(e.ForeignKey)) ? ahead
                : back.All(e => IsOptional(e.ForeignKey)) ? back
                : null;
            if (side != null)
            {
                breaks.AddRange(side.Select(e => new CycleBreak(e.Dependent, e.ForeignKey.Properties)));
            }
        }

        return breaks;
    }

    /// <summary>
    /// The other pending deletes <paramref name="dependent"/> points at, matched on the foreign key values the
    /// row still holds in the database — the current values are what a primer would have changed, and what EF
    /// ignores when it orders the deletes.
    /// </summary>
    private static IEnumerable<Edge> OutgoingEdges(EntityEntry dependent, DeletedIndex index)
    {
        foreach (var foreignKey in dependent.Metadata.GetForeignKeys())
        {
            var values = foreignKey.Properties.Select(p => dependent.Property(p.Name).OriginalValue).ToArray();
            if (values.Any(v => v == null))
            {
                continue;
            }

            var principal = index.Find(foreignKey.PrincipalKey, values);
            if (principal != null
                && !ReferenceEquals(principal.Entity, dependent.Entity)
                && foreignKey.PrincipalEntityType.ClrType.IsInstanceOfType(principal.Entity))
            {
                yield return new Edge(dependent, principal, foreignKey);
            }
        }
    }


    private static readonly ConditionalWeakTable<IModel, HashSet<string>> CyclableTypes = new();

    /// <summary>
    /// The entity types (by name) a breakable pair can form between: two non-owned types each carrying a
    /// foreign key to the other — or one type carrying one to itself — where at least one of the two keys is
    /// optional, together with every type deriving from them. Computed once per model; a model without such
    /// a pair, which is most models, makes every save skip the change tracker entirely.
    /// </summary>
    private static HashSet<string> FindCyclableTypes(IModel model)
    {
        var cyclable = new HashSet<string>();
        foreach (var entityType in model.GetEntityTypes().Where(t => !t.IsOwned()))
        {
            foreach (var reference in entityType.GetForeignKeys())
            {
                var principal = reference.PrincipalEntityType;
                if (principal.IsOwned())
                {
                    continue;
                }
                var mutual = principal.GetForeignKeys().Any(back =>
                    Related(back.PrincipalEntityType, entityType) && (IsOptional(reference) || IsOptional(back)));
                if (!mutual)
                {
                    continue;
                }
                foreach (var type in entityType.GetDerivedTypesInclusive().Concat(principal.GetDerivedTypesInclusive()))
                {
                    cyclable.Add(type.Name);
                }
            }
        }
        return cyclable;
    }

    private static bool IsOptional(IForeignKey foreignKey)
        => foreignKey.Properties.All(p => p.IsNullable);

    /// <summary>Same type, or one derived from the other — an entry of a derived type is an instance of the
    /// principal type a foreign key names, which is how the edges are matched at save time.</summary>
    private static bool Related(IEntityType a, IEntityType b)
        => a.ClrType.IsAssignableFrom(b.ClrType) || b.ClrType.IsAssignableFrom(a.ClrType);


    /// <summary>The pending deletes by key, so that each foreign key finds its principal in one lookup.</summary>
    private sealed class DeletedIndex
    {
        private readonly Dictionary<IKey, Dictionary<KeyValues, EntityEntry>> _byKey = [];

        public DeletedIndex(EntityEntry[] deleted)
        {
            foreach (var entry in deleted)
            {
                foreach (var key in entry.Metadata.GetKeys())
                {
                    if (!_byKey.TryGetValue(key, out var entries))
                    {
                        _byKey[key] = entries = [];
                    }
                    entries.TryAdd(new KeyValues(key.Properties.Select(p => entry.Property(p.Name).OriginalValue).ToArray()), entry);
                }
            }
        }

        public EntityEntry? Find(IKey key, object?[] values)
            => _byKey.TryGetValue(key, out var entries) && entries.TryGetValue(new KeyValues(values), out var entry) ? entry : null;
    }

    /// <summary>Key values compared element-wise, structurally (a <c>byte[]</c> key compares by content).</summary>
    private readonly struct KeyValues(object?[] values) : IEquatable<KeyValues>
    {
        private static readonly IEqualityComparer Comparer = StructuralComparisons.StructuralEqualityComparer;
        private readonly object?[] _values = values;

        public bool Equals(KeyValues other)
        {
            if (_values.Length != other._values.Length)
            {
                return false;
            }
            for (var i = 0; i < _values.Length; i++)
            {
                if (!Comparer.Equals(_values[i], other._values[i]))
                {
                    return false;
                }
            }
            return true;
        }

        public override bool Equals(object? obj) => obj is KeyValues other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (var value in _values)
            {
                hash.Add(value is null ? 0 : Comparer.GetHashCode(value));
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>Two tracked entities, compared by reference.</summary>
    private readonly record struct Pair(object Dependent, object Principal)
    {
        public Pair Reversed => new(Principal, Dependent);

        public bool Equals(Pair other)
            => ReferenceEquals(Dependent, other.Dependent) && ReferenceEquals(Principal, other.Principal);

        public override int GetHashCode()
            => HashCode.Combine(RuntimeHelpers.GetHashCode(Dependent), RuntimeHelpers.GetHashCode(Principal));
    }

    private sealed record Edge(EntityEntry Dependent, EntityEntry Principal, IForeignKey ForeignKey);
    private sealed record CycleBreak(EntityEntry Entry, IReadOnlyList<IProperty> Properties);
}
