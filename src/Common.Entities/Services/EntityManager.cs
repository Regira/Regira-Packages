using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.Services;

public class EntityManager<TEntity>(IEntityRepository<TEntity, int, SearchObject<int>> repo)
    : EntityManager<TEntity, int>(repo), IEntityManager<TEntity>
    where TEntity : class, IEntity<int>;

public class EntityManager<TEntity, TKey>(IEntityRepository<TEntity, TKey, SearchObject<TKey>> repo)
    : EntityManager<TEntity, TKey, SearchObject<TKey>>(repo)
    where TEntity : class, IEntity<TKey>;

public class EntityManager<TEntity, TKey, TSearchObject>(IEntityRepository<TEntity, TKey, TSearchObject> repo)
    : EntityWrappingServiceBase<TEntity, TKey, TSearchObject>(repo), IEntityManager<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    protected readonly IEntityRepository<TEntity, TKey> Repo = repo;
}


public class EntityManager<TEntity, TSearchObject, TSortBy, TIncludes>(
    IEntityRepository<TEntity, int, TSearchObject, TSortBy, TIncludes> repo)
    : EntityManager<TEntity, int, TSearchObject, TSortBy, TIncludes>(repo),
        IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

public class EntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes> repo)
    : EntityWrappingServiceBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(repo),
        IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    protected readonly IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes> Repo = repo;
}