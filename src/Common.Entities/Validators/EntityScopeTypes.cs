using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using Regira.Utilities;

namespace Regira.Entities.Validators;

/// <summary>
/// The types an entity is in scope of: the entity type itself, then every type it derives from or implements. A hook
/// scoped to any of them — a validator, a global filter — applies to the entity.
/// </summary>
public static class EntityScopeTypes
{
    private static readonly ConcurrentDictionary<Type, ReadOnlyCollection<Type>> Cache = new();

    /// <summary>
    /// <paramref name="entityType"/> first, then its base types and interfaces. The type itself is included explicitly —
    /// <see cref="TypeUtility.GetBaseTypes"/> returns only what a type derives from — so that a hook scoped to the
    /// concrete entity is matched rather than skipped.
    /// </summary>
    public static IReadOnlyList<Type> Of(Type entityType)
        => Cache.GetOrAdd(entityType, static type => new[] { type }.Concat(TypeUtility.GetBaseTypes(type)).Distinct().ToArray().AsReadOnly());
}
