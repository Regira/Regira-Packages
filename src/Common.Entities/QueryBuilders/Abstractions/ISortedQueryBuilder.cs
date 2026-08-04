using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public interface ISortedQueryBuilder<TEntity, TKey> : ISortedQueryBuilder<TEntity, TKey, EntitySortBy>
    where TEntity : IEntity<TKey>;

public interface ISortedQueryBuilder<TEntity, TKey, TSortBy>
    where TEntity : IEntity<TKey>
    where TSortBy : struct, Enum
{
    IQueryable<TEntity> SortBy(IQueryable<TEntity> query, TSortBy? sortBy = null);
}
