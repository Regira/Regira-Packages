using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public interface IIncludableQueryBuilder<TEntity, TKey> : IIncludableQueryBuilder<TEntity, TKey, EntityIncludes>
    where TEntity : IEntity<TKey>;
public interface IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    where TEntity : IEntity<TKey>
    where TIncludes : struct, Enum
{
    IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, TIncludes? includes = null);
}
