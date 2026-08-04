using System.Collections;
using System.Reflection;

namespace Regira.Utilities;

public static class ObjectUtility
{
    /// <summary>
    /// Merges the properties of multiple source objects into a target object.
    /// </summary>
    /// <typeparam name="T">The type of the objects being merged.</typeparam>
    /// <param name="target">The target object to which properties will be merged.</param>
    /// <param name="sources">An array of source objects whose properties will be merged into the target object.</param>
    /// <returns>The target object with merged properties from the source objects.</returns>
    /// <remarks>
    /// This method iterates through the properties of the source objects and assigns their values to the corresponding properties of the target object.
    /// If a property value in the source object is not null and the property is a complex type, it recursively merges the properties.
    /// </remarks>
    public static T Merge<T>(T target, params T[] sources)
    {
        var type = target!.GetType();
        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(prop => prop.GetIndexParameters().Length == 0 && prop.GetMethod != null)
            .ToArray();

        foreach (var source in sources)
        {
            Merge(target, source, properties);
        }

        return target;
    }
    /// <summary>
    /// Merges the properties of a source object into a target object using the specified properties.
    /// </summary>
    /// <typeparam name="T">The type of the objects being merged.</typeparam>
    /// <param name="target">The target object to which properties will be merged.</param>
    /// <param name="source">The source object whose properties will be merged into the target object.</param>
    /// <param name="properties">The collection of properties to be considered for merging.</param>
    /// <remarks>
    /// This method iterates through the specified properties of the source object and assigns their values to the corresponding properties of the target object.
    /// If a property value in the source object is not null and the property is a complex type, it recursively merges the properties.
    /// </remarks>
    private static void Merge<T>(T target, T source, IEnumerable<PropertyInfo> properties)
    {
        foreach (var property in properties)
        {
            var value = property.GetValue(source);
            if (value != null)
            {
                var targetValue = property.GetValue(target);
                var propertyType = property.PropertyType;
                if (targetValue != null && !TypeUtility.IsSimpleType(propertyType) && propertyType.IsClass)
                {
                    Merge(targetValue, property.GetValue(source));
                }
                else
                {
                    property.SetValue(target, value);
                }
            }
        }
    }

    /// <summary>
    /// Populates the properties of the target object with values from the input object.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object whose properties will be populated.</param>
    /// <param name="input">
    /// The input object containing values to populate the target object. 
    /// It can be an object with matching property names or a dictionary with string keys.
    /// </param>
    /// <returns>The target object with its properties populated from the input object.</returns>
    /// <remarks>
    /// If the input is a dictionary, the keys are matched to the property names of the target object.
    /// If the input is an object, its properties are matched to the target object's properties by name.
    /// Matching is case-insensitive, and PascalCase conversion is applied for compatibility.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if the target object is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if a property value cannot be converted to the target property's type.
    /// </exception>
    public static T Fill<T>(T target, object? input)
    {
        var targetProperties = typeof(T)
            .GetProperties()
            .Where(prop => prop.GetIndexParameters().Length == 0 && prop.SetMethod != null)
            .ToArray();

        if (input != null)
        {
            if (input is IDictionary<string, object?> dic)
            {
                return Fill(target, dic);
            }

            var inputProperties = input.GetType()
                .GetRuntimeProperties()
                .Where(prop => prop.GetIndexParameters().Length == 0 && prop.GetMethod != null)
                .ToArray();
            foreach (var property in targetProperties)
            {
                var inputProperty = inputProperties.FirstOrDefault(p => p.Name == property.Name || p.Name.Equals(property.Name, StringComparison.InvariantCultureIgnoreCase) || p.Name.ToPascalCase()!.Equals(property.Name, StringComparison.InvariantCultureIgnoreCase));
                if (inputProperty != null)
                {
                    var value = ConvertValue(inputProperty.GetValue(input), property.PropertyType);
                    property.SetValue(target!, value, null);
                }
            }
        }

        return target;
    }
    /// <summary>
    /// Creates an instance of the specified type <typeparamref name="T"/> and populates its properties
    /// using the provided <paramref name="input"/> object.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the object to create. Must have a parameterless constructor.
    /// </typeparam>
    /// <param name="input">
    /// An object containing the data to populate the properties of the created instance.
    /// </param>
    /// <returns>
    /// A new instance of type <typeparamref name="T"/> with its properties populated from the <paramref name="input"/> object.
    /// </returns>
    /// <remarks>
    /// This method uses reflection to map the properties of the <paramref name="input"/> object
    /// to the properties of the created instance. Property names are matched case-insensitively.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="input"/> is <c>null</c>.
    /// </exception>
    /// <example>
    /// <code>
    /// var input = new { Name = "John", Age = 30 };
    /// var person = ObjectUtility.Create{Person}(input);
    /// Console.WriteLine(person.Name); // Outputs: John
    /// Console.WriteLine(person.Age);  // Outputs: 30
    /// </code>
    /// </example>
    public static T Create<T>(object input)
        where T : new()
        => Fill(new T(), input);

    /// <summary>
    /// Populates the properties of the target object using the provided dictionary of key-value pairs.
    /// </summary>
    /// <typeparam name="T">The type of the target object.</typeparam>
    /// <param name="target">The target object whose properties will be populated.</param>
    /// <param name="input">
    /// A dictionary containing property names as keys and their corresponding values to populate the target object.
    /// The keys are case-insensitive and can be matched using PascalCase for tolerance.
    /// </param>
    /// <returns>The target object with its properties populated from the dictionary.</returns>
    /// <remarks>
    /// This method iterates through the properties of the target object and assigns values from the dictionary
    /// to the corresponding properties. If a key in the dictionary matches a property name (case-insensitively or in PascalCase),
    /// its value is converted to the property's type and assigned.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if the <paramref name="target"/> or <paramref name="input"/> is null.</exception>
    /// <exception cref="InvalidCastException">
    /// Thrown if a value in the dictionary cannot be converted to the type of the corresponding property.
    /// </exception>
    public static T Fill<T>(T target, IDictionary<string, object?> input)
    {
        var caseInsensitiveInput = new Dictionary<string, object?>(input, StringComparer.InvariantCultureIgnoreCase);
        var targetProperties = typeof(T).GetProperties()
            .Where(prop => prop.GetIndexParameters().Length == 0 && prop.SetMethod != null);
        foreach (var property in targetProperties)
        {
            // find the corresponding input key using ToPascalCase to have some tolerance
            var inputKey = caseInsensitiveInput.Keys.FirstOrDefault(key => key == property.Name || key.Equals(property.Name, StringComparison.InvariantCultureIgnoreCase) || key.ToPascalCase()!.Equals(property.Name, StringComparison.CurrentCultureIgnoreCase));
            if (inputKey != null)
            {
                var value = ConvertValue(caseInsensitiveInput[inputKey], property.PropertyType);
                property.SetValue(target!, value, null);
            }
        }

        return target;
    }

    /// <summary>
    /// Converts the specified value to the specified target type.
    /// </summary>
    /// <param name="value">The value to be converted. Can be <c>null</c>.</param>
    /// <param name="type">The target type to which the value should be converted.</param>
    /// <returns>
    /// The converted value as an object of the specified type, or <c>null</c> if the input value is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// If the value is a collection, it attempts to convert it to a generic collection of the target type.
    /// If the value's type does not match the target type, it uses <see cref="System.Convert.ChangeType(object, Type)"/> 
    /// to perform the conversion.
    /// </remarks>
    public static object? ConvertValue(object? value, Type type)
    {
        if (value is ICollection collection)
        {
            var genericType = TypeUtility.GetGenericCollectionType(type);
            if (genericType != null)
            {
                value = CollectionUtility.CreateGenericList(genericType, collection);
            }
        }

        if (value != null && value.GetType() != TypeUtility.GetSimpleType(type) && !TypeUtility.GetBaseTypes(value.GetType()).Contains(TypeUtility.GetSimpleType(type)))
        {
            return Convert.ChangeType(value, TypeUtility.GetSimpleType(type));
        }

        return value;
    }
}