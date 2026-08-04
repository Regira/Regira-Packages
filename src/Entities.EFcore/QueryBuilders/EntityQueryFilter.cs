using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders;

public class EntityQueryFilter<TEntity, TKey, TSearchObject>(Func<IQueryable<TEntity>, TSearchObject?, IQueryable<TEntity>> filterFunc) : IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    public IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so)
        => filterFunc(query, so);
}