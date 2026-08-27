using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Regira.Entities.DependencyInjection.Licensing;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Models;

namespace Regira.Entities.DependencyInjection.Extensions;

public static class ServiceCollectionExtensions
{
    public static EntityServiceCollectionOptions UseEntities(this IServiceCollection services, Action<EntityServiceCollectionOptions>? configure = null)
    {
        var options = new EntityServiceCollectionOptions(services);
        configure?.Invoke(options);
        // License validation is deferred: For<>() checks the limit once the free tier is exceeded
        services.TryAddSingleton(services);
        services.RegisterTierStartupLogger();
        services.RegisterGlobalPagingOptions(options);
        services.RegisterGlobalQueryOptions(options);
        services.RegisterGlobalReadOptions(options);
        services.RegisterStartupValidation(options);
        return options;
    }

    public static EntityServiceCollection<TContext> UseEntities<TContext>(this IServiceCollection services, Action<EntityServiceCollectionOptions>? configure = null)
        where TContext : DbContext
    {
        var options = new EntityServiceCollectionOptions(services);
        configure?.Invoke(options);
        // License validation is deferred: For<>() checks the limit once the free tier is exceeded
        services.TryAddSingleton(services);
        services.RegisterTierStartupLogger();
        services.RegisterGlobalPagingOptions(options);
        services.RegisterGlobalQueryOptions(options);
        services.RegisterGlobalReadOptions(options);
        services.RegisterStartupValidation(options);
        services.WireDbContextDefaults<TContext>(options);
        return new EntityServiceCollection<TContext>(options);
    }

    /// <summary>
    /// Records the selected DbContext plumbing for <typeparamref name="TContext"/>
    /// (see <see cref="EntityServiceCollectionOptions.DbContextWiring"/> — everything with <c>UseDefaults()</c>,
    /// à la carte via <c>WireDbContext(...)</c>) and registers the open-generic
    /// <see cref="EntityDbContextOptionsConfiguration{TContext}"/> that applies it at options-build time,
    /// so the app's <c>AddDbContext</c> only needs the provider. The assignability match in
    /// <see cref="DbContextWiringRegistry"/> also covers concrete contexts registered through an abstract base
    /// (<c>UseEntities&lt;AppContextBase&gt;()</c> + <c>AddDbContext&lt;SqlServerAppContext&gt;()</c>),
    /// independent of call order. Wiring is idempotent, so repeated <c>UseEntities()</c> calls
    /// (or standalone DAL extensions) never double-configure.
    /// </summary>
    private static void WireDbContextDefaults<TContext>(this IServiceCollection services, EntityServiceCollectionOptions options)
        where TContext : DbContext
    {
        DbContextWiringRegistry.For(services).Add(typeof(TContext), options.DbContextWiring);
        services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IDbContextOptionsConfiguration<>), typeof(EntityDbContextOptionsConfiguration<>)));
    }

    /// <summary>
    /// Registers the per-collection <see cref="EntityLicenseValidator"/> as a singleton and the
    /// <see cref="EntityLicenseStartupLogger"/> hosted service so the tier/registration tally is logged
    /// exactly once at host start. <c>TryAdd*</c> keeps repeated <c>UseEntities()</c> calls from double-logging.
    /// </summary>
    private static void RegisterTierStartupLogger(this IServiceCollection services)
    {
        services.TryAddSingleton(EntityLicenseValidator.For(services));
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EntityLicenseStartupLogger>());
    }

    /// <summary>
    /// Registers the global <see cref="EntityListOptions"/> from the configured paging defaults.
    /// Across multiple <c>UseEntities()</c> calls the last call that explicitly configured paging wins
    /// (so a later <c>SetPageSize()</c> opt-out is not swallowed by an earlier registration). Calls that
    /// never touched paging only seed a default when none exists yet, and never overwrite an existing one.
    /// </summary>
    private static void RegisterGlobalPagingOptions(this IServiceCollection services, EntityServiceCollectionOptions options)
    {
        if (options.PageSizeConfigured)
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(EntityListOptions));
            if (existing != null)
            {
                services.Remove(existing);
            }
            services.AddSingleton(new EntityListOptions { DefaultPageSize = options.DefaultPageSize, MaxPageSize = options.MaxPageSize });
        }
        else
        {
            services.TryAddSingleton(new EntityListOptions { DefaultPageSize = options.DefaultPageSize, MaxPageSize = options.MaxPageSize });
        }
    }

    /// <summary>
    /// Registers the global <see cref="EntityQueryOptions"/> from the configured query behavior
    /// (<see cref="EntityServiceCollectionOptions.DefaultArchivedFilter"/>).
    /// Same last-explicit-configuration-wins semantics as <see cref="RegisterGlobalPagingOptions"/>.
    /// </summary>
    private static void RegisterGlobalQueryOptions(this IServiceCollection services, EntityServiceCollectionOptions options)
    {
        if (options.QueryBehaviorConfigured)
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(EntityQueryOptions));
            if (existing != null)
            {
                services.Remove(existing);
            }
            services.AddSingleton(new EntityQueryOptions { DefaultArchivedFilter = options.DefaultArchivedFilter });
        }
        else
        {
            services.TryAddSingleton(new EntityQueryOptions { DefaultArchivedFilter = options.DefaultArchivedFilter });
        }
    }

    /// <summary>
    /// Registers the global <see cref="EntityReadOptions"/> from the configured read behavior
    /// (<see cref="EntityServiceCollectionOptions.RefetchAfterSave"/>).
    /// Same last-explicit-configuration-wins semantics as <see cref="RegisterGlobalPagingOptions"/>.
    /// </summary>
    private static void RegisterGlobalReadOptions(this IServiceCollection services, EntityServiceCollectionOptions options)
    {
        if (options.ReadBehaviorConfigured)
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(EntityReadOptions));
            if (existing != null)
            {
                services.Remove(existing);
            }
            services.AddSingleton(new EntityReadOptions { RefetchAfterSave = options.RefetchAfterSave });
        }
        else
        {
            services.TryAddSingleton(new EntityReadOptions { RefetchAfterSave = options.RefetchAfterSave });
        }
    }

    /// <summary>
    /// Registers the startup validation of entity registrations: the hosted service, the built-in
    /// validators (interceptor wiring, ignored <c>?q=</c> input, competing write authorities, out-of-scope
    /// global filters, missing archived query filter, archivable reference data behind a required FK,
    /// attachments owners whose input DTO cannot carry the collection, unenforceable <c>[ServerOwned]</c>
    /// declarations) and the
    /// <see cref="EntityValidationOptions"/>.
    /// Runs in Development by default; see <see cref="EntityServiceCollectionOptions.ConfigureValidation"/>.
    /// </summary>
    private static void RegisterStartupValidation(this IServiceCollection services, EntityServiceCollectionOptions options)
    {
        EntityRegistrationLog.For(services);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, EntityValidationStartupService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, InterceptorWiringValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, QSearchValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, WriteAuthorityValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, GlobalFilterScopeValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, ArchivedQueryFilterValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, ArchivableReferenceDataValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, AttachmentsInputDtoValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, ServerOwnedValidator>());

        // The controller validator lives in Regira.Entities.Web (it needs MVC types), which this project
        // cannot reference — bind it late so validation is enabled by UseEntities() itself rather than by
        // an unrelated JSON-options call. No-ops for apps that don't reference the web package.
        var controllerValidator = Type.GetType(
            "Regira.Entities.Web.Validation.ControllerRegistrationValidator, Regira.Entities.Web", throwOnError: false);
        if (controllerValidator != null)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(typeof(IEntityRegistrationValidator), controllerValidator));
        }

        if (options.ValidationConfigured)
        {
            var existing = services.FirstOrDefault(d => d.ServiceType == typeof(EntityValidationOptions));
            if (existing != null)
            {
                services.Remove(existing);
            }
            services.AddSingleton(options.Validation);
        }
        else
        {
            services.TryAddSingleton(options.Validation);
        }
    }

    public static IServiceCollection GetServices<TContext>(this IEntityServiceCollection<TContext> entityServiceCollection)
        where TContext : DbContext
    {
        return entityServiceCollection.Services;
    }
}