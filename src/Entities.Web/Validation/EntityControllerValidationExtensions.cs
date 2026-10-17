#if NETCOREAPP3_1_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Web.Endpoints;

namespace Regira.Entities.Web.Validation;

public static class EntityControllerValidationExtensions
{
    /// <summary>
    /// Registers the startup check that every <c>EntityControllerBase&lt;...&gt;</c> subclass has matching
    /// <c>For&lt;&gt;()</c> registrations (see <see cref="ControllerRegistrationValidator"/>), and hands the
    /// controllers' DTOs to the checks that judge DTOs (<see cref="ControllerDtoShapeSource"/>) — and the same for the
    /// mapped entity endpoints (<c>MapEntityEndpoints()</c>): what they mapped, their DTOs, an entity a controller serves
    /// too and attachment downloads without <c>UseAttachmentUris()</c>. Also registered automatically by <c>UseEntities()</c>.
    /// Runs in Development by default.
    /// </summary>
    public static EntityServiceCollectionOptions ValidateEntityControllers(this EntityServiceCollectionOptions options)
    {
        options.Services.ValidateEntityControllers();
        return options;
    }

    /// <inheritdoc cref="ValidateEntityControllers(EntityServiceCollectionOptions)"/>
    public static IServiceCollection ValidateEntityControllers(this IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, ControllerRegistrationValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityDtoShapeSource, ControllerDtoShapeSource>());
        // what MapEntityEndpoints() maps is recorded here, at startup, for the checks below
        services.TryAddSingleton<EntityEndpointRegistry>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityRegistrationValidator, EntityEndpointValidator>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IEntityDtoShapeSource, EntityEndpointDtoShapeSource>());
        return services;
    }
}
#endif
