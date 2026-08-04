using Microsoft.Extensions.Logging;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders;

/// <summary>
/// Chooses which registered global filters to apply for a given entity + search-object key type.
/// The framework's default filters are int-keyed, but a non-int entity may also have <c>&lt;TKey&gt;</c>
/// variants registered. Running every applicable variant would double-apply (e.g. the int archive filter
/// hides archived rows via its key-agnostic default while the matching-key variant honours
/// <c>Archived=Only</c>, yielding an empty result). So per filter <em>family</em> we run exactly one
/// variant: the one whose key type matches the search object when present, otherwise the family's default
/// (which still applies its key-agnostic behaviour — soft-delete, timestamps, Q — and null-coerces only
/// the keyed fields). This keeps soft-delete safe even when only the int variant is registered.
/// <para>
/// A family is (concrete type definition + the filtered entity-scope <c>TEntity</c>). Deduping the key
/// variants of one filter (<c>FilterArchivablesQueryBuilder</c> vs <c>&lt;Guid&gt;</c> — both scope
/// <c>IArchivable</c>) is intended; two closed instantiations of a generic filter that scope DIFFERENT
/// entity interfaces (<c>AccessFilter&lt;IHasOwner&gt;</c> vs <c>AccessFilter&lt;IHasDepartment&gt;</c>)
/// are distinct filters — collapsing them would silently drop a row-scoping/security filter.
/// </para>
/// </summary>
internal static class GlobalFilterSelector
{
    public static IReadOnlyList<IGlobalFilteredQueryBuilder> Select<TKey>(IEnumerable<IGlobalFilteredQueryBuilder> applicableFilters, ILogger? logger = null)
        => applicableFilters
            .GroupBy(FamilyKey)
            .Select(family =>
            {
                // >1 distinct concrete type accepting the SAME key means the runner-up can never run for
                // this key — a variant for another key is the designed dedupe and stays silent.
                var sameKey = family.Where(AcceptsKey<TKey>).Select(f => f.GetType()).Distinct().ToArray();
                if (sameKey.Length > 1)
                    logger?.LogWarning("Global filter family {Family} has {Count} filters accepting key {Key}; only {Winner} runs — split the runner-up into its own family (derive it from the abstract base, not from another concrete filter)",
                        family.Key, sameKey.Length, typeof(TKey).Name, sameKey[0].Name);
                return family.FirstOrDefault(AcceptsKey<TKey>) ?? family.First();
            })
            .ToArray();

    // Family = (concrete type definition, filtered entity scope). Both are needed: the type definition
    // alone collapses distinct scopes of one generic filter (the security leak); the entity scope alone
    // collapses two different filter classes that happen to scope the same interface.
    private static (Type Definition, Type? Scope) FamilyKey(IGlobalFilteredQueryBuilder filter)
        => (Family(filter.GetType()), EntityScope(filter));

    // The first CONCRETE generic type definition up the inheritance chain identifies a filter "family"
    // (e.g. FilterArchivablesQueryBuilder and FilterArchivablesQueryBuilder&lt;Guid&gt; share one).
    // Abstract bases (GlobalFilteredQueryBuilderBase&lt;,&gt;, FilteredQueryBuilderBase&lt;,,&gt;) are shared
    // infrastructure, never a family identity — a concrete filter deriving from them directly is its own
    // family, so two distinct custom filters registered per the documented pattern never dedupe each other.
    private static Type Family(Type filterType)
    {
        for (var t = filterType; t != null && t != typeof(object); t = t.BaseType)
        {
            if (t.IsGenericType && !t.IsAbstract)
            {
                return t.GetGenericTypeDefinition();
            }
        }
        return filterType;
    }

    // The TEntity the filter scopes, read from its IGlobalFilteredQueryBuilder<TEntity, TKey> interface.
    // The key variants of one filter (FilterArchivablesQueryBuilder<int>/<Guid>) share TEntity (IArchivable),
    // so they dedupe; AccessFilter<IHasOwner> and AccessFilter<IHasDepartment> have distinct TEntity, so
    // they don't. (FilterIdsQueryBuilder's TEntity is IEntity<TKey> and thus differs per key — but its key
    // variants are never both applicable to one entity, so the distinction is moot there.)
    private static Type? EntityScope(IGlobalFilteredQueryBuilder filter)
        => filter.GetType().GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IGlobalFilteredQueryBuilder<,>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();

    private static bool AcceptsKey<TKey>(IGlobalFilteredQueryBuilder filter)
        => filter.GetType().GetInterfaces().Any(i =>
            i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(IGlobalFilteredQueryBuilder<,>)
            && i.GetGenericArguments()[1] == typeof(TKey));
}
