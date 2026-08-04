using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public interface IFilteredQueryBuilder<TEntity, TKey, in TSearchObject>
    where TSearchObject : ISearchObject<TKey>
{
    IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so);
}
