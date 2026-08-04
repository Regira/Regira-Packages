using Regira.DAL.Paging;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public interface IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : IEntity<TKey>
    where TSearchObject : ISearchObject<TKey>
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    IQueryable<TEntity> Filter(IQueryable<TEntity> query, TSearchObject? so);
    IQueryable<TEntity> FilterList(IQueryable<TEntity> query, IList<TSearchObject?>? so);
    IQueryable<TEntity> SortByList(IQueryable<TEntity> query, IList<TSearchObject?>? so, IList<TSortBy>? sortByList, TIncludes? includes);
    IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, IList<TSearchObject?>? so, IList<TSortBy>? sortByList, TIncludes? includes);

    IQueryable<TEntity> Query(IQueryable<TEntity> query, IList<TSearchObject?>? searchObjects, IList<TSortBy> sortBy, TIncludes? includes, PagingInfo? pagingInfo);
}
