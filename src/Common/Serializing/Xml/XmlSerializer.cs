using Regira.Serializing.Abstractions;
using Regira.Utilities;

namespace Regira.Serializing.Xml;

/// <summary>
/// Provides functionality for serializing and deserializing objects to and from XML format.
/// </summary>
/// <remarks>
/// This class implements the <see cref="Regira.Serializing.Abstractions.ISerializer"/> interface, 
/// enabling XML-based serialization and deserialization for various object types. 
/// It utilizes utility methods from <see cref="Regira.Utilities.XmlUtility"/> and 
/// <see cref="Regira.Utilities.ObjectUtility"/> to perform its operations.
/// </remarks>
public class XmlSerializer : ISerializer
{
    /// <summary>
    /// Deserializes the specified XML string into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="content">
    /// The XML string to deserialize. Can be <c>null</c> or empty.
    /// </param>
    /// <returns>
    /// An object of type <typeparamref name="T"/> if deserialization is successful; otherwise, <c>null</c>.
    /// </returns>
    /// <remarks>
    /// This method utilizes <see cref="Regira.Utilities.XmlUtility.Deserialize{T}"/> to perform the deserialization.
    /// </remarks>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the XML string cannot be deserialized into the specified type <typeparamref name="T"/>.
    /// </exception>
    public T? Deserialize<T>(string? content)
        => XmlUtility.Deserialize<T>(content);
    /// <summary>
    /// Deserializes the specified XML content into an object of the specified type.
    /// </summary>
    /// <param name="content">The XML content to deserialize. Can be <c>null</c>.</param>
    /// <param name="type">The <see cref="Type"/> of the object to deserialize into.</param>
    /// <returns>
    /// An instance of the specified type populated with data from the XML content, 
    /// or <c>null</c> if the content is <c>null</c> or deserialization fails.
    /// </returns>
    /// <remarks>
    /// This method uses <see cref="Regira.Utilities.XmlUtility.Deserialize{T}"/> to parse the XML content 
    /// and <see cref="ObjectUtility.Fill{T}(T,object?)"/> to populate the properties of the created object.
    /// </remarks>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="type"/> is <c>null</c>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the object cannot be created or populated due to invalid XML content or type mismatch.
    /// </exception>
    public object? Deserialize(string? content, Type type)
        => ObjectUtility.Fill(Activator.CreateInstance(type), XmlUtility.Deserialize<object?>(content));

    /// <summary>
    /// Serializes the specified object into its XML string representation.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="item">The object to serialize into XML.</param>
    /// <returns>A string containing the XML representation of the specified object.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided <paramref name="item"/> is <c>null</c>.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the object cannot be serialized due to invalid data or configuration.
    /// </exception>
    public string Serialize<T>(T item)
        => XmlUtility.Serialize(item);
}