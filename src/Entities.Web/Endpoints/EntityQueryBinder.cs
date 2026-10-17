using System.Collections;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Regira.Entities.Models;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// Binds a search object, paging and the <c>includes</c> / <c>sortBy</c> lists from the query string, as MVC's
/// <c>[FromQuery]</c> binds them for the controllers. Minimal APIs bind no complex type from the query, and not even the
/// default search object's <c>ICollection&lt;TKey&gt;</c> properties.
/// <para>
/// A public settable property binds when its type converts from a string — numbers, text, enums, dates, <c>Guid</c>s and
/// their nullables — or is a collection of such values (an array, a <c>List&lt;T&gt;</c>, a <c>HashSet&lt;T&gt;</c> or an
/// interface a list implements). Names match case-insensitively; a collection takes repeated keys (<c>ids=1&amp;ids=2</c>)
/// or indexed ones (<c>ids[0]=1</c>). Values convert with the invariant culture, an enum by name in any case, and an empty
/// value is no value. A value that does not convert is an error under the property's name, as MVC's model state has it.
/// A property of a complex type does not bind.
/// </para>
/// </summary>
internal static class EntityQueryBinder
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> BindableProperties = new();

    public static T Bind<T>(IQueryCollection query, ICollection<EntityInputError> errors)
        where T : class, new()
    {
        var model = new T();
        foreach (var property in BindableProperties.GetOrAdd(typeof(T), FindBindableProperties))
        {
            var values = ValuesOf(query, property.Name);
            if (values.Count == 0)
            {
                continue;
            }
            if (TryConvert(property.PropertyType, values, out var value, out var invalid))
            {
                property.SetValue(model, value);
            }
            else
            {
                errors.Add(Invalid(property.Name, invalid));
            }
        }
        return model;
    }

    public static TEnum[] BindList<TEnum>(IQueryCollection query, string name, ICollection<EntityInputError> errors)
        where TEnum : struct, Enum
    {
        var values = ValuesOf(query, name);
        if (values.Count == 0)
        {
            return [];
        }
        if (TryConvert(typeof(TEnum[]), values, out var result, out var invalid))
        {
            return (TEnum[])result!;
        }
        errors.Add(Invalid(name, invalid));
        return [];
    }

    public static T? BindValue<T>(IQueryCollection query, string name, ICollection<EntityInputError> errors)
    {
        var values = ValuesOf(query, name);
        if (values.Count == 0)
        {
            return default;
        }
        if (TryConvert(typeof(T), values, out var result, out var invalid))
        {
            return (T?)result;
        }
        errors.Add(Invalid(name, invalid));
        return default;
    }

    private static EntityInputError Invalid(string name, string? value)
        => new(name, $"The value '{value}' is not valid for {name}.");

    private static PropertyInfo[] FindBindableProperties(Type type)
        => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic && p.GetIndexParameters().Length == 0)
            .Where(p => IsScalar(p.PropertyType) || CollectionElementType(p.PropertyType) is { } element && IsScalar(element))
            .ToArray();

    private static List<string> ValuesOf(IQueryCollection query, string name)
    {
        var values = query[name].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!).ToList();
        var indexed = query.Keys
            .Select(key => (key, index: IndexOf(key, name)))
            .Where(x => x.index != null)
            .OrderBy(x => x.index);
        foreach (var (key, _) in indexed)
        {
            values.AddRange(query[key].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!));
        }
        return values;
    }

    // name[3] → 3
    private static int? IndexOf(string key, string name)
        => key.Length > name.Length + 2 && key.StartsWith(name, StringComparison.OrdinalIgnoreCase) && key[name.Length] == '[' && key[^1] == ']'
           && int.TryParse(key.AsSpan(name.Length + 1, key.Length - name.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index
            : null;

    private static bool TryConvert(Type type, List<string> values, out object? result, out string? invalid)
    {
        invalid = null;
        if (CollectionElementType(type) is not { } elementType || type == typeof(string))
        {
            return TryConvertScalar(type, values[0], out result) || Fail(values[0], out result, out invalid);
        }

        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;
        foreach (var value in values)
        {
            if (!TryConvertScalar(elementType, value, out var item))
            {
                return Fail(value, out result, out invalid);
            }
            list.Add(item);
        }

        if (type.IsArray)
        {
            var array = Array.CreateInstance(elementType, list.Count);
            list.CopyTo(array, 0);
            result = array;
        }
        else if (type.IsAssignableFrom(list.GetType()))
        {
            result = list;
        }
        else
        {
            result = Activator.CreateInstance(typeof(HashSet<>).MakeGenericType(elementType), list);
        }
        return true;
    }

    private static bool Fail(string value, out object? result, out string? invalid)
    {
        result = null;
        invalid = value;
        return false;
    }

    private static bool TryConvertScalar(Type type, string value, out object? result)
    {
        try
        {
            result = TypeDescriptor.GetConverter(type).ConvertFromString(null, CultureInfo.InvariantCulture, value);
        }
        catch (Exception ex) when (ex is FormatException or NotSupportedException or ArgumentException or OverflowException)
        {
            result = null;
            return false;
        }
        // a number that names no member is no value of a plain enum, as MVC's enum binder has it
        var enumType = Nullable.GetUnderlyingType(type) ?? type;
        return !enumType.IsEnum || enumType.IsDefined(typeof(FlagsAttribute), false) || result == null || Enum.IsDefined(enumType, result);
    }

    private static bool IsScalar(Type type)
        => type != typeof(object) && TypeDescriptor.GetConverter(type).CanConvertFrom(typeof(string));

    // the T of a collection the binder can create: an array, a type a List<T> is, or a type a HashSet<T> is
    private static Type? CollectionElementType(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }
        if (type.IsArray)
        {
            return type.GetElementType();
        }
        var enumerable = type.GetInterfaces().Prepend(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (enumerable == null)
        {
            return null;
        }
        var element = enumerable.GetGenericArguments()[0];
        return type.IsAssignableFrom(typeof(List<>).MakeGenericType(element)) || type.IsAssignableFrom(typeof(HashSet<>).MakeGenericType(element))
            ? element
            : null;
    }
}
