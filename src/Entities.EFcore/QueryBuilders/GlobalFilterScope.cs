using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.QueryBuilders;

/// <summary>
/// Decides whether a registered global filter applies to a given entity type.
/// <para>
/// Shared deliberately by the runtime (<see cref="QueryBuilder{TEntity,TKey,TSearchObject,TSortBy,TIncludes}"/>)
/// and by the startup validator that reports inert filters: one definition keeps the two in agreement, so
/// the validator cannot vouch for a filter that never runs.
/// </para>
/// </summary>
internal static class GlobalFilterScope
{
    /// <summary>
    /// A filter applies to <paramref name="entityType"/> when any type in its own hierarchy carries a
    /// generic argument that the entity satisfies — either an interface/base type the entity implements,
    /// or the entity type itself (a filter may scope the concrete entity, not only an interface).
    /// </summary>
    internal static bool AppliesTo(Type filterType, Type entityType)
    {
        var entityTypes = EntityScopeTypes(entityType);
        var filterTypes = TypeUtility.GetBaseTypes(filterType).Concat([filterType]).Distinct();
        return filterTypes.Any(ft => entityTypes.Any(et => TypeUtility.HasGenericArgument(ft, et)));
    }

    /// <inheritdoc cref="AppliesTo(Type,Type)"/>
    internal static bool AppliesTo(IGlobalFilteredQueryBuilder filter, Type entityType)
        => AppliesTo(filter.GetType(), entityType);

    /// <summary>
    /// The entity's base types and interfaces, plus the entity type itself. The entity must be included
    /// explicitly — <see cref="TypeUtility.GetBaseTypes"/> returns only what a type derives from, never the
    /// type itself — so that a filter scoped to the concrete entity is matched rather than skipped.
    /// </summary>
    internal static Type[] EntityScopeTypes(Type entityType)
        => TypeUtility.GetBaseTypes(entityType).Concat([entityType]).Distinct().ToArray();
}
