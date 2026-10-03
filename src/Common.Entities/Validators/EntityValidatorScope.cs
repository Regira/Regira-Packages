using System.Collections.Concurrent;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validators;

/// <summary>
/// Decides whether a validator applies to an entity type. Shared by the runtime (<see cref="EntityValidatorExtensions.ValidateItem{TEntity}"/>,
/// <see cref="EntityValidatorExtensions.AnyApplyTo"/>) and the startup checks, so a check cannot vouch for a validator the
/// runtime skips.
/// </summary>
internal static class EntityValidatorScope
{
    private static readonly ConcurrentDictionary<Type, Type[]> Scopes = new();
    private static readonly ConcurrentDictionary<(Type Validator, Type Entity), bool> Matches = new();

    /// <summary>
    /// The <c>TScope</c> of every <see cref="IEntityValidator{TScope}"/> the validator implements. Empty for a validator
    /// implementing only <see cref="IEntityValidator"/>, which checks every entity.
    /// </summary>
    public static IReadOnlyList<Type> ScopesOf(Type validatorType)
        => Scopes.GetOrAdd(validatorType, static type => type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityValidator<>))
            .Select(i => i.GetGenericArguments()[0])
            .Distinct()
            .ToArray());

    /// <summary>
    /// A validator applies to <paramref name="entityType"/> when one of its scopes is that type, a type it derives from or
    /// an interface it implements (<see cref="EntityScopeTypes.Of"/>). Cached: registrations cannot change once the
    /// container is built.
    /// </summary>
    public static bool AppliesTo(Type validatorType, Type entityType)
        => Matches.GetOrAdd((validatorType, entityType), static key =>
        {
            var scopes = ScopesOf(key.Validator);
            if (scopes.Count == 0)
            {
                return true;
            }
            var entityScopes = EntityScopeTypes.Of(key.Entity);
            return scopes.Any(entityScopes.Contains);
        });

    /// <summary>
    /// Whether <paramref name="validator"/> runs for an item of <paramref name="itemType"/>: it applies to the type
    /// (<see cref="AppliesTo"/>) and, as an <see cref="ISelectiveEntityValidator"/>, covers it.
    /// </summary>
    public static bool RunsFor(IEntityValidator validator, Type itemType)
        => AppliesTo(validator.GetType(), itemType)
           && (validator is not ISelectiveEntityValidator selective || selective.Covers(itemType));
}
