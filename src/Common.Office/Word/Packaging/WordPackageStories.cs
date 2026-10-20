using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Regira.Office.Word.Packaging;

/// <summary>The parts of a written Word package that a template reaches: the main document, the headers and the footers.</summary>
internal static class WordPackageStories
{
    /// <summary>
    /// The entry names of the package's main document, header and footer parts, found by their content types. Empty
    /// for an archive that is no Open XML package.
    /// </summary>
    public static HashSet<string> Of(ZipArchive zip)
    {
        var stories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var stream = zip.GetEntry("[Content_Types].xml")?.Open();
        if (stream == null)
        {
            return stories;
        }

        // a part's content type is its Override's, or else the Default of its extension
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in XDocument.Load(stream).Root!.Elements())
        {
            var contentType = (string?)type.Attribute("ContentType") ?? "";
            switch (type.Name.LocalName)
            {
                case "Override":
                    overrides.TryAdd(((string?)type.Attribute("PartName") ?? "").TrimStart('/'), contentType);
                    break;
                case "Default":
                    defaults.TryAdd((string?)type.Attribute("Extension") ?? "", contentType);
                    break;
            }
        }

        foreach (var entry in zip.Entries)
        {
            var contentType = overrides.TryGetValue(entry.FullName, out var overridden) ? overridden
                : defaults.GetValueOrDefault(Path.GetExtension(entry.FullName).TrimStart('.')) ?? "";
            if (contentType.EndsWith(".main+xml", StringComparison.Ordinal)
                || contentType.EndsWith("wordprocessingml.header+xml", StringComparison.Ordinal)
                || contentType.EndsWith("wordprocessingml.footer+xml", StringComparison.Ordinal))
            {
                stories.Add(entry.FullName);
            }
        }
        return stories;
    }

    /// <summary>
    /// Writes the part back into its entry. A carriage return in the part's text is a character reference, which only
    /// Entitize writes back as one: Replace writes a line feed, and None a bare carriage return, which a reader takes for one.
    /// </summary>
    public static void Save(ZipArchiveEntry entry, XDocument part)
    {
        using var stream = entry.Open();
        stream.SetLength(0);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize });
        part.Save(writer);
    }
}
