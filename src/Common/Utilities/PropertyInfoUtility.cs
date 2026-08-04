using System.Collections;
using System.Reflection;

namespace Regira.Utilities;

public static class PropertyInfoUtility
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="properties"></param>
    /// <param name="myInterface"></param>
    /// <returns></returns>
    public static IEnumerable<PropertyInfo> WithInterface(this IEnumerable<PropertyInfo> properties, Type myInterface) 
        => properties.Where(p => TypeUtility.GetBaseTypes(p.PropertyType).Any(i => i == myInterface));
    /// <summary>
    /// Filters a collection of <see cref="PropertyInfo"/> objects to include only those whose property type implements the specified interface type.
    /// </summary>
    /// <typeparam name="T">The interface type to filter by.</typeparam>
    /// <param name="properties">The collection of <see cref="PropertyInfo"/> objects to filter.</param>
    /// <returns>An <see cref="IEnumerable{T}"/> containing the filtered <see cref="PropertyInfo"/> objects.</returns>
    public static IEnumerable<PropertyInfo> WithInterface<T>(this IEnumerable<PropertyInfo> properties) 
        => WithInterface(properties, typeof(T));

    /// <summary>
    /// Filters a collection of <see cref="PropertyInfo"/> objects to include only those whose property type implements
    /// <see cref="IEnumerable"/> and has a generic argument that implements the specified interface type.
    /// </summary>
    /// <param name="properties">The collection of <see cref="PropertyInfo"/> objects to filter.</param>
    /// <param name="myInterface">The interface type that the generic argument of the property type must implement.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the filtered <see cref="PropertyInfo"/> objects.
    /// </returns>
    public static IEnumerable<PropertyInfo> WithIEnumerableType(this IEnumerable<PropertyInfo> properties, Type myInterface) 
        => properties.Where(p =>
            TypeUtility.GetBaseTypes(p.PropertyType).Any(i => i == typeof(IEnumerable))
            && p.PropertyType.GetGenericArguments().Any(a => TypeUtility.GetBaseTypes(a).Any(i => i == myInterface))
        );
    /// <summary>
    /// Filters a collection of <see cref="PropertyInfo"/> objects to include only those whose property type implements
    /// <see cref="IEnumerable"/> and has a generic argument that implements the specified interface type.
    /// </summary>
    /// <typeparam name="T">The interface type that the generic argument of the property type must implement.</typeparam>
    /// <param name="properties">The collection of <see cref="PropertyInfo"/> objects to filter.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the filtered <see cref="PropertyInfo"/> objects.
    /// </returns>
    public static IEnumerable<PropertyInfo> WithIEnumerableType<T>(this IEnumerable<PropertyInfo> properties)
        => WithIEnumerableType(properties, typeof(T));

    /// <summary>
    /// Filters a collection of <see cref="PropertyInfo"/> objects to include only those that have a specific custom attribute applied.
    /// </summary>
    /// <param name="properties">The collection of <see cref="PropertyInfo"/> objects to filter.</param>
    /// <param name="attribute">The type of the custom attribute to filter by.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the filtered <see cref="PropertyInfo"/> objects that have the specified custom attribute applied.
    /// </returns>
    public static IEnumerable<PropertyInfo> WithCustomAttribute(this IEnumerable<PropertyInfo> properties, Type attribute) 
        => properties.Where(p => p.GetCustomAttributes(true).Any(a => a.GetType() == attribute));
    /// <summary>
    /// Filters a collection of <see cref="PropertyInfo"/> objects to include only those that have a specific custom attribute of type <typeparamref name="T"/> applied.
    /// </summary>
    /// <typeparam name="T">The type of the custom attribute to filter by.</typeparam>
    /// <param name="properties">The collection of <see cref="PropertyInfo"/> objects to filter.</param>
    /// <returns>
    /// An <see cref="IEnumerable{T}"/> containing the filtered <see cref="PropertyInfo"/> objects that have the specified custom attribute of type <typeparamref name="T"/> applied.
    /// </returns>
    public static IEnumerable<PropertyInfo> WithCustomAttribute<T>(this IEnumerable<PropertyInfo> properties) 
        => WithCustomAttribute(properties, typeof(T));

    /// <summary>
    /// Retrieves a specific value from a custom attribute applied to a property.
    /// </summary>
    /// <typeparam name="TAttribute">The type of the attribute to retrieve.</typeparam>
    /// <typeparam name="TValue">The type of the value to extract from the attribute.</typeparam>
    /// <param name="prop">The <see cref="PropertyInfo"/> representing the property to inspect.</param>
    /// <param name="value">
    /// A function that defines how to extract the desired value from the attribute of type <typeparamref name="TAttribute"/>.
    /// </param>
    /// <returns>
    /// The extracted value of type <typeparamref name="TValue"/> if the attribute is found; otherwise, <c>default</c>.
    /// </returns>
    public static TValue? GetAttributeValue<TAttribute, TValue>(this PropertyInfo prop, Func<TAttribute, TValue> value) where TAttribute : Attribute
    {
        var attr = prop.GetCustomAttributes(typeof(TAttribute), true)
            .FirstOrDefault();

        if (attr is TAttribute att)
        {
            return value(att);
        }

        return default;
    }
}