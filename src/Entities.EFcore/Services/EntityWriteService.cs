using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.EFcore.Services;

public class EntityWriteService<TContext, TEntity>(
    TContext dbContext,
    IEntityReadService<TEntity, int> readService,
    IEnumerable<IEntityPrepper> preppers,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntity, int>(dbContext, readService, preppers, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<int>;


public class EntityWriteService<TContext, TEntity, TKey>(
    TContext dbContext,
    IEntityReadService<TEntity, TKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    ILoggerFactory? loggerFactory = null)
    : IEntityWriteService<TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    protected ILogger? Logger = loggerFactory?.CreateLogger<EntityWriteService<TContext, TEntity, TKey>>();

    protected TContext DbContext = dbContext;
    public virtual DbSet<TEntity> DbSet => DbContext.Set<TEntity>();

    public virtual async Task Add(TEntity item, CancellationToken token = default)
    {
        await PrepareItem(item, null, token);

        Logger?.LogDebug($"Adding new {typeof(TEntity).FullName}");

        DbSet.Add(item);
    }
    public virtual async Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
    {
        // The write path resolves its row archived-inclusive, decoupled from the public read contract:
        // GET /{id} keeps 404-ing on archived rows while a restore (or any write on an archived row)
        // still finds its original. This stays a FILTERED lookup, never a raw DbSet fetch: it is the
        // regular query pipeline, and ArchivedFilter.Included widens nothing but the archived flag —
        // every IGlobalFilteredQueryBuilder (tenant/owner row security) still runs, and so does every EF
        // query filter the app configured itself (on net10.0 only the named archived filter is ignored;
        // on net8.0 no query filter is ignored at all). See QueryExtensions.FilterArchivable.
        var original = await readService.Details(item.Id, ArchivedFilter.Included, token);

        Logger?.LogDebug($"Modifying {typeof(TEntity).FullName} #{item.Id} {(original == null ? "" : " with original")}");

        await PrepareItem(item, original, token);

        if (original != null)
        {
            DbContext.Entry(original).State = EntityState.Detached;
            DbContext.Attach(item);
            DbContext.Entry(item).OriginalValues.SetValues(original);
            DbContext.Entry(item).State = EntityState.Modified;
        }

        return original;
    }
    public virtual Task Save(TEntity item, CancellationToken token = default)
        => item.IsNew() ? Add(item, token) : Modify(item, token);
    public virtual Task Remove(TEntity item, CancellationToken token = default)
    {
        DbSet.Remove(item);
        Logger?.LogDebug($"Removing {typeof(TEntity).FullName} #{item.Id}");
        return Task.CompletedTask;
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
    /// Saves changes to DB, and detaches all entries in ChangeTracker to prevent issues with stale entries in future operations.<br />
    /// The clear happens on <b>success only</b> — deliberately asymmetric: on failure (including
    /// <see cref="EntityConstraintException"/>) the tracker keeps its entries, matching stock EF Core
    /// semantics, so a direct caller (seeding, jobs) can fix or remove the offending entity and retry —
    /// or discard the scope. Only a successful save deviates from stock EF Core by clearing.
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
        catch (DbUpdateException ex) when (ex.IsConstraintViolation())
        {
            Logger?.LogWarning(ex, "Database constraint rejected the change for {EntityType}", typeof(TEntity).FullName);
            // the provider's constraint text stays in the log + InnerException — Message must be safe to
            // render anywhere (dev exception pages, generic exception handlers) without leaking index
            // names or other users' values
            throw new EntityConstraintException(EntityConstraintException.ClientMessage, ex);
        }
    }
}
