using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using Regira.Entities.Models;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validators;

/// <summary>Reads the <c>args</c> of <see cref="IEntityValidatorContext.AddError"/> into <see cref="EntityInputError.Args"/>.</summary>
internal static class EntityInputErrorArgs
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> Properties = new();

    /// <summary>
    /// A dictionary's entries, or the public properties of any other object — an anonymous one — by name; <c>null</c> when
    /// that leaves nothing.
    /// </summary>
    public static IReadOnlyDictionary<string, object?>? From(object? args)
    {
        if (args == null)
        {
            return null;
        }

        var values = new Dictionary<string, object?>();
        switch (args)
        {
            case IEnumerable<KeyValuePair<string, object?>> pairs:
                foreach (var (key, value) in pairs)
                {
                    values[key] = value;
                }
                break;
            // a dictionary of another value type (Dictionary<string, int>) is no IEnumerable<KeyValuePair<string, object?>>
            case IDictionary dictionary:
                foreach (DictionaryEntry entry in dictionary)
                {
                    values[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)!] = entry.Value;
                }
                break;
            default:
                foreach (var property in Properties.GetOrAdd(args.GetType(), static type => type
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                             .Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                             .ToArray()))
                {
                    values[property.Name] = property.GetValue(args);
                }
                break;
        }
        return values.Count == 0 ? null : values;
    }
}
