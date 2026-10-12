using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;

/// <summary>
/// Every <c>For&lt;&gt;()</c> builder, whatever its shape: what an extension that only records something about the entity
/// needs, and keeps the builder's own type by taking it as a type parameter (<c>where TBuilder : IEntityServiceBuilder</c>).
/// </summary>
public interface IEntityServiceBuilder
{
    IServiceCollection Services { get; }
    Type EntityType { get; }
}
// ReSharper disable UnusedTypeParameter
public interface IEntityServiceBuilder<TContext, TEntity> : IEntityServiceBuilder<TContext, TEntity, int>
    where TContext : DbContext
    where TEntity : class, IEntity<int>;
public interface IEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject> : IEntityServiceBuilder<TContext, TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new();
public interface IEntityServiceBuilder<TContext, TEntity, TKey> : IEntityServiceBuilder
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    new IServiceCollection Services { get; }
    IServiceCollection IEntityServiceBuilder.Services => Services;
    Type IEntityServiceBuilder.EntityType => typeof(TEntity);
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

