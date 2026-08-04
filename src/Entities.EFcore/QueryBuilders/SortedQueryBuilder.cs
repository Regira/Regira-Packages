using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders;

public class SortedQueryBuilder<TEntity>(Func<IQueryable<TEntity>, IQueryable<TEntity>> sortQuery)
    : SortedQueryBuilder<TEntity, int>(sortQuery)
    where TEntity : IEntity<int>;

public class SortedQueryBuilder<TEntity, TKey>(Func<IQueryable<TEntity>, IQueryable<TEntity>> sortQuery)
    : SortedQueryBuilder<TEntity, TKey, EntitySortBy>((query, _) => sortQuery(query)), ISortedQueryBuilder<TEntity, TKey>
    where TEntity : IEntity<TKey>;
public class SortedQueryBuilder<TEntity, TKey, TSortBy>(Func<IQueryable<TEntity>, TSortBy?, IQueryable<TEntity>> sortQuery)
    : ISortedQueryBuilder<TEntity, TKey, TSortBy>
    where TEntity : IEntity<TKey>
    where TSortBy : struct, Enum
{
    public virtual IQueryable<TEntity> SortBy(IQueryable<TEntity> query, TSortBy? sortBy)
        => sortQuery(query, sortBy);
}