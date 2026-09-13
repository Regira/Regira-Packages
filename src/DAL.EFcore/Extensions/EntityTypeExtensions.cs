using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Regira.DAL.EFcore.Extensions;

public static class EntityTypeExtensions
{
    // ConcurrentDictionary, and typed as one: reached through IDictionary the only way to fill it is
    // ContainsKey-then-Add, and two requests priming the same entity type both pass the check and the
    // loser throws "the key already existed". GetOrAdd is the atomic form. The factory is pure, so the
    // extra evaluation a racing caller may do costs nothing beyond the work itself.
    private static readonly ConcurrentDictionary<IEntityType, IDictionary<IProperty, Attribute[]>> AttributesMetadataCache = new();

    public static IDictionary<IProperty, Attribute[]> GetPropertyAttributes(this EntityEntry entry)
        => entry.Metadata.GetPropertyAttributes();
    public static IDictionary<IProperty, Attribute[]> GetPropertyAttributes(this IEntityType entityType)
        => AttributesMetadataCache.GetOrAdd(
            entityType,
            static type => type.GetProperties()
                .Where(p => p.PropertyInfo != null)
                .ToDictionary(p => p, p => p.PropertyInfo!.GetCustomAttributes(false).Cast<Attribute>().ToArray()));
}