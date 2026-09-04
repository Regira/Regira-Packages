using Regira.Entities.Models.Abstractions;
using Regira.Utilities;
using System.Collections.Concurrent;
using System.Reflection;

namespace Regira.Entities.Attributes;

/// <summary>
/// Resolves and caches the <see cref="ServerOwnedAttribute"/>-marked properties of an entity type. The
/// enforcing prepper and startup validation both read it, so what is reported and what is enforced cannot
/// drift apart.
/// </summary>
public static class ServerOwnedProperties
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> DeclaredCache = new();
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ProtectedCache = new();

    /// <summary>Every property carrying <see cref="ServerOwnedAttribute"/>, enforceable or not.</summary>
    public static IReadOnlyList<PropertyInfo> Declared(Type type)
        => DeclaredCache.GetOrAdd(type, static t => t
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.IsDefined(typeof(ServerOwnedAttribute), inherit: true))
            .ToArray());

    /// <summary>The marked properties the write path restores — the declared ones with no <see cref="SkipReason"/>.</summary>
    public static IReadOnlyList<PropertyInfo> Protected(Type type)
        => ProtectedCache.GetOrAdd(type, static t => Declared(t)
            .Where(p => SkipReason(t, p) == null)
            .ToArray());

    /// <summary>
    /// Why <paramref name="property"/> cannot be server-owned, or <c>null</c> when it can. Written to be
    /// shown as-is in a startup validation message.
    /// </summary>
    public static string? SkipReason(Type type, PropertyInfo property)
    {
        if (IsArchivedFlag(type, property))
        {
            return "it is the IArchivable soft-delete flag, which a restore has to be able to clear";
        }
        if (!property.CanRead || !property.CanWrite)
        {
            return "it has no public getter and setter to restore through";
        }
        if (!TypeUtility.IsSimpleType(property.PropertyType))
        {
            // A navigation carries a tracked graph; copying that reference across Modify()'s detach/attach
            // is not a restore but a second writer for the whole subtree.
            return "only scalars and foreign keys are restored — a navigation or collection is not";
        }
        return null;
    }

    /// <summary>Whether <paramref name="property"/> is <see cref="IArchivable.IsArchived"/> on an archivable entity.</summary>
    public static bool IsArchivedFlag(Type type, PropertyInfo property)
        => typeof(IArchivable).IsAssignableFrom(type) && property.Name == nameof(IArchivable.IsArchived);
}
