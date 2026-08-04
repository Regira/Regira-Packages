using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Services.Abstractions;

// with ID type
public interface IEntityService<TEntity, TKey> : IEntityReadService<TEntity, TKey>, IEntityWriteService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>;

public interface IEntityService<TEntity, TKey, in TSearchObject>
    : IEntityReadService<TEntity, TKey, TSearchObject>, IEntityService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();

public interface IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    : IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

// without ID type (-> int)
public interface IEntityService<TEntity> : IEntityService<TEntity, int>
    where TEntity : class, IEntity<int>;
public interface IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
    : IEntityService<TEntity, int, TSearchObject, TSortBy, TIncludes>, IEntityService<TEntity>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;