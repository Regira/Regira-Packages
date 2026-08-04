using System.Linq.Expressions;
using System.Reflection;

namespace Regira.Utilities;

public static class AttributeUtility
{
    /// <summary>
    /// Retrieves the value of a specified attribute applied to a property, using a selector function.
    /// </summary>
    /// <typeparam name="T">The type of the class containing the property.</typeparam>
    /// <typeparam name="TOut">The type of the property.</typeparam>
    /// <typeparam name="TAttribute">The type of the attribute to retrieve.</typeparam>
    /// <typeparam name="TValue">The type of the value to extract from the attribute.</typeparam>
    /// <param name="propertyExpression">
    /// An expression that specifies the property for which the attribute value is to be retrieved.
    /// </param>
    /// <param name="valueSelector">
    /// A function to extract the desired value from the attribute.
    /// </param>
    /// <param name="inherited">
    /// A boolean value indicating whether to search the inheritance chain for the attribute.
    /// </param>
    /// <returns>
    /// The value extracted from the attribute using the <paramref name="valueSelector"/> function, 
    /// or <c>null</c> if the attribute is not found.
    /// </returns>
    public static TValue? GetPropertyAttributeValue<T, TOut, TAttribute, TValue>
        (Expression<Func<T, TOut>> propertyExpression, Func<TAttribute, TValue> valueSelector, bool inherited = true)
        where TAttribute : Attribute
    {
        var expression = (MemberExpression)propertyExpression.Body;
        var propertyInfo = (PropertyInfo)expression.Member;
        return propertyInfo.GetCustomAttributes(typeof(TAttribute), inherited).FirstOrDefault() is TAttribute attr ? valueSelector(attr) : default;
    }
}