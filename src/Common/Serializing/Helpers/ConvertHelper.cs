using Regira.Serializing.Abstractions;
using Regira.Utilities;

namespace Regira.Serializing.Helpers;

public class ConvertHelper(ISerializer serializer)
{
    /// <summary>
    /// Converts a collection of objects into a strongly-typed collection of the specified type.
    /// </summary>
    /// <typeparam name="T">
    /// The target type for the conversion. Must be a reference type with a parameterless constructor.
    /// </typeparam>
    /// <param name="items">
    /// The collection of objects to be converted. Each object in the collection will be transformed
    /// into a dictionary representation and then deserialized into the target type.
    /// </param>
    /// <returns>
    /// A collection of objects of type <typeparamref name="T"/>. Returns <c>null</c> if the conversion fails.
    /// </returns>
    /// <remarks>
    /// This method uses the provided serializer to serialize and deserialize the objects.
    /// It ensures that the dictionaries created from the objects are type-safe before deserialization.
    /// </remarks>
    public IEnumerable<T>? Convert<T>(IEnumerable<object> items)
        where T : class, new()
    {
        var dics = items
            .Select(x => DictionaryUtility.ToDictionary(x))
            .ToList();
        dics.MakeTypeSafe<T>();
        var json = serializer.Serialize(dics);
        var typedItems = serializer.Deserialize<List<T>>(json);
        return typedItems;
    }
}