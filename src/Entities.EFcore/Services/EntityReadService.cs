using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.DAL.Paging;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services;
using Regira.Entities.Services.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Services;

public class EntityReadService<TContext, TEntity, TKey, TSearchObject>(TContext dbContext, IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes> queryBuilder,
    IEnumerable<IEntityProcessor<TEntity, EntityIncludes>> entityProcessors, ILoggerFactory? loggerFactory = null)
    : EntityReadService<TContext, TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>(dbContext, queryBuilder, entityProcessors, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();

public class EntityReadService<TContext, TEntity, TSearchObject, TSortBy, TIncludes>(TContext dbContext, IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes> queryBuilder,
    IEnumerable<IEntityProcessor<TEntity, TIncludes>> entityProcessors, ILoggerFactory? loggerFactory = null)
    : EntityReadService<TContext, TEntity, int, TSearchObject, TSortBy, TIncludes>(dbContext, queryBuilder, entityProcessors, loggerFactory)
    where TContext : DbContext
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

public class EntityReadService<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>
(TContext dbContext, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes> queryBuilder,
    IEnumerable<IEntityProcessor<TEntity, TIncludes>> entityProcessors, ILoggerFactory? loggerFactory = null)
    : IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    protected ILogger? Logger = loggerFactory?.CreateLogger<EntityWriteService<TContext, TEntity, TKey>>();
    public virtual DbSet<TEntity> DbSet => dbContext.Set<TEntity>();
    // Details
    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default)
        => Details(id, null, token);
    /// <inheritdoc cref="IEntityReadService{TEntity,TKey}.Details(TKey,ArchivedFilter?,CancellationToken)"/>
    public virtual async Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default)
    {
        if (id == null || id.Equals(default(TKey)))
        {
            return null;
        }

        // Details always loads the full graph: the OR of every registered include flag
        var effectiveIncludes = EnumUtility.GetMaxFlagValue<TIncludes>();

        Logger?.LogDebug($"Fetching details for {typeof(TEntity).FullName} #{id} includes {effectiveIncludes}");

        // the synthesized search object still travels through the single global-filter loop — only the
        // archived filter reads Archived, so all other global filters keep applying
        var item = await queryBuilder.Query(DbSet, [Convert(new { Id = id, Archived = archived })], [], effectiveIncludes, null)
            .AsNoTrackingWithIdentityResolution()
            .SingleOrDefaultAsync(token);

        if (item == null || item.Id?.Equals(id) != true)
        {
            // make sure IDs match
            return null;
        }

        Logger?.LogDebug($"Fetched details for {typeof(TEntity).FullName} #{id}");

        foreach (var entityProcessor in entityProcessors)
        {
            Logger?.LogDebug($"Processing {typeof(TEntity).FullName} #{item.Id} using {entityProcessor.GetType().FullName}");
            await entityProcessor.Process([item], effectiveIncludes, token);
        }

        return item;
    }

    // List
    public virtual async Task<IList<TEntity>> List(IList<TSearchObject?> searchObjects, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
    {
        Logger?.LogDebug($"Fetching list for {typeof(TEntity).FullName} sorting by {string.Join(",", sortBy)} includes {includes}");

        var items = await queryBuilder.Query(DbSet, searchObjects, sortBy, includes, pagingInfo)
            .AsNoTrackingWithIdentityResolution()
            .ToListAsync(token);

        foreach (var entityProcessor in entityProcessors)
        {
            Logger?.LogDebug($"Processing {typeof(TEntity).FullName} #({string.Join(",", items.Select(x => x.Id))}) using {entityProcessor.GetType().FullName}");
            await entityProcessor.Process(items, includes, token);
        }

        return items;
    }

    public virtual async Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => await List([so], [], null, pagingInfo, token);
    Task<IList<TEntity>> IEntityReadService<TEntity, TKey>.List(object? so, PagingInfo? pagingInfo, CancellationToken token)
        => List(Convert(so), pagingInfo, token);

    // Count
    public virtual Task<long> Count(IList<TSearchObject?> searchObjects, CancellationToken token = default)
        => queryBuilder.FilterList(dbContext.Set<TEntity>(), searchObjects).LongCountAsync(token);
    public virtual Task<long> Count(TSearchObject? so = null, CancellationToken token = default)
        => queryBuilder.Filter(DbSet, so).LongCountAsync(token);
    Task<long> IEntityReadService<TEntity, TKey>.Count(object? so, CancellationToken token)
        => Count(Convert(so), token);

    // Helpers
    public virtual TSearchObject? Convert(object? so)
        => SearchObjectCoercion.Coerce<TSearchObject>(so);
}
