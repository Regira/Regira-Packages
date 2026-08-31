using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
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
/// leaves every synchronous caller (seeding, jobs, <c>EnsureCreated</c> tooling) broken. The
/// <c>acceptAllChangesOnSuccess</c> flag goes to the extension, not into the delegate: the extension owns
/// which phase is allowed to accept.
/// <code>
/// public override int SaveChanges(bool acceptAllChangesOnSuccess)
///     => this.SaveChangesBreakingDeleteCycles(accept => base.SaveChanges(accept), acceptAllChangesOnSuccess);
///
/// public override Task&lt;int&gt; SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken token = default)
///     => this.SaveChangesBreakingDeleteCyclesAsync((accept, t) => base.SaveChangesAsync(accept, t),
///         acceptAllChangesOnSuccess, token);
/// </code>
/// </example>
public static class DeleteCycleExtensions
{
    /// <summary>
    /// Runs <paramref name="save"/>, first breaking any reference that would make EF Core reject the delete as
    /// a circular dependency. Saves that contain no such cycle call <paramref name="save"/> exactly once and
    /// open no transaction, so the common path is unchanged apart from one change-tracker scan.
    /// </summary>
    /// <param name="dbContext">The context whose change tracker holds the pending delete.</param>
    /// <param name="save">The real save — <c>accept =&gt; base.SaveChanges(accept)</c> from an override. It is
    /// invoked twice when a cycle was broken, and its return values are summed. Pass the flag straight
    /// through: what each phase may accept is the extension's to decide.</param>
    /// <param name="acceptAllChangesOnSuccess">The caller's own flag, honoured on the <b>final</b> save — with
    /// <see langword="false"/> the deletes stay pending until the caller calls <c>AcceptAllChanges()</c>. The
    /// reference-dropping <c>UPDATE</c> that precedes them is always accepted: its entries are what EF reads
    /// the delete order from, and leaving them pending both re-raises the circular dependency and re-sends
    /// every other change in the save.</param>
    public static int SaveChangesBreakingDeleteCycles(this DbContext dbContext, Func<bool, int> save,
        bool acceptAllChangesOnSuccess = true)
    {
        var breaks = FindBreakableDeleteCycles(dbContext);
        if (breaks.Count == 0)
        {
            return save(acceptAllChangesOnSuccess);
        }

        int TwoPhaseSave()
        {
            DropReferences(breaks);
            var affected = save(true);
            Redelete(breaks);
            return affected + save(acceptAllChangesOnSuccess);
        }

        // A transaction the caller owns — its own, or an ambient TransactionScope — already spans both saves,
        // and a retrying execution strategy refuses to run at all while one is current. So the strategy wraps
        // only the case this method opens the transaction for itself.
        if (HasCallerTransaction(dbContext))
        {
            return TwoPhaseSave();
        }

        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var owned = dbContext.Database.BeginTransaction();
            var affected = TwoPhaseSave();
            owned.Commit();
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

        async Task<int> TwoPhaseSaveAsync(CancellationToken ct)
        {
            DropReferences(breaks);
            var affected = await save(true, ct);
            Redelete(breaks);
            return affected + await save(acceptAllChangesOnSuccess, ct);
        }

        if (HasCallerTransaction(dbContext))
        {
            return await TwoPhaseSaveAsync(token);
        }

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var owned = await dbContext.Database.BeginTransactionAsync(ct);
            var affected = await TwoPhaseSaveAsync(ct);
            await owned.CommitAsync(ct);
            return affected;
        }, token);
    }

    /// <summary>
    /// Whether a transaction outside this method already spans the save: one the caller began on the context,
    /// or the ambient <see cref="TransactionScope"/> the connection enlists in. Both make the two phases
    /// atomic already, and both make an execution strategy that retries throw rather than run.
    /// </summary>
    private static bool HasCallerTransaction(DbContext dbContext)
        => dbContext.Database.CurrentTransaction != null || Transaction.Current != null;


    /// <summary>
    /// Phase one: the entry stops being deleted and becomes an <c>UPDATE</c> that nulls the offending foreign
    /// key. <see cref="EntityState.Unchanged"/> clears every modification flag, so the property is written
    /// after the state, not before.
    /// </summary>
    private static void DropReferences(IReadOnlyList<CycleBreak> breaks)
    {
        foreach (var group in breaks.GroupBy(b => b.Entry))
        {
            group.Key.State = EntityState.Unchanged;
            foreach (var property in group.SelectMany(b => b.Properties))
            {
                var entryProperty = group.Key.Property(property.Name);
                entryProperty.CurrentValue = null;
                entryProperty.IsModified = true;
            }
        }
    }

    /// <summary>Phase two: the row is deleted now that nothing being deleted alongside it is still referenced.</summary>
    private static void Redelete(IReadOnlyList<CycleBreak> breaks)
    {
        foreach (var group in breaks.GroupBy(b => b.Entry))
        {
            group.Key.State = EntityState.Deleted;
        }
    }


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
