using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.DependencyInjection.Reactors;

public static class ServiceCollectionReactorExtensions
{
    /// <summary>
    /// Registers a reactor that declares what it reacts to itself: every entity its <see cref="IEntityReactor{TEntity}"/>
    /// covers (an interface or base type reaches every entity implementing it), or every entity when it implements
    /// only <see cref="IEntityReactor"/>.
    /// </summary>
    public static IServiceCollection AddReactor<TReactor>(this IServiceCollection services)
        where TReactor : class, IEntityReactor
        => services.AddTransient<IEntityReactor, TReactor>();
    /// <summary>Registers a reactor for the changes of <typeparamref name="TEntity"/>.</summary>
    public static IServiceCollection AddReactor<TEntity, TReactor>(this IServiceCollection services)
        where TReactor : class, IEntityReactor<TEntity>
        => services.AddTransient<IEntityReactor<TEntity>, TReactor>();
    /// <summary>
    /// Registers a reactor for the changes of <typeparamref name="TEntity"/>, created by <paramref name="factory"/> from
    /// the reaction's own DI scope.
    /// </summary>
    public static IServiceCollection AddReactor<TEntity>(this IServiceCollection services, Func<IServiceProvider, IEntityReactor<TEntity>> factory)
        => services.AddTransient(factory);

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
