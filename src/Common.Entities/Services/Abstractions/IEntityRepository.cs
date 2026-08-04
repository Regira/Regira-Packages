using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Services.Abstractions;

public interface IEntityRepository<TEntity> : IEntityRepository<TEntity, int>, IEntityService<TEntity>
    where TEntity : class, IEntity<int>;
public interface IEntityRepository<TEntity, TKey> : IEntityService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>;
public interface IEntityRepository<TEntity, TKey, in TSearchObject>
    : IEntityService<TEntity, TKey, TSearchObject>, IEntityRepository<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();


public interface IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes> 
    : IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>,
    IEntityRepository<TEntity, int, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;
public interface IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes> 
    : IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>,
    IEntityRepository<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;