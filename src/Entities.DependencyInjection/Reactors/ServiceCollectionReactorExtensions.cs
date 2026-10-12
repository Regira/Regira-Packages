using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.DependencyInjection.Reactors;

/// <summary>
/// Reactor registrations. Each also registers the <see cref="IServiceCollection"/> itself, as <c>UseEntities()</c> does:
/// the reactor interceptor finds the reactors, and what each reacts to, in the registrations.
/// </summary>
public static class ServiceCollectionReactorExtensions
{
    /// <summary>
    /// Registers a reactor that declares what it reacts to itself: every entity its <see cref="IEntityReactor{TEntity}"/>
    /// covers (an interface or base type reaches every entity implementing it), or every entity when it implements
    /// only <see cref="IEntityReactor"/>.
    /// </summary>
    public static IServiceCollection AddReactor<TReactor>(this IServiceCollection services)
        where TReactor : class, IEntityReactor
        => services.WithRegistrations().AddTransient<IEntityReactor, TReactor>();
    /// <summary>Registers a reactor for the changes of <typeparamref name="TEntity"/>.</summary>
    public static IServiceCollection AddReactor<TEntity, TReactor>(this IServiceCollection services)
        where TReactor : class, IEntityReactor<TEntity>
        => services.WithRegistrations().AddTransient<IEntityReactor<TEntity>, TReactor>();
    /// <summary>
    /// Registers a reactor for the changes of <typeparamref name="TEntity"/>, created by <paramref name="factory"/> from
    /// the reaction's own DI scope.
    /// </summary>
    public static IServiceCollection AddReactor<TEntity>(this IServiceCollection services, Func<IServiceProvider, IEntityReactor<TEntity>> factory)
        => services.WithRegistrations().AddTransient(factory);

    private static IServiceCollection WithRegistrations(this IServiceCollection services)
    {
        services.TryAddSingleton(services);
        return services;
    }

    /// <summary>
    /// Registers a reactor for every entity it covers (see <see cref="AddReactor{TReactor}(IServiceCollection)"/>) — the
    /// global counterpart of the entity builder's <c>AddReactor&lt;TReactor&gt;()</c>, e.g. one reactor on a shared interface.
    /// </summary>
    public static EntityServiceCollectionOptions AddReactor<TReactor>(this EntityServiceCollectionOptions options)
        where TReactor : class, IEntityReactor
    {
        options.Services.AddReactor<TReactor>();
        return options;
    }
}
