using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// <c>e.Endpoints(o => …)</c> on a <c>For&lt;&gt;()</c> registration: how <c>MapEntityEndpoints()</c> maps the entity
/// (<see cref="EntityEndpointOptions"/>). Inert for an app that maps no endpoints. Each overload returns the builder it
/// was called on, so the registration chains on.
/// </summary>
public static class EntityEndpointBuilderExtensions
{
    public static EntityServiceBuilder<TContext, TEntity, TKey> Endpoints<TContext, TEntity, TKey>(
        this EntityServiceBuilder<TContext, TEntity, TKey> builder, Action<EntityEndpointOptions> configure)
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>
    {
        builder.Services.ConfigureEntityEndpoints(typeof(TEntity), configure);
        return builder;
    }

    public static EntityIntServiceBuilder<TContext, TEntity> Endpoints<TContext, TEntity>(
        this EntityIntServiceBuilder<TContext, TEntity> builder, Action<EntityEndpointOptions> configure)
        where TContext : DbContext
        where TEntity : class, IEntity<int>
    {
        builder.Services.ConfigureEntityEndpoints(typeof(TEntity), configure);
        return builder;
    }

    public static EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> Endpoints<TContext, TEntity, TKey, TSearchObject>(
        this EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> builder, Action<EntityEndpointOptions> configure)
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
    {
        builder.Services.ConfigureEntityEndpoints(typeof(TEntity), configure);
        return builder;
    }

    public static ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> Endpoints<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
        this ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> builder, Action<EntityEndpointOptions> configure)
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        builder.Services.ConfigureEntityEndpoints(typeof(TEntity), configure);
        return builder;
    }

    public static ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Endpoints<TContext, TEntity, TSearchObject, TSortBy, TIncludes>(
        this ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> builder, Action<EntityEndpointOptions> configure)
        where TContext : DbContext
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        builder.Services.ConfigureEntityEndpoints(typeof(TEntity), configure);
        return builder;
    }

    // one registration per entity: a second Endpoints() call adds to the options of the first
    private static void ConfigureEntityEndpoints(this IServiceCollection services, Type entityType, Action<EntityEndpointOptions> configure)
    {
        var registration = services
            .Where(d => d.ServiceType == typeof(EntityEndpointRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityEndpointRegistration>()
            .FirstOrDefault(r => r.EntityType == entityType);
        if (registration == null)
        {
            registration = new EntityEndpointRegistration(entityType, new EntityEndpointOptions());
            services.AddSingleton(registration);
        }
        configure(registration.Options);
    }
}
