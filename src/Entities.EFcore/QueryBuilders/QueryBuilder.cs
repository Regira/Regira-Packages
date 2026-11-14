using Microsoft.Extensions.Logging;
using Regira.DAL.Paging;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.QueryBuilders;

public class QueryBuilder<TEntity>(
    IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
    IEnumerable<IFilteredQueryBuilder<TEntity, int, SearchObject<int>>>? filters = null,
    ISortedQueryBuilder<TEntity, int>? sortedQueryBuilder = null,
    IIncludableQueryBuilder<TEntity, int>? includableQueryBuilder = null,
    ILoggerFactory? loggerFactory = null
) : QueryBuilder<TEntity, int>(globalFilters, filters, sortedQueryBuilder, includableQueryBuilder, loggerFactory)
    where TEntity : IEntity<int>;

public class QueryBuilder<TEntity, TKey>(
    IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
    IEnumerable<IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>>? filters = null,
    ISortedQueryBuilder<TEntity, TKey>? sortedQueryBuilder = null,
    IIncludableQueryBuilder<TEntity, TKey>? includableQueryBuilder = null,
    ILoggerFactory? loggerFactory = null
) : QueryBuilder<TEntity, TKey, SearchObject<TKey>>(globalFilters, filters, sortedQueryBuilder, includableQueryBuilder, loggerFactory)
    where TEntity : IEntity<TKey>;

public class QueryBuilder<TEntity, TKey, TSearchObject>(
    IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
    IEnumerable<IFilteredQueryBuilder<TEntity, TKey, TSearchObject>>? filters = null,
    ISortedQueryBuilder<TEntity, TKey>? sortedQueryBuilder = null,
    IIncludableQueryBuilder<TEntity, TKey>? includableQueryBuilder = null,
    ILoggerFactory? loggerFactory = null
)
    : QueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>(globalFilters, filters, sortedQueryBuilder, includableQueryBuilder, loggerFactory)
    where TEntity : IEntity<TKey>
    where TSearchObject : ISearchObject<TKey>
{
    public IQueryable<TEntity> Query(IQueryable<TEntity> query, IList<TSearchObject?>? searchObjects, PagingInfo? pagingInfo)
        => Query(query, searchObjects, [], null, pagingInfo);
}

public class QueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>(
    IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
    IEnumerable<IFilteredQueryBuilder<TEntity, int, TSearchObject>>? filters = null,
    ISortedQueryBuilder<TEntity, int, TSortBy>? sortedQueryBuilder = null,
    IIncludableQueryBuilder<TEntity, int, TIncludes>? includableQueryBuilder = null,
    ILoggerFactory? loggerFactory = null)
    : QueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>(globalFilters, filters, sortedQueryBuilder, includableQueryBuilder, loggerFactory)
    where TEntity : IEntity<int>
    where TSearchObject : ISearchObject<int>
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;


public class QueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
        IEnumerable<IGlobalFilteredQueryBuilder> globalFilters,
        IEnumerable<IFilteredQueryBuilder<TEntity, TKey, TSearchObject>>? filters = null,
        ISortedQueryBuilder<TEntity, TKey, TSortBy>? sortedQueryBuilder = null,
        IIncludableQueryBuilder<TEntity, TKey, TIncludes>? includableQueryBuilder = null,
        ILoggerFactory? loggerFactory = null
    )
    : IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : IEntity<TKey>
    where TSearchObject : ISearchObject<TKey>
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    readonly ILogger? _logger = loggerFactory?.CreateLogger<QueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();

    // Which global filters apply to TEntity, deduplicated to one variant per family (preferring the one
    // whose key matches TSearchObject). Stable for this closed generic type, so resolved once.
    private IReadOnlyList<IGlobalFilteredQueryBuilder>? _globalFilters;
    private IReadOnlyList<IGlobalFilteredQueryBuilder> GlobalFilters => _globalFilters ??= ResolveGlobalFilters();

    private IReadOnlyList<IGlobalFilteredQueryBuilder> ResolveGlobalFilters()
    {
        var applicable = globalFilters.Where(filter => GlobalFilterScope.AppliesTo(filter, typeof(TEntity)));
        return GlobalFilterSelector.Select<TKey>(applicable, _logger);
    }

    public virtual IQueryable<TEntity> Query(IQueryable<TEntity> query, IList<TSearchObject?>? searchObjects, IList<TSortBy> sortBy, TIncludes? includes, PagingInfo? pagingInfo)
    {
        var filteredQuery = FilterList(query, searchObjects);
        var sortedQuery = SortByList(filteredQuery, searchObjects, sortBy, includes);
        var pagedQuery = sortedQuery.PageQuery(pagingInfo);
        var includingQuery = AddIncludes(pagedQuery, searchObjects, sortBy, includes);

        return includingQuery;
    }

    public virtual IQueryable<TEntity> Filter(IQueryable<TEntity> query, TSearchObject? so)
    {
        var globallyFilteredQuery = GlobalFilters
            .Aggregate(
                query,
                (filteredQuery, filter) =>
                {
                    _logger?.LogDebug("Applying global filter {Filter} for type {EntityType}", filter.GetType().FullName, typeof(TEntity).FullName);
                    return filter.Build(filteredQuery, so);
                }
            );
        return filters
            ?.Aggregate(
                globallyFilteredQuery,
                (filteredQuery, filter) =>
                {
                    _logger?.LogDebug("Applying filter {Filter} for type {EntityType}", filter.GetType().FullName, typeof(TEntity).FullName);
                    return filter.Build(filteredQuery, so);
                }) ?? globallyFilteredQuery;
    }
    /// <summary>
    /// The union of <see cref="Filter"/> over each search object. An empty or <c>null</c> list filters as one
    /// <c>null</c> search object, so the global filters (row scopes, the archived filter) always run.
    /// </summary>
    public virtual IQueryable<TEntity> FilterList(IQueryable<TEntity> query, IList<TSearchObject?>? searchObjects)
    {
        if (searchObjects == null || searchObjects.Count == 0)
        {
            return Filter(query, default);
        }
        return searchObjects
            .Skip(1)
            .Aggregate(Filter(query, searchObjects[0]), (r, so) => r.Union(Filter(query, so)));
    }

    public virtual IQueryable<TEntity> SortBy(IQueryable<TEntity> query, IList<TSearchObject?>? so, TSortBy? sortBy, TIncludes? includes)
        => sortedQueryBuilder?.SortBy(query, sortBy) ?? query;
    public virtual IQueryable<TEntity> SortByList(IQueryable<TEntity> query, IList<TSearchObject?>? so, IList<TSortBy>? sortByList, TIncludes? includes)
    {
        if (sortByList?.Any() != true)
        {
            return SortBy(query, so, null, includes);
        }
        return sortByList.Aggregate(query, (current, by) => SortBy(current, so, by, includes));
    }

    public virtual IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, IList<TSearchObject?>? so, IList<TSortBy>? sortByList, TIncludes? includes)
        => includableQueryBuilder?.AddIncludes(query, includes) ?? query;
}