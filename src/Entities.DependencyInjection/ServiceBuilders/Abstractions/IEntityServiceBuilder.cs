using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;

// ReSharper disable UnusedTypeParameter
public interface IEntityServiceBuilder<TContext, TEntity> : IEntityServiceBuilder<TContext, TEntity, int>
    where TContext : DbContext
    where TEntity : class, IEntity<int>;
public interface IEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject> : IEntityServiceBuilder<TContext, TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();
public interface IEntityServiceBuilder<TContext, TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    IServiceCollection Services { get; }
    bool HasEntityService();
    bool HasService<TService>();
}
public interface IEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> : IEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;
// ReSharper restore UnusedTypeParameter

