using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Regira.DAL.EFcore.Extensions;

public static class EntityTypeExtensions
{
    private static readonly IDictionary<IEntityType, IDictionary<IProperty, Attribute[]>> AttributesMetadataCache
        = new ConcurrentDictionary<IEntityType, IDictionary<IProperty, Attribute[]>>();

    public static IDictionary<IProperty, Attribute[]> GetPropertyAttributes(this EntityEntry entry)
        => entry.Metadata.GetPropertyAttributes();
    public static IDictionary<IProperty, Attribute[]> GetPropertyAttributes(this IEntityType entityType)
    {
        if (!AttributesMetadataCache.ContainsKey(entityType))
        {
            var properties = entityType.GetProperties()
                .Where(p => p.PropertyInfo != null)
                .ToDictionary(p => p, p => p.PropertyInfo!.GetCustomAttributes(false).Cast<Attribute>().ToArray());
            AttributesMetadataCache.Add(entityType, properties);
        }

        return AttributesMetadataCache[entityType];
    }
}