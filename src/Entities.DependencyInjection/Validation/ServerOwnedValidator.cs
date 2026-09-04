using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attributes;
using Regira.Entities.Preppers;
using Regira.Entities.Preppers.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports <see cref="ServerOwnedAttribute"/> declarations that do not do what they read like: a property
/// that cannot be server-owned (the soft-delete flag, a navigation, a property without both accessors), and
/// an entity whose own write service has no enforcement registered. The enforcing prepper skips exactly
/// what this reports, so a declaration is ignored rather than half-applied.
/// </summary>
internal sealed class ServerOwnedValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var registeredTypes = context.Registrations.Entities.Select(e => e.EntityType).Distinct().ToArray();
        var declaringTypes = registeredTypes
            .Concat(context.Registrations.Related.Select(r => r.RelatedType))
            .Distinct()
            .Where(t => ServerOwnedProperties.Declared(t).Count > 0)
            .ToArray();
        if (declaringTypes.Length == 0)
        {
            yield break;
        }

        foreach (var entityType in declaringTypes)
        {
            foreach (var property in ServerOwnedProperties.Declared(entityType))
            {
                if (ServerOwnedProperties.SkipReason(entityType, property) is not { } reason)
                {
                    continue;
                }

                var action = ServerOwnedProperties.IsArchivedFlag(entityType, property)
                    ? $"ACTION: remove [ServerOwned] from {entityType.Name}.{property.Name} and keep the flag on TInputDto — that is what makes a restore expressible. " +
                      "To keep clients from archiving through a plain update, gate the transition (a role-gated PATCH endpoint), don't freeze the column."
                    : $"ACTION: remove [ServerOwned] from {entityType.Name}.{property.Name}. " +
                      "An owned child collection is governed by the parent's Related() sync; anything else needing the stored row belongs in a prepper (e.AddPrepper<T>()).";

                yield return new EntityValidationIssue(EntityValidationSeverity.Error,
                    $"{entityType.Name}.{property.Name} is marked [ServerOwned] but cannot be server-owned: {reason}. " +
                    $"The attribute is ignored on this property, so it stays as writable as it was. {action} " +
                    "See entities.patterns → Server-owned / immutable fields on update.");
            }
        }

        // Only an entity with a write service of its own depends on the global registration: a Related()
        // child is prepped through its parent's chain, which carries the restore unconditionally, so warning
        // about one would report an enforced field as unenforced.
        var unenforced = declaringTypes
            .Where(t => registeredTypes.Contains(t) && ServerOwnedProperties.Protected(t).Count > 0)
            .ToArray();
        if (unenforced.Length > 0 && !IsEnforcementRegistered(context.Services))
        {
            var names = string.Join(", ", unenforced.Select(t => t.Name).Order());
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{names} declare [ServerOwned] properties, but no AutoServerOwnedPrepper is registered, so nothing enforces them: " +
                "a PUT/PATCH that omits one of those fields still writes it back as null/default, returns 200, and logs nothing. " +
                "ACTION: register the entities with UseEntities<TContext>(o => o.UseDefaults()) — or, for an à-la-carte setup, " +
                "add o.AddDefaultPreppers(). See entities.patterns → Server-owned / immutable fields on update.");
        }
    }

    // Matched on the implementation type: a factory registration cannot be identified without instantiating it.
    private static bool IsEnforcementRegistered(IServiceCollection services)
        => services.Any(d => d.ServiceType == typeof(IEntityPrepper)
                             && d.ImplementationType != null
                             && DerivesFromAutoServerOwned(d.ImplementationType));

    private static bool DerivesFromAutoServerOwned(Type type)
    {
        for (var current = type; current != null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(AutoServerOwnedPrepper<>))
            {
                return true;
            }
        }
        return false;
    }
}
