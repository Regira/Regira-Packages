using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Shared formatting for "entity service not registered" diagnostics: used by the request-time
/// <c>GetRequiredEntityService</c> helpers (MVC and FastEndpoints) and by the startup validators,
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
    /// <c>For&lt;&gt;()</c> call is easy to spot.
    /// </summary>
    public static string DescribeMissingService(Type requestedType, IServiceCollection? services)
    {
        if (requestedType.IsGenericType && services != null)
        {
            var entityType = requestedType.GetGenericArguments()[0];

            var registered = services
                .Where(d =>
                    d.ServiceType.IsGenericType
                    && EntityServiceOpenGenerics.Contains(d.ServiceType.GetGenericTypeDefinition())
                    && d.ServiceType.GetGenericArguments()[0] == entityType)
                .Select(d => d.ServiceType.Name + FormatTypeArgs(d.ServiceType))
                .Distinct()
                .ToList();

            if (registered.Count > 0)
            {
                return $"No service of type '{requestedType.Name}{FormatTypeArgs(requestedType)}' was registered. " +
                       $"The following IEntityService registrations exist for '{entityType.Name}': " +
                       string.Join(", ", registered) + ". " +
                       "Make sure all generic parameters in .For<>() exactly match what is being requested.";
            }

            return $"No service of type '{requestedType.Name}{FormatTypeArgs(requestedType)}' was registered, " +
                   $"and no entity services for '{entityType.Name}' were found. " +
                   $"Register it via .For<{entityType.Name}>() or an appropriate overload.";
        }

        return $"No service of type '{requestedType.Name}' was registered. " +
               "Register entity services using .For<>() with matching generic type parameters.";
    }

    public static string FormatTypeArgs(Type type) =>
        type.IsGenericType
            ? "<" + string.Join(", ", type.GetGenericArguments().Select(a => a.Name)) + ">"
            : string.Empty;
}
