using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders;

public class IncludableQueryBuilder<TEntity>(Func<IQueryable<TEntity>, EntityIncludes?, IQueryable<TEntity>> addIncludes) : IncludableQueryBuilder<TEntity, int>(addIncludes)
    where TEntity : IEntity<int>;

public class IncludableQueryBuilder<TEntity, TKey>(Func<IQueryable<TEntity>, EntityIncludes?, IQueryable<TEntity>> addIncludes)
    : IncludableQueryBuilder<TEntity, TKey, EntityIncludes>(addIncludes), IIncludableQueryBuilder<TEntity, TKey>
    where TEntity : IEntity<TKey>;

public class IncludableQueryBuilder<TEntity, TKey, TIncludes>(Func<IQueryable<TEntity>, TIncludes?, IQueryable<TEntity>> addIncludes)
    : IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    where TEntity : IEntity<TKey>
    where TIncludes : struct, Enum
{
    public virtual IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, TIncludes? includes = null)
        => addIncludes(query, includes);
}