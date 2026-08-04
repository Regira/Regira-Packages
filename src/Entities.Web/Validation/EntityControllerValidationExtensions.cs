#if NETCOREAPP3_1_OR_GREATER
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;

namespace Regira.Entities.Web.Validation;

public static class EntityControllerValidationExtensions
{
    /// <summary>
    /// Registers the startup check that every <c>EntityControllerBase&lt;...&gt;</c> subclass has matching
    /// <c>For&lt;&gt;()</c> registrations (see <see cref="ControllerRegistrationValidator"/>). Also registered
    /// automatically by <c>UseEntities()</c>. Runs in Development by default.
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
        return services;
    }
}
#endif
