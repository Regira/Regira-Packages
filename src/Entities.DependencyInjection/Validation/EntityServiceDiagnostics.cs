using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Shared formatting for "entity service not registered" diagnostics: used by the request-time
/// <c>GetRequiredEntityService</c> helper (MVC controllers) and by the startup validators,
/// so a mismatch reads the same wherever it surfaces.
/// </summary>
public static class EntityServiceDiagnostics
{
    public static readonly IReadOnlySet<Type> EntityServiceOpenGenerics = new HashSet<Type>
    {
        typeof(IEntityService<>),
        typeof(IEntityService<,>),
        typeof(IEntityService<,,>),
        typeof(IEntityService<,,,>),
        typeof(IEntityService<,,,,>)
    };

    public static bool IsRegistered(IServiceCollection services, Type closedServiceType)
        => services.Any(d =>
            d.ServiceType == closedServiceType
            // an open-generic registration (services.AddTransient(typeof(IEntityService<,>), …)) serves
            // every closed variant — a legitimate config the validator must not flag
            || (d.ServiceType.IsGenericTypeDefinition && closedServiceType.IsGenericType
                && d.ServiceType == closedServiceType.GetGenericTypeDefinition()));

    /// <summary>
    /// Builds the explanatory message for a missing entity-service registration, listing the
    /// IEntityService arities that <em>are</em> registered for the entity so the mismatching
    /// <c>For&lt;&gt;()</c> call is easy to spot. Types read as written: <c>IEntityService&lt;Course, Int32&gt;</c>.
    /// </summary>
    public static string DescribeMissingService(Type requestedType, IServiceCollection? services)
    {
        var requested = EntityRepositoryRegistry.FriendlyName(requestedType);
        if (requestedType.IsGenericType && services != null)
        {
            var entityType = requestedType.GetGenericArguments()[0];
            var entity = EntityRepositoryRegistry.FriendlyName(entityType);

            var registered = services
                .Where(d =>
                    d.ServiceType.IsGenericType
                    && EntityServiceOpenGenerics.Contains(d.ServiceType.GetGenericTypeDefinition())
                    && d.ServiceType.GetGenericArguments()[0] == entityType)
                .Select(d => EntityRepositoryRegistry.FriendlyName(d.ServiceType))
                .Distinct()
                .ToList();

            if (registered.Count > 0)
            {
                return $"No service of type '{requested}' was registered. " +
                       $"The following IEntityService registrations exist for '{entity}': " +
                       string.Join(", ", registered) + ". " +
                       "Make sure all generic parameters in .For<>() exactly match what is being requested.";
            }

            return $"No service of type '{requested}' was registered, " +
                   $"and no entity services for '{entity}' were found. " +
                   $"Register it via .For<{entity}>() or an appropriate overload.";
        }

        return $"No service of type '{requested}' was registered. " +
               "Register entity services using .For<>() with matching generic type parameters.";
    }

    /// <summary>A generic type's arguments as written, <c>&lt;Course, SearchObject&lt;Int32&gt;&gt;</c>; empty for a non-generic type.</summary>
    public static string FormatTypeArgs(Type type) =>
        type.IsGenericType
            ? "<" + string.Join(", ", type.GetGenericArguments().Select(EntityRepositoryRegistry.FriendlyName)) + ">"
            : string.Empty;
}
