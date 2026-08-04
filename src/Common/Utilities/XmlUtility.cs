using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace Regira.Utilities;

public static class XmlUtility
{
    /// <summary>
    /// Converts an XML string into a <see cref="Stream"/> representation.
    /// </summary>
    /// <param name="xml">The XML string to be converted into a stream.</param>
    /// <returns>A <see cref="Stream"/> containing the XML data.</returns>
    /// <exception cref="System.Xml.XmlException">Thrown if the provided XML string is not well-formed.</exception>
    public static Stream ToStream(string xml)
    {
        var doc = XDocument.Parse(xml);
        return ToStream(doc);
    }
    /// <summary>
    /// Reads an XML document from the provided <see cref="Stream"/> and converts it into a string representation.
    /// </summary>
    /// <param name="stream">The <see cref="Stream"/> containing the XML data to be read.</param>
    /// <returns>A string representation of the XML document.</returns>
    /// <exception cref="System.Xml.XmlException">Thrown if the XML data in the stream is not well-formed.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided <paramref name="stream"/> is <c>null</c>.</exception>
    public static string FromStream(Stream stream)
    {
        var doc = XDocument.Load(stream);
        return doc.ToString();
    }
    /// <summary>
    /// Converts the specified <see cref="XDocument"/> into a <see cref="Stream"/> representation.
    /// </summary>
    /// <param name="doc">The <see cref="XDocument"/> to be converted into a stream.</param>
    /// <returns>A <see cref="Stream"/> containing the XML data from the <paramref name="doc"/>.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided <paramref name="doc"/> is <c>null</c>.</exception>
    public static Stream ToStream(this XDocument doc)
    {
        var ms = new MemoryStream();
        var xws = new XmlWriterSettings
        {
            //OmitXmlDeclaration = true,
            Indent = true
        };

        using var xw = XmlWriter.Create(ms, xws);
        doc.WriteTo(xw);

        return ms;
    }

    /// <summary>
    /// Serializes the specified object into its XML string representation.
    /// </summary>
    /// <typeparam name="T">The type of the object to serialize.</typeparam>
    /// <param name="obj">The object to serialize into XML.</param>
    /// <returns>A string containing the XML representation of the specified object.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown if the provided <paramref name="obj"/> is <c>null</c>.</exception>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the object cannot be serialized due to invalid data or configuration.
    /// </exception>
    public static string Serialize<T>(T obj)
    {
        using var writer = new StringWriter();
        var serializer = new XmlSerializer(typeof(T));
        serializer.Serialize(writer, obj!);
        return writer.ToString();
    }
    /// <summary>
    /// Deserializes the specified XML string into an object of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The type of the object to deserialize to.</typeparam>
    /// <param name="content">The XML string to deserialize. Can be <c>null</c> or empty.</param>
    /// <returns>An object of type <typeparamref name="T"/> if deserialization is successful; otherwise, <c>null</c>.</returns>
    /// <exception cref="System.InvalidOperationException">
    /// Thrown if the XML string cannot be deserialized into the specified type <typeparamref name="T"/>.
    /// </exception>
    public static T? Deserialize<T>(string? content)
    {
        var result = default(T);

        if (!string.IsNullOrEmpty(content))
        {
            using var stringReader = new StringReader(content);
            using var xmlReader = XmlReader.Create(stringReader);
            var serializer = new XmlSerializer(typeof(T));

            if (serializer.CanDeserialize(xmlReader))
            {
                result = (T?)serializer.Deserialize(xmlReader);
            }
        }
        return result;
    }
}