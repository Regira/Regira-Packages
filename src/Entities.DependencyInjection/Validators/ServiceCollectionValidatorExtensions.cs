using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Validators;
using Regira.Entities.Validators;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.DependencyInjection.Validators;

/// <summary>
/// Registers <see cref="IEntityValidator"/>s. Every registration joins the one list every write service imports, so the
/// place of registration never narrows a validator's scope: a validator applies to every entity its
/// <see cref="IEntityValidator{TScope}"/> covers, whichever <c>For&lt;&gt;()</c> registered it.
/// </summary>
public static class ServiceCollectionValidatorExtensions
{
    /// <summary>
    /// Registers a validator class for every entity it is scoped to — an interface or base type reaches every entity
    /// implementing it — and as each <see cref="IEntityValidator{TScope}"/> it implements, for code that injects it directly.
    /// A class registered more than once runs once.
    /// </summary>
    public static IServiceCollection AddValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IEntityValidator
    {
        // TryAddEnumerable: the same interface-scoped validator added in several For<>() blocks must not report its errors twice
        services.TryAddEnumerable(ServiceDescriptor.Transient<IEntityValidator, TValidator>());
        foreach (var scope in EntityValidatorScope.ScopesOf(typeof(TValidator)))
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEntityValidator<>).MakeGenericType(scope), typeof(TValidator)));
        }
        return services;
    }
    /// <inheritdoc cref="AddValidator{TValidator}(IServiceCollection)"/>
    public static IServiceCollection AddValidator<TScope, TValidator>(this IServiceCollection services)
        where TValidator : class, IEntityValidator<TScope>
        => services.AddValidator<TValidator>();
    /// <summary>Registers a validator for <typeparamref name="TScope"/>, created by <paramref name="factory"/>.</summary>
    public static IServiceCollection AddValidator<TScope>(this IServiceCollection services, Func<IServiceProvider, IEntityValidator<TScope>> factory)
    {
        services.AddTransient<IEntityValidator>(factory);
        services.AddTransient(factory);
        return services;
    }
    /// <summary>
    /// Registers <paramref name="validate"/> as a validator for <typeparamref name="TScope"/> — the entity type, a base
    /// class or an interface. Each call adds a validator of its own.
    /// </summary>
    public static IServiceCollection AddValidator<TScope>(this IServiceCollection services, Action<IEntityValidatorContext<TScope>> validate)
        where TScope : class
        => services.AddTransient<IEntityValidator>(_ => new EntityValidator<TScope>(validate));
    /// <summary>
    /// Registers <paramref name="validate"/> as a validator for <typeparamref name="TScope"/>, receiving the request's
    /// <typeparamref name="TContext"/> — e.g. to check that a referenced row exists. Each call adds a validator of its own.
    /// </summary>
    public static IServiceCollection AddValidator<TContext, TScope>(this IServiceCollection services, Func<IEntityValidatorContext<TScope>, TContext, Task> validate)
        where TContext : DbContext
        where TScope : class
        => services.AddTransient<IEntityValidator>(p => new EntityValidator<TContext, TScope>(p.GetRequiredService<TContext>(), validate));
    /// <inheritdoc cref="AddValidator{TContext,TScope}(IServiceCollection,Func{IEntityValidatorContext{TScope},TContext,Task})"/>
    /// <remarks><paramref name="validate"/> receives the write's cancellation token, to pass to its queries.</remarks>
    public static IServiceCollection AddValidator<TContext, TScope>(this IServiceCollection services, Func<IEntityValidatorContext<TScope>, TContext, CancellationToken, Task> validate)
        where TContext : DbContext
        where TScope : class
        => services.AddTransient<IEntityValidator>(p => new EntityValidator<TContext, TScope>(p.GetRequiredService<TContext>(), validate));


    /// <summary>
    /// Registers a validator class globally — the place for one scoped to an interface or base type, e.g.
    /// <c>IEntityValidator&lt;IHasTenantId&gt;</c>. See <see cref="AddValidator{TValidator}(IServiceCollection)"/>.
    /// </summary>
    public static EntityServiceCollectionOptions AddValidator<TValidator>(this EntityServiceCollectionOptions options)
        where TValidator : class, IEntityValidator
    {
        options.Services.AddValidator<TValidator>();
        return options;
    }
    /// <summary>Registers <paramref name="validate"/> globally, for every entity in <typeparamref name="TScope"/>.</summary>
    public static EntityServiceCollectionOptions AddValidator<TScope>(this EntityServiceCollectionOptions options, Action<IEntityValidatorContext<TScope>> validate)
        where TScope : class
    {
        options.Services.AddValidator(validate);
        return options;
    }
    /// <summary>
    /// Registers <paramref name="validate"/> globally, for every entity in <typeparamref name="TScope"/>, receiving the
    /// request's <typeparamref name="TContext"/>.
    /// </summary>
    public static EntityServiceCollectionOptions AddValidator<TContext, TScope>(this EntityServiceCollectionOptions options, Func<IEntityValidatorContext<TScope>, TContext, Task> validate)
        where TContext : DbContext
        where TScope : class
    {
        options.Services.AddValidator(validate);
        return options;
    }
    /// <inheritdoc cref="AddValidator{TContext,TScope}(EntityServiceCollectionOptions,Func{IEntityValidatorContext{TScope},TContext,Task})"/>
    /// <remarks><paramref name="validate"/> receives the write's cancellation token, to pass to its queries.</remarks>
    public static EntityServiceCollectionOptions AddValidator<TContext, TScope>(this EntityServiceCollectionOptions options, Func<IEntityValidatorContext<TScope>, TContext, CancellationToken, Task> validate)
        where TContext : DbContext
        where TScope : class
    {
        options.Services.AddValidator(validate);
        return options;
    }
}
