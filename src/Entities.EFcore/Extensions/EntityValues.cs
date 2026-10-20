using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Reads and writes an entity's mapped members and compares its key values, on an entity that need not be tracked.
/// Reflection rather than EF's own getter, whose API differs between the EF Core majors this package targets.
/// </summary>
internal static class EntityValues
{
    /// <summary>The CLR value of <paramref name="property"/> on <paramref name="entity"/>.</summary>
    public static object? Read(IPropertyBase property, object entity)
        => property.PropertyInfo?.GetMethod != null
            ? property.PropertyInfo.GetValue(entity)
            : property.FieldInfo?.GetValue(entity);

    /// <summary>Sets the CLR value of <paramref name="property"/> on <paramref name="entity"/>.</summary>
    public static void Write(IPropertyBase property, object entity, object? value)
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

    /// <summary>The values of <paramref name="properties"/> on <paramref name="entity"/>, in order.</summary>
    public static object?[] KeyOf(IReadOnlyList<IProperty> properties, object entity)
        => properties.Select(p => Read(p, entity)).ToArray();

    /// <summary>Whether two sets of values of <paramref name="properties"/> are the same key, as EF compares keys.</summary>
    public static bool SameKey(IReadOnlyList<IProperty> properties, object?[] left, object?[] right)
        => properties.Select((p, i) => p.GetKeyValueComparer().Equals(left[i], right[i])).All(same => same);

    /// <summary>Key values compared the way EF compares them, so a dictionary finds a row in one lookup.</summary>
    public sealed class KeyComparer(IReadOnlyList<IProperty> properties) : IEqualityComparer<object?[]>
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
}
