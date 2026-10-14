using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers;
using Regira.Entities.Preppers.Abstractions;

namespace Regira.Entities.DependencyInjection.Preppers;

public static class ServiceCollectionPrepperExtensions
{
    // Preppers
    /// <summary>
    /// Registers a prepper class for every entity it is scoped to — an interface or base type reaches every entity
    /// implementing it. A class registered more than once runs once.
    /// </summary>
    public static IServiceCollection AddPrepper<TPrepper>(this IServiceCollection services)
        where TPrepper : class, IEntityPrepper
    {
        // TryAddEnumerable: the same interface-scoped prepper added in several For<>() blocks must not run twice per save
        services.TryAddEnumerable(ServiceDescriptor.Transient<IEntityPrepper, TPrepper>());
        return services;
    }

    /// <inheritdoc cref="AddPrepper{TPrepper}(IServiceCollection)"/>
    public static IServiceCollection AddPrepper<TEntity, TPrepper>(this IServiceCollection services)
        where TPrepper : class, IEntityPrepper<TEntity>
    {
        AddPrepper<TPrepper>(services);
        services.TryAddEnumerable(ServiceDescriptor.Transient<IEntityPrepper<TEntity>, TPrepper>());
        return services;
    }
    public static IServiceCollection AddPrepper<TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityPrepper
        => services.AddTransient<IEntityPrepper>(factory);

    public static IServiceCollection AddPrepper<TEntity>(this IServiceCollection services, Func<IServiceProvider, IEntityPrepper<TEntity>> factory)
    {
        services.AddTransient<IEntityPrepper>(factory);
        services.AddTransient(factory);
        return services;
    }

    public static IServiceCollection AddPrepper<TEntity>(this IServiceCollection services, Action<TEntity> prepareFunc)
        where TEntity : class
        => services.AddTransient<IEntityPrepper>(_ => new EntityPrepper<TEntity>(prepareFunc));

    public static IServiceCollection AddPrepper<TContext, TEntity>(this IServiceCollection services, Func<TEntity, TContext, Task> prepareFunc)
        where TContext : DbContext
        where TEntity : class
        => services.AddTransient<IEntityPrepper>(p => new EntityPrepper<TContext, TEntity>(p.GetRequiredService<TContext>(), prepareFunc));


    /// <inheritdoc cref="AddPrepper{TPrepper}(IServiceCollection)"/>
    public static EntityServiceCollectionOptions AddPrepper<TImplementation>(this EntityServiceCollectionOptions options)
        where TImplementation : class, IEntityPrepper
    {
        options.Services.AddPrepper<TImplementation>();
        return options;
    }
    /// <inheritdoc cref="AddPrepper{TPrepper}(IServiceCollection)"/>
    public static EntityServiceCollectionOptions AddPrepper<TImplementation, TKey>(this EntityServiceCollectionOptions options)
        where TImplementation : class, IEntityPrepper<TKey>
    {
        options.Services.AddPrepper<TKey, TImplementation>();
        return options;
    }
    public static EntityServiceCollectionOptions AddPrepper<TContext, TEntity>(this EntityServiceCollectionOptions options, Func<TEntity, TContext, Task> prepareFunc)
        where TContext : DbContext
        where TEntity : class
    {
        options.Services.AddPrepper(prepareFunc);
        return options;
    }
    public static EntityServiceCollectionOptions AddPrepper<TEntity>(this EntityServiceCollectionOptions options, Action<TEntity> prepareFunc)
        where TEntity : class
    {
        options.Services.AddPrepper(prepareFunc);
        return options;
    }

    /// <summary>
    /// Add default Preppers
    /// <list type="bullet">
    ///     <item><see cref="AutoServerOwnedPrepper"/> — enforces <see cref="ServerOwnedAttribute"/> on every entity</item>
    /// </list>
    /// Inert until an entity marks a property <c>[ServerOwned]</c>. Preppers run in registration order, so
    /// an entity's own prepper can take a marked field over as long as it is registered after this call —
    /// which every <c>.For&lt;&gt;()</c> chain after <c>UseDefaults()</c> is.
    /// </summary>
    public static EntityServiceCollectionOptions AddDefaultPreppers(this EntityServiceCollectionOptions options)
    {
        // TryAddEnumerable: repeating UseDefaults() must not put the same restore in the pipeline twice
        options.Services.TryAddEnumerable(ServiceDescriptor.Transient<IEntityPrepper, AutoServerOwnedPrepper>());
        return options;
    }
}