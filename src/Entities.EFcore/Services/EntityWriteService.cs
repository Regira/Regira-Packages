using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.EFcore.Services;

public class EntityWriteService<TContext, TEntity>(
    TContext dbContext,
    IEntityReadService<TEntity, int> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntity, int>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<int>
{
    /// <summary>
    /// A write service without validators: nothing registered with <c>AddValidator</c> or <c>Validate</c> runs for it.
    /// A subclass passes <c>IEnumerable&lt;IEntityValidator&gt;</c> to the other constructor to run them.
    /// </summary>
    public EntityWriteService(TContext dbContext, IEntityReadService<TEntity, int> readService, IEnumerable<IEntityPrepper> preppers,
        ILoggerFactory? loggerFactory = null)
        : this(dbContext, readService, preppers, [], loggerFactory)
    {
    }
}


/// <summary>
/// Writes <typeparamref name="TEntity"/> through its <typeparamref name="TContext"/>: runs the preppers and the
/// validators, then tracks the change for <see cref="SaveChanges"/>.
/// </summary>
public class EntityWriteService<TContext, TEntity, TKey>(
    TContext dbContext,
    IEntityReadService<TEntity, TKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : IEntityWriteService<TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    /// <summary>
    /// A write service without validators: nothing registered with <c>AddValidator</c> or <c>Validate</c> runs for it.
    /// A subclass passes <c>IEnumerable&lt;IEntityValidator&gt;</c> to the other constructor to run them.
    /// </summary>
    public EntityWriteService(TContext dbContext, IEntityReadService<TEntity, TKey> readService, IEnumerable<IEntityPrepper> preppers,
        ILoggerFactory? loggerFactory = null)
        : this(dbContext, readService, preppers, [], loggerFactory)
    {
    }

    protected ILogger? Logger = loggerFactory?.CreateLogger<EntityWriteService<TContext, TEntity, TKey>>();

    protected TContext DbContext = dbContext;
    public virtual DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    public virtual async Task Add(TEntity item, CancellationToken token = default)
    {
        var refusable = CanBeRefused(item);
        if (refusable)
        {
            DetectCallerChanges();
        }
        using (var marked = refusable ? new ChangeTrackerLog(DbContext) : null)
        {
            await PrepareItem(item, null, token);
            await ValidateMarked(marked, item, null, EntityWriteOperation.Add, token);
        }

        Logger?.LogDebug($"Adding new {typeof(TEntity).FullName}");

        DbSet.Add(item);
    }
    public virtual async Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
    {
        var refusable = CanBeRefused(item);
        if (refusable)
        {
            DetectCallerChanges();
        }

        // The write path resolves its row archived-inclusive, decoupled from the public read contract:
        // GET /{id} keeps 404-ing on archived rows while a restore (or any write on an archived row)
        // still finds its original. This stays a FILTERED lookup, never a raw DbSet fetch: it is the
        // regular query pipeline, and ArchivedFilter.Included widens nothing but the archived flag —
        // every IGlobalFilteredQueryBuilder (tenant/owner row security) still runs, and so does every EF
        // query filter the app configured itself (on net10.0 only the named archived filter is ignored;
        // on net8.0 no query filter is ignored at all). See QueryExtensions.FilterArchivable.
        var original = await readService.Details(item.Id, ArchivedFilter.Included, token);

        Logger?.LogDebug($"Modifying {typeof(TEntity).FullName} #{item.Id} {(original == null ? "" : " with original")}");

        // Before the preppers, over the whole graph: a Related() sync attaches each child's graph, which reaches
        // this entity through a back-reference and every sibling through this entity — their fixup, which undoes
        // a foreign-key change, would otherwise run before TrackAsUpdateOf gets to check each of them.
        if (original != null)
        {
            DbContext.DropStaleReferencesInGraph(item, original);
        }

        // The concurrency tokens the client sent, read before any prepper runs: a prepper may overwrite them
        // ([ServerOwned] restores from the stored row), and the check has to compare the client's value.
        var clientTokens = DbContext.CaptureClientTokens(item);

        // no stored row, no validation (below): nothing refuses the write, so there is no log to keep
        using (var marked = refusable && original != null ? new ChangeTrackerLog(DbContext) : null)
        {
            await PrepareItem(item, original, token);
            // no stored row, no update to check: Modify answers null (not found)
            if (original != null)
            {
                await ValidateMarked(marked, item, original, EntityWriteOperation.Modify, token);
            }
        }

        if (original != null)
        {
            DbContext.TrackAsUpdateOf(item, original, clientTokens);
        }

        return original;
    }

    /// <summary>
    /// Runs <see cref="ValidateItem"/> after the preppers, before the entity is tracked. A <c>Related()</c> sync has already
    /// marked child rows by then, so a refused write takes back everything <paramref name="marked"/> saw marked, and stops
    /// tracking <paramref name="item"/> itself should the caller have tracked it: nothing of the write reaches a later
    /// <see cref="SaveChanges"/> in the same scope. No log (<c>null</c>): nothing can refuse the write.
    /// </summary>
    private async Task ValidateMarked(ChangeTrackerLog? marked, TEntity item, TEntity? original, EntityWriteOperation operation, CancellationToken token)
    {
        if (marked == null)
        {
            await ValidateItem(item, original, operation, token);
            return;
        }
        try
        {
            await ValidateItem(item, original, operation, token);
        }
        catch
        {
            // a prepper's edit to a row tracked before this write becomes a state change the log can undo; the refusal is
            // still what the caller gets — a detection failure must neither replace it nor keep the undo from running
            try
            {
                DetectPendingChanges();
            }
            catch (Exception detectionFailure)
            {
                Logger?.LogWarning(detectionFailure, "Detecting the changes a refused {EntityType} write made failed; edits to rows tracked before the write may stay",
                    typeof(TEntity).FullName);
            }
            marked.Undo(item);
            throw;
        }
    }
    /// <summary>
    /// Whether a validator runs for <paramref name="item"/>, so that the write can be refused. Only then do <see cref="Add"/>
    /// and <see cref="Modify"/> let EF detect pending edits and record what the write marks, for a refusal to take back —
    /// a pass over every tracked row that a write nothing can refuse does not pay.
    /// </summary>
    private bool CanBeRefused(TEntity item) => validators.AnyApplyTo(item.GetType());
    /// <summary>
    /// Lets EF notice the edits made to tracked rows, whose values it otherwise compares only when it saves. Before a write,
    /// so what the caller changed beforehand is recorded as the caller's and a refusal leaves it alone; on a refusal, so
    /// what the preppers changed becomes a state change the refusal takes back. A context that does not detect changes on
    /// its own (<c>AutoDetectChangesEnabled = false</c>, as bulk jobs set it) leaves detection to its caller here too.
    /// </summary>
    private void DetectPendingChanges()
    {
        if (DbContext.ChangeTracker.AutoDetectChangesEnabled)
        {
            DbContext.ChangeTracker.DetectChanges();
        }
    }
    /// <summary>
    /// <see cref="DetectPendingChanges"/> before a write that can be refused. A required relationship the caller severed on a
    /// row tracked earlier makes EF throw here; that failure is the caller's, not this write's, so it is logged and left to
    /// <see cref="SaveChanges"/>, which reports it as it would for a write no validator runs for.
    /// </summary>
    private void DetectCallerChanges()
    {
        try
        {
            DetectPendingChanges();
        }
        catch (InvalidOperationException ex) when (ex.IsSeveredRequiredRelationship())
        {
            Logger?.LogWarning(ex, "Detecting the pending changes before a {EntityType} write failed; SaveChanges reports it",
                typeof(TEntity).FullName);
        }
    }
    public virtual Task Save(TEntity item, CancellationToken token = default)
        => item.IsNew() ? Add(item, token) : Modify(item, token);
    public virtual async Task Remove(TEntity item, CancellationToken token = default)
    {
        await ValidateItem(item, null, EntityWriteOperation.Remove, token);
        await RemoveItem(item, token);
    }
    /// <summary>
    /// Marks <paramref name="item"/> for removal, once <see cref="Remove"/> validated it. Override this rather than
    /// <see cref="Remove"/> to mark more rows alongside it, so nothing is marked for a delete the validators reject.
    /// </summary>
    protected virtual Task RemoveItem(TEntity item, CancellationToken token = default)
    {
        // an IArchivable becomes a soft delete at the save, which leaves its dependents as they are: EF must not cascade to them now
        RemoveGuarded(() => DbContext.RemoveWithoutCascade(item, () => DbSet.Remove(item)), item.Id);
        Logger?.LogDebug($"Removing {typeof(TEntity).FullName} #{item.Id}");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Marks a row Deleted, reporting a required relationship the change tracker cannot sever as the same
    /// constraint conflict the database would have raised.<br />
    /// Marking is already enough to trip it: EF cascades the state change to tracked dependents there and
    /// then, so the failure surfaces here rather than at <see cref="SaveChanges"/> — see that method for the
    /// counterpart on the flush path. <see cref="Remove"/> marks an <see cref="IArchivable"/> without that cascade:
    /// a soft delete severs nothing. Every removal has to go through this, including principals deleted
    /// alongside the entity itself, or one unguarded <c>Remove</c> puts the raw exception back.
    /// </summary>
    protected void RemoveGuarded(Action remove, object? id = null)
    {
        try
        {
            remove();
        }
        catch (InvalidOperationException ex) when (ex.IsSeveredRequiredRelationship())
        {
            Logger?.LogWarning(ex, "A required relationship blocked the removal of {EntityType} #{Id}", typeof(TEntity).FullName, id);
            throw new EntityConstraintException(EntityConstraintException.ClientMessage, ex);
        }
    }

    public virtual async Task PrepareItem(TEntity item, TEntity? original, CancellationToken token = default)
    {
        var matchingPreppers = preppers.FindMatchingServices(item);
        foreach (var prepper in matchingPreppers)
        {
            Logger?.LogDebug($"Preparing {typeof(TEntity).FullName} #{item.Id} using {prepper.GetType().FullName}");
            await prepper.Prepare(item, original, token);
        }
    }

    /// <summary>
    /// Runs the validators in scope of <paramref name="item"/> and throws an <see cref="EntityInputException{T}"/> of
    /// <typeparamref name="TEntity"/> holding every error they added. <see cref="Add"/> and <see cref="Modify"/> call it
    /// after the preppers — <see cref="Modify"/> only when the stored row was found — and <see cref="Remove"/> before
    /// anything is marked. Not an override point: validators are the one way to refuse a write, and whether one runs for
    /// the item decides whether <see cref="Add"/> and <see cref="Modify"/> record what a refusal takes back.
    /// </summary>
    public Task ValidateItem(TEntity item, TEntity? original, EntityWriteOperation operation, CancellationToken token = default)
        => validators.ValidateItem(item, original, operation, token);

    /// <summary>
    /// Saves changes to DB, and detaches all entries in ChangeTracker to prevent issues with stale entries in future operations.<br />
    /// A write built on a stale read — a concurrency token the row no longer holds, or a row another writer
    /// removed — surfaces as <see cref="EntityConcurrencyException"/>; a database integrity-constraint violation
    /// as <see cref="EntityConstraintException"/>. Transient faults (deadlocks, timeouts) are not wrapped.<br />
    /// The clear happens on <b>success only</b> — deliberately asymmetric: on failure (including
    /// <see cref="EntityConstraintException"/> and <see cref="EntityConcurrencyException"/>) the tracker keeps its
    /// entries, matching stock EF Core semantics, so a direct caller (seeding, jobs) can fix or remove the
    /// offending entity and retry — or discard the scope. Only a successful save deviates from stock EF Core by clearing.
    /// </summary>
    /// <param name="token"></param>
    /// <returns></returns>
    public virtual async Task<int> SaveChanges(CancellationToken token = default)
    {
#if DEBUG
        Logger?.LogDebug(DbContext.ChangeTracker.DebugView.ShortView);
#endif
        try
        {
            var count = await DbContext.SaveChangesAsync(token);
            DbContext.ChangeTracker.Clear();
            return count;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // EF raises it for any UPDATE/DELETE that matched no row: the row no longer holds the concurrency token
            // the client sent, or another writer removed it. Either way the change was built on a stale read, and
            // the caller's answer is to reload rather than to fix its input — not a constraint violation.
            Logger?.LogWarning(ex, "A concurrent write rejected the change for {EntityType} (conflicting: {ConflictingEntries})",
                typeof(TEntity).FullName, string.Join(", ", ex.Entries.Select(e => e.Metadata.ClrType.Name).Distinct()));
            throw new EntityConcurrencyException(EntityConcurrencyException.ClientMessage, ex);
        }
        catch (DbUpdateException ex) when (ex.IsConstraintViolation())
        {
            Logger?.LogWarning(ex, "Database constraint rejected the change for {EntityType}", typeof(TEntity).FullName);
            // the provider's constraint text stays in the log + InnerException — Message must be safe to
            // render anywhere (dev exception pages, generic exception handlers) without leaking index
            // names or other users' values
            throw new EntityConstraintException(EntityConstraintException.ClientMessage, ex);
        }
        catch (InvalidOperationException ex) when (ex.IsSeveredRequiredRelationship())
        {
            // The change tracker rejected the same integrity rule the database would have, one step earlier:
            // tracked dependents make EF fix up the severed required FK client-side, so the provider is never
            // reached and no DbUpdateException is raised. Same cause, same answer to the caller.
            Logger?.LogWarning(ex, "A required relationship blocked the change for {EntityType}", typeof(TEntity).FullName);
            throw new EntityConstraintException(EntityConstraintException.ClientMessage, ex);
        }
    }
}
