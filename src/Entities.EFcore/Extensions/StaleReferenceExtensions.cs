using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Keeps a foreign-key change from being undone by the loaded graph that still ties the entity to its old
/// principal.
/// <para>
/// An update attaches the incoming entity's whole graph, and EF's attach fixup lets a loaded relationship
/// overwrite a changed foreign key: a reference navigation still pointing at the old principal copies that
/// principal's key back, and so does the old principal's inverse collection when the principal is still in the
/// graph (the assignee who also authored one of the entity's children). An entity read with its includes
/// (<c>Details(id)</c>) and then given a new <c>AssigneeId</c> therefore saves the old one — no exception, no
/// warning. Before anything is attached, a reference navigation still on the stored principal is dropped when its
/// foreign key was set to a different key, and wherever the relationship moved away from the stored principal,
/// the entity is taken out of every loaded instance of that principal.
/// </para>
/// <para>
/// A collection of an entity being saved is never touched: a <c>Related()</c> sync reads a row missing from it as a
/// delete. A row listed there therefore stays with that entity, whatever its own foreign key says — a child moves
/// to another parent through the collections, not through its key.
/// </para>
/// <para>
/// A foreign key that is <b>absent</b> (<c>null</c>, or the property's sentinel) beside a navigation is left to
/// the navigation, as EF would: that is also the shape of a request body that sends only the nested object, and
/// such a client must keep its relation. Clearing a relation in code therefore clears the navigation too. A
/// navigation the caller re-pointed decides the key.
/// </para>
/// </summary>
internal static class StaleReferenceExtensions
{
    /// <summary>
    /// The graph-wide pass: <paramref name="incoming"/> and every entity its loaded collections reach that
    /// <paramref name="stored"/>'s collections hold under the same key, each checked against every loaded instance
    /// of its old principals. Call it before anything attaches part of the graph: attaching one child reaches its
    /// siblings through the parent, and their fixup runs before each sibling's own turn.
    /// </summary>
    internal static void DropStaleReferencesInGraph(this DbContext dbContext, object incoming, object stored)
    {
        // pair first, drop second: a row may only leave a collection once it is known that no sync reads that
        // collection — and the walk only learns that as it goes
        var pairs = new List<(object Incoming, object Stored)>();
        var synced = new HashSet<(object Owner, INavigation Navigation)>(OwnerNavigationComparer.Instance);
        Pair(dbContext, incoming, stored, pairs, synced, new HashSet<object>(ReferenceEqualityComparer.Instance));

        var graph = CollectGraph(dbContext, incoming);
        foreach (var (pairedIncoming, pairedStored) in pairs)
        {
            Drop(dbContext, pairedIncoming, pairedStored, graph, synced);
        }
    }

    /// <summary>
    /// The single-entity pass, for a caller that tracks one entity of a graph at a time (a <c>Related()</c> sync):
    /// drops a stale reference navigation only. It takes nothing out of a collection — the one the caller is
    /// syncing may be among them — and leaves the rest of the graph to <see cref="DropStaleReferencesInGraph"/>.
    /// </summary>
    internal static void DropStaleReferences(this DbContext dbContext, object incoming, object stored)
        => Drop(dbContext, incoming, stored, graph: null, synced: null);

    /// <summary>
    /// Collects <paramref name="incoming"/> and, recursively, every item of its loaded collections that
    /// <paramref name="stored"/>'s matching collection holds under the same key.
    /// </summary>
    /// <param name="dbContext">The context whose model describes the graph.</param>
    /// <param name="incoming">The entity about to be attached.</param>
    /// <param name="stored">Its stored counterpart.</param>
    /// <param name="pairs">Receives every incoming entity with its stored counterpart.</param>
    /// <param name="synced">Receives every collection walked here — the collections a sync may read.</param>
    /// <param name="visited">The incoming entities already paired, so a cycle ends.</param>
    private static void Pair(DbContext dbContext, object incoming, object stored, List<(object, object)> pairs,
        HashSet<(object, INavigation)> synced, HashSet<object> visited)
    {
        if (!visited.Add(incoming))
        {
            return;
        }
        pairs.Add((incoming, stored));

        var entityType = dbContext.Model.FindRuntimeEntityType(incoming.GetType());
        foreach (var navigation in entityType?.GetNavigations().Where(n => n.IsCollection) ?? [])
        {
            var key = navigation.TargetEntityType.FindPrimaryKey();
            if (key == null
                || ReadClrValue(navigation, incoming) is not IEnumerable<object> incomingItems
                || ReadClrValue(navigation, stored) is not IEnumerable<object> storedItems)
            {
                continue;
            }
            synced.Add((incoming, navigation));

            var storedByKey = new Dictionary<object?[], object>(new KeyComparer(key.Properties));
            foreach (var storedItem in storedItems)
            {
                storedByKey.TryAdd(KeyOf(key.Properties, storedItem), storedItem);
            }
            foreach (var item in incomingItems.ToList())
            {
                var itemKey = KeyOf(key.Properties, item);
                if (!IsAbsent(key.Properties, itemKey) && storedByKey.TryGetValue(itemKey, out var original))
                {
                    Pair(dbContext, item, original, pairs, synced, visited);
                }
            }
        }
    }

    private static void Drop(DbContext dbContext, object incoming, object stored, IReadOnlyList<object>? graph,
        HashSet<(object, INavigation)>? synced)
    {
        var entityType = dbContext.Model.FindRuntimeEntityType(incoming.GetType());
        if (entityType == null)
        {
            return;
        }

        foreach (var navigation in entityType.GetNavigations())
        {
            if (navigation.IsCollection || !navigation.IsOnDependent)
            {
                continue;
            }

            var foreignKey = navigation.ForeignKey;
            // a shadow foreign key has no CLR member a caller could have changed
            if (foreignKey.Properties.Any(p => p.PropertyInfo == null && p.FieldInfo == null))
            {
                continue;
            }

            var storedKey = KeyOf(foreignKey.Properties, stored);
            if (IsAbsent(foreignKey.Properties, storedKey))
            {
                continue; // the stored row had no principal to hold on to
            }

            var incomingKey = KeyOf(foreignKey.Properties, incoming);
            // an absent key beside a navigation is a body that sent only the nested object: the navigation decides
            var keyAbsent = IsAbsent(foreignKey.Properties, incomingKey);
            var principal = ReadClrValue(navigation, incoming);

            if (principal != null
                && !keyAbsent
                && !SameKey(foreignKey.Properties, incomingKey, storedKey)
                && SameKey(foreignKey.PrincipalKey.Properties, KeyOf(foreignKey.PrincipalKey.Properties, principal), storedKey))
            {
                WriteClrValue(navigation, incoming, null);
                principal = null;
            }

            if (graph == null || synced == null)
            {
                continue;
            }

            var current = principal != null ? KeyOf(foreignKey.PrincipalKey.Properties, principal) : keyAbsent ? null : incomingKey;
            if (current != null && SameKey(foreignKey.PrincipalKey.Properties, current, storedKey))
            {
                continue; // still related to the stored principal
            }

            var oldPrincipals = graph.Where(e => foreignKey.PrincipalEntityType.ClrType.IsInstanceOfType(e)
                                                 && !ReferenceEquals(e, principal)
                                                 && SameKey(foreignKey.PrincipalKey.Properties, KeyOf(foreignKey.PrincipalKey.Properties, e), storedKey));
            foreach (var oldPrincipal in oldPrincipals.ToList())
            {
                DetachFromInverse(navigation, oldPrincipal, incoming, synced);
            }
        }
    }

    /// <summary>
    /// Takes <paramref name="dependent"/> out of <paramref name="principal"/>'s side of the relationship: a loaded
    /// inverse collection (or one-to-one reference) that still holds the dependent makes the attach write that
    /// principal's key back. A collection a sync reads is left alone — there, a missing row is a delete.
    /// </summary>
    private static void DetachFromInverse(INavigation navigation, object principal, object dependent,
        HashSet<(object, INavigation)> synced)
    {
        var inverse = navigation.Inverse;
        if (inverse == null)
        {
            return;
        }

        if (!inverse.IsCollection)
        {
            if (ReferenceEquals(ReadClrValue(inverse, principal), dependent))
            {
                WriteClrValue(inverse, principal, null);
            }
            return;
        }

        if (!synced.Contains((principal, inverse))
            && ReadClrValue(inverse, principal) is IEnumerable<object> items
            && items.Any(i => ReferenceEquals(i, dependent)))
        {
            inverse.GetCollectionAccessor()?.Remove(principal, dependent);
        }
    }

    /// <summary>Every entity reachable from <paramref name="root"/> through loaded navigations, the root included.</summary>
    private static List<object> CollectGraph(DbContext dbContext, object root)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var pending = new Stack<object>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var entity = pending.Pop();
            var entityType = seen.Add(entity) ? dbContext.Model.FindRuntimeEntityType(entity.GetType()) : null;
            if (entityType == null)
            {
                continue;
            }

            var navigations = entityType.GetNavigations().Cast<INavigationBase>().Concat(entityType.GetSkipNavigations());
            foreach (var navigation in navigations)
            {
                var value = ReadClrValue(navigation, entity);
                if (!navigation.IsCollection)
                {
                    if (value != null) pending.Push(value);
                }
                else if (value is IEnumerable<object> items)
                {
                    foreach (var item in items.Where(i => i != null)) pending.Push(item);
                }
            }
        }
        return seen.ToList();
    }

    private static object?[] KeyOf(IReadOnlyList<IProperty> properties, object entity)
        => properties.Select(p => ReadClrValue(p, entity)).ToArray();

    private static bool SameKey(IReadOnlyList<IProperty> properties, object?[] left, object?[] right)
        => properties.Select((p, i) => p.GetKeyValueComparer().Equals(left[i], right[i])).All(same => same);

    /// <summary>A key with any part <c>null</c> or at its property's sentinel identifies no row.</summary>
    private static bool IsAbsent(IReadOnlyList<IProperty> properties, object?[] values)
        => properties.Select((p, i) => values[i] == null || p.GetKeyValueComparer().Equals(values[i], p.Sentinel)).Any(absent => absent);

    private static object? ReadClrValue(IPropertyBase property, object entity)
        => property.PropertyInfo?.GetMethod != null
            ? property.PropertyInfo.GetValue(entity)
            : property.FieldInfo?.GetValue(entity);

    private static void WriteClrValue(IPropertyBase property, object entity, object? value)
    {
        if (property.PropertyInfo?.SetMethod != null)
        {
            property.PropertyInfo.SetValue(entity, value);
        }
        else
        {
            property.FieldInfo?.SetValue(entity, value);
        }
    }

    /// <summary>Key values compared the way EF compares them, so a dictionary finds a stored row in one lookup.</summary>
    private sealed class KeyComparer(IReadOnlyList<IProperty> properties) : IEqualityComparer<object?[]>
    {
        public bool Equals(object?[]? x, object?[]? y)
            => x != null && y != null && SameKey(properties, x, y);

        public int GetHashCode(object?[] values)
        {
            var hash = new HashCode();
            for (var i = 0; i < properties.Count; i++)
            {
                hash.Add(values[i] == null ? 0 : properties[i].GetKeyValueComparer().GetHashCode(values[i]!));
            }
            return hash.ToHashCode();
        }
    }

    /// <summary>An entity instance (by reference) with one of its navigations.</summary>
    private sealed class OwnerNavigationComparer : IEqualityComparer<(object Owner, INavigation Navigation)>
    {
        public static readonly OwnerNavigationComparer Instance = new();

        public bool Equals((object Owner, INavigation Navigation) x, (object Owner, INavigation Navigation) y)
            => ReferenceEquals(x.Owner, y.Owner) && ReferenceEquals(x.Navigation, y.Navigation);

        public int GetHashCode((object Owner, INavigation Navigation) value)
            => HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value.Owner), value.Navigation);
    }
}
