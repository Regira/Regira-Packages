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
    /// Runs <paramref name="save"/>, first dropping any reference that would make EF Core reject the delete as
    /// a circular dependency. A save with no such pair runs <paramref name="save"/> exactly once, opens no
    /// transaction and starts no execution strategy — the common path costs one change-tracker scan.
    /// </summary>
    /// <param name="dbContext">The context whose change tracker holds the pending delete.</param>
    /// <param name="save">The real save — <c>base.SaveChanges</c> from an override. Called twice when a cycle
    /// was broken, and its return values are summed.</param>
    /// <param name="acceptAllChangesOnSuccess">The caller's flag, honoured on the save that carries the
    /// deletes. The reference-dropping <c>UPDATE</c> before it is always accepted: the delete order is read
    /// from the original values it settles, and everything else pending in the same save would be sent twice.</param>
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
            return TwoPhaseSave(breaks, save, acceptAllChangesOnSuccess);
        }

        return dbContext.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = dbContext.Database.BeginTransaction();
            var affected = TwoPhaseSave(breaks, save, acceptAllChangesOnSuccess);
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
            return await TwoPhaseSaveAsync(breaks, save, acceptAllChangesOnSuccess, token);
        }

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
            var affected = await TwoPhaseSaveAsync(breaks, save, acceptAllChangesOnSuccess, ct);
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
    /// Drop the reference, save, delete. Phase one is saved with <c>acceptAllChangesOnSuccess: true</c>
    /// whatever the caller asked for — see the parameter's remarks.
    /// </summary>
    private static int TwoPhaseSave(IReadOnlyList<CycleBreak> breaks, Func<bool, int> save, bool acceptAllChangesOnSuccess)
    {
        DropReferences(breaks);
        var affected = save(true);
        Redelete(breaks);
        return affected + save(acceptAllChangesOnSuccess);
    }

    /// <inheritdoc cref="TwoPhaseSave"/>
    private static async Task<int> TwoPhaseSaveAsync(IReadOnlyList<CycleBreak> breaks,
        Func<bool, CancellationToken, Task<int>> save, bool acceptAllChangesOnSuccess, CancellationToken token)
    {
        DropReferences(breaks);
        var affected = await save(true, token);
        Redelete(breaks);
        return affected + await save(acceptAllChangesOnSuccess, token);
    }


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
