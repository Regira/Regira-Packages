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
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TValidator"/> derives from <see cref="EntityValidatorBase{TScope}"/> and implements
    /// <see cref="IEntityValidator{TScope}"/> for another scope too, which the base class never runs.
    /// </exception>
    public static IServiceCollection AddValidator<TValidator>(this IServiceCollection services)
        where TValidator : class, IEntityValidator
    {
        EnsureEveryScopeRuns(typeof(TValidator));
        // TryAddEnumerable: the same interface-scoped validator added in several For<>() blocks must not report its errors twice
        services.TryAddEnumerable(ServiceDescriptor.Transient<IEntityValidator, TValidator>());
        foreach (var scope in EntityValidatorScope.ScopesOf(typeof(TValidator)))
        {
            services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IEntityValidator<>).MakeGenericType(scope), typeof(TValidator)));
        }
        return services;
    }
    /// <summary>
    /// <see cref="EntityValidatorBase{TScope}"/> implements the untyped <see cref="IEntityValidator.Validate"/> the pipeline
    /// calls by casting to its own scope: a subclass that adds a second <see cref="IEntityValidator{TScope}"/> would see that
    /// scope's <c>Validate</c> never run, and an item outside the base scope fail the cast. Unless the class implements the
    /// untyped method itself, it is refused here rather than at its first write.
    /// </summary>
    private static void EnsureEveryScopeRuns(Type validatorType)
    {
        var baseScope = BaseScopeOf(validatorType);
        if (baseScope == null)
        {
            return;
        }
        var otherScopes = EntityValidatorScope.ScopesOf(validatorType).Where(scope => scope != baseScope).ToArray();
        var dispatcher = validatorType.GetInterfaceMap(typeof(IEntityValidator)).TargetMethods.Single().DeclaringType;
        if (otherScopes.Length == 0 || dispatcher is not { IsGenericType: true } || dispatcher.GetGenericTypeDefinition() != typeof(EntityValidatorBase<>))
        {
            return;
        }
        throw new ArgumentException(
            $"{validatorType.Name} derives from EntityValidatorBase<{baseScope.Name}> and also implements " +
            $"{string.Join(", ", otherScopes.Select(scope => $"IEntityValidator<{scope.Name}>"))}. The base class runs only its own " +
            $"Validate, for items it can cast to {baseScope.Name}, so the other scope's Validate would never run. Split it into one " +
            "validator per scope, or implement IEntityValidator<TScope> for each scope and IEntityValidator.Validate directly.",
            nameof(validatorType));
    }
    private static Type? BaseScopeOf(Type validatorType)
    {
        for (var type = validatorType.BaseType; type != null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EntityValidatorBase<>))
            {
                return type.GetGenericArguments()[0];
            }
        }
        return null;
    }
    /// <summary>
    /// Registers <paramref name="validate"/> as a validator for <typeparamref name="TScope"/> — the entity type, a base
    /// class or an interface. Each call adds a validator of its own.
    /// </summary>
    public static IServiceCollection AddValidator<TScope>(this IServiceCollection services, Action<IEntityValidatorContext<TScope>> validate)
        where TScope : class
        => services.AddTransient<IEntityValidator>(_ => new EntityValidator<TScope>(validate));
    /// <inheritdoc cref="AddValidator{TScope}(IServiceCollection,Action{IEntityValidatorContext{TScope}})"/>
    /// <remarks>An <c>async</c> delegate binds here, and the write awaits it.</remarks>
    public static IServiceCollection AddValidator<TScope>(this IServiceCollection services, Func<IEntityValidatorContext<TScope>, Task> validate)
        where TScope : class
        => services.AddTransient<IEntityValidator>(_ => new EntityValidator<TScope>(validate));
    /// <summary>
    /// Registers <paramref name="validate"/> as a validator for <typeparamref name="TScope"/>, receiving the request's
    /// <typeparamref name="TContext"/> — e.g. to check that a referenced row exists. Each call adds a validator of its own.
    /// The context is unfiltered — global filters (tenant, owner) do not apply to it — so check a row-secured reference
    /// through <c>IEntityReadService&lt;,&gt;</c> in a validator class instead. <paramref name="validate"/> receives the
    /// write's cancellation token, to pass to its queries.
    /// </summary>
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
    /// <inheritdoc cref="AddValidator{TScope}(EntityServiceCollectionOptions,Action{IEntityValidatorContext{TScope}})"/>
    /// <remarks>An <c>async</c> delegate binds here, and the write awaits it.</remarks>
    public static EntityServiceCollectionOptions AddValidator<TScope>(this EntityServiceCollectionOptions options, Func<IEntityValidatorContext<TScope>, Task> validate)
        where TScope : class
    {
        options.Services.AddValidator<TScope>(validate);
        return options;
    }
    /// <summary>
    /// Registers <paramref name="validate"/> globally, for every entity in <typeparamref name="TScope"/>, receiving the
    /// request's <typeparamref name="TContext"/> and the write's cancellation token. The context is unfiltered, as it is for
    /// <see cref="AddValidator{TContext,TScope}(IServiceCollection,Func{IEntityValidatorContext{TScope},TContext,CancellationToken,Task})"/>.
    /// </summary>
    public static EntityServiceCollectionOptions AddValidator<TContext, TScope>(this EntityServiceCollectionOptions options, Func<IEntityValidatorContext<TScope>, TContext, CancellationToken, Task> validate)
        where TContext : DbContext
        where TScope : class
    {
        options.Services.AddValidator(validate);
        return options;
    }
}
