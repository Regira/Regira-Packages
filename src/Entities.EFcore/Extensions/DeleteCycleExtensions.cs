using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Update;
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
    /// and starts no execution strategy — the common path costs one change-tracker scan.
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
    /// transaction on a connection already enlisted in one, which EF refuses. It is not covered by the test
    /// suite — SQLite rejects ambient transactions outright, whatever the code under test does.
    /// </para>
    /// <para>
    /// The execution strategy is not what the check protects, in either arrangement a caller's transaction can
    /// arrive in. Inside the caller's own strategy a nested one is suspended: it neither retries nor throws.
    /// Under a bare <c>BeginTransaction()</c> with no strategy around it, a retrying strategy refuses to start —
    /// and so does EF's own <c>SaveChanges</c>, so that arrangement fails identically with or without this
    /// method. Both are pinned in the test suite. Skipping the strategy is a tidiness; skipping the transaction
    /// is not.
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

    private static IEnumerable<(EntityEntry Entry, IReadOnlyList<IProperty> Properties)> GroupByEntry(IReadOnlyList<CycleBreak> breaks)
        => breaks.GroupBy(b => b.Entry).Select(g => (g.Key, (IReadOnlyList<IProperty>)g.SelectMany(b => b.Properties).Distinct().ToList()));

    /// <summary>
    /// A token the store moves on every <c>UPDATE</c> — a rowversion — which the reference drop therefore
    /// changed. An application-owned token (<c>[ConcurrencyCheck]</c>) is deliberately not included: the
    /// <c>UPDATE</c> never touched it, and re-reading it would adopt another writer's value and defeat the
    /// very check the column exists for.
    /// </summary>
    private static bool IsStoreGeneratedToken(IProperty property)
        => property.IsConcurrencyToken && property.ValueGenerated.HasFlag(ValueGenerated.OnUpdate);

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
            throw new DbUpdateConcurrencyException(
                $"The {entry.Metadata.DisplayName()} row being deleted was modified or deleted since it was loaded, "
                + "so the reference to its child could not be dropped. Reload the entity and retry.",
                [(IUpdateEntry)entry.GetInfrastructure()]);
        }
    }

    /// <summary>
    /// <c>UPDATE table SET fk = NULL WHERE key = @p0 AND token = @p1</c> for one entry, against the table the
    /// foreign key is mapped to, with values converted the way the provider stores them. The <c>WHERE</c>
    /// carries the row's concurrency tokens from their original values, as EF's own <c>UPDATE</c> would, so
    /// another writer's change is detected here rather than overwritten — which is also what makes refreshing
    /// a store-generated token afterwards sound: the row was proven untouched before its new value is adopted.
    /// </summary>
    private static (string Sql, object?[] Parameters) DropReferenceStatement(DbContext dbContext, EntityEntry entry, IReadOnlyList<IProperty> properties)
    {
        var entityType = properties[0].DeclaringType as IEntityType ?? entry.Metadata;
        var tableName = entityType.GetTableName()
            ?? throw new InvalidOperationException($"{entityType.DisplayName()} is not mapped to a table, so a delete cycle through it cannot be broken.");
        var schema = entityType.GetSchema();
        var table = StoreObjectIdentifier.Table(tableName, schema);
        var key = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.DisplayName()} has no primary key, so a delete cycle through it cannot be broken.");

        var helper = dbContext.GetService<ISqlGenerationHelper>();
        string Column(IProperty property) => helper.DelimitIdentifier(property.GetColumnName(table) ?? property.Name);

        var set = string.Join(", ", properties.Select(p => $"{Column(p)} = NULL"));

        var guards = key.Properties.Concat(entry.Metadata.GetProperties().Where(p => p.IsConcurrencyToken && !p.IsKey()));
        var parameters = new List<object?>();
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
    /// The foreign keys to null before the delete: one per pair of entries that are both being deleted and
    /// both reference the other. Only an <b>optional</b> foreign key can be dropped, which is also the one to
    /// drop — the required side is the child's link to its owner, and that row is going away anyway.
    /// <para>
    /// Deliberately limited to direct pairs. A longer ring (<c>A → B → C → A</c>) is left to EF's own
    /// exception rather than resolved by a guess at which link is the incidental one.
    /// </para>
    /// </summary>
    private static IReadOnlyList<CycleBreak> FindBreakableDeleteCycles(DbContext dbContext)
    {
        // Under the default Immediate timing the cascade already ran inside Remove(); under OnSaveChanges the
        // dependents are still Unchanged at this point and there would be no cycle to find yet.
        if (dbContext.ChangeTracker.CascadeDeleteTiming == CascadeTiming.OnSaveChanges)
        {
            dbContext.ChangeTracker.CascadeChanges();
        }

        var deleted = dbContext.ChangeTracker.Entries().Where(e => e.State == EntityState.Deleted).ToArray();
        if (deleted.Length < 2)
        {
            return [];
        }

        var edges = deleted.SelectMany(entry => OutgoingEdges(entry, deleted)).ToArray();
        var breaks = new List<CycleBreak>();
        var broken = new List<(object A, object B)>();
        // Optional first: where both directions are optional either would do, and taking the first keeps the
        // choice deterministic (change-tracker order, then the entity type's foreign keys).
        foreach (var edge in edges.Where(e => e.ForeignKey.Properties.All(p => p.IsNullable)))
        {
            var isCycle = edges.Any(other =>
                ReferenceEquals(other.Dependent.Entity, edge.Principal.Entity)
                && ReferenceEquals(other.Principal.Entity, edge.Dependent.Entity));
            var alreadyBroken = broken.Any(pair =>
                (ReferenceEquals(pair.A, edge.Dependent.Entity) && ReferenceEquals(pair.B, edge.Principal.Entity))
                || (ReferenceEquals(pair.A, edge.Principal.Entity) && ReferenceEquals(pair.B, edge.Dependent.Entity)));
            if (!isCycle || alreadyBroken)
            {
                continue;
            }
            broken.Add((edge.Dependent.Entity, edge.Principal.Entity));
            breaks.Add(new CycleBreak(edge.Dependent, edge.ForeignKey.Properties));
        }

        return breaks;
    }

    /// <summary>
    /// The other pending deletes <paramref name="dependent"/> points at, matched on the foreign key values the
    /// row still holds in the database — the current values are what a primer would have changed, and what EF
    /// ignores when it orders the deletes.
    /// </summary>
    private static IEnumerable<Edge> OutgoingEdges(EntityEntry dependent, EntityEntry[] deleted)
    {
        foreach (var foreignKey in dependent.Metadata.GetForeignKeys())
        {
            var values = foreignKey.Properties.Select(p => dependent.Property(p.Name).OriginalValue).ToArray();
            if (values.Any(v => v == null))
            {
                continue;
            }

            var principal = deleted.FirstOrDefault(candidate =>
                !ReferenceEquals(candidate.Entity, dependent.Entity)
                && foreignKey.PrincipalEntityType.ClrType.IsInstanceOfType(candidate.Entity)
                && foreignKey.PrincipalKey.Properties
                    .Select(p => candidate.Property(p.Name).OriginalValue)
                    .SequenceEqual(values));
            if (principal != null)
            {
                yield return new Edge(dependent, principal, foreignKey);
            }
        }
    }


    private sealed record Edge(EntityEntry Dependent, EntityEntry Principal, IForeignKey ForeignKey);
    private sealed record CycleBreak(EntityEntry Entry, IReadOnlyList<IProperty> Properties);
}
