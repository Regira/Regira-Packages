using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Spire.Doc;

namespace Regira.Office.Word.Spire.Internal;

/// <summary>
/// Word writes a text box twice: as DrawingML in an <c>mc:Choice</c>, and as a VML copy in the <c>mc:Fallback</c>
/// beside it, for readers that do not know DrawingML shapes — plain Open XML text extraction among them. Spire
/// loads the fallback into a copy of its own that nothing in its document model reaches, and writes that copy back
/// as it was read, so parameters, conditional blocks and nested documents would leave the template's tags in it.
/// Rewriting each fallback text box from its DrawingML copy in the written package keeps the two the same, as Word
/// writes them.
/// </summary>
internal static class TextBoxFallbacks
{
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Whether Spire writes the format as an Open XML package: Docx, Dotx, Docm or Dotm, in any version.</summary>
    public static bool InPackage(FileFormat format)
        => format is FileFormat.Docx or FileFormat.Docx2010 or FileFormat.Docx2013 or FileFormat.Docx2016 or FileFormat.Docx2019
            or FileFormat.Dotx or FileFormat.Dotx2010 or FileFormat.Dotx2013 or FileFormat.Dotx2016 or FileFormat.Dotx2019
            or FileFormat.Docm or FileFormat.Docm2010 or FileFormat.Docm2013 or FileFormat.Docm2016 or FileFormat.Docm2019
            or FileFormat.Dotm or FileFormat.Dotm2010 or FileFormat.Dotm2013 or FileFormat.Dotm2016 or FileFormat.Dotm2019;

    /// <summary>
    /// Rewrites the fallback copy of every text box in the package's stories from its DrawingML copy. A part without
    /// such a text box, or whose copies already match, is left as it is.
    /// </summary>
    /// <param name="package">The package as Spire wrote it; rewritten in place</param>
    public static void Synchronize(Stream package)
    {
        var rewritten = new Dictionary<string, XDocument>();
        package.Position = 0;
        using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
        {
            var stories = Stories(zip);
            foreach (var entry in zip.Entries.Where(entry => stories.Contains(entry.FullName)))
            {
                string xml;
                using (var reader = new StreamReader(entry.Open()))
                {
                    xml = reader.ReadToEnd();
                }
                if (!xml.Contains("txbxContent") || !xml.Contains("AlternateContent"))
                {
                    continue;
                }

                var part = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                if (Synchronize(part))
                {
                    rewritten[entry.FullName] = part;
                }
            }
        }

        if (rewritten.Count > 0)
        {
            package.Position = 0;
            using var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
            foreach (var (name, part) in rewritten)
            {
                using var stream = zip.GetEntry(name)!.Open();
                stream.SetLength(0);
                // a carriage return in the part's text is a character reference, which only Entitize writes back as one:
                // Replace writes a line feed, and None a bare carriage return, which a reader takes for one
                using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), NewLineHandling = NewLineHandling.Entitize });
                part.Save(writer);
            }
        }
        package.Position = 0;
    }

    /// <summary>
    /// The entry names of the parts a template's parameters, blocks and nested documents reach: the main document, the
    /// headers and the footers, found by their content types. No other part's copies can differ.
    /// </summary>
    private static HashSet<string> Stories(ZipArchive zip)
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

    private static bool Synchronize(XDocument part)
    {
        var changed = false;
        // innermost first, so a box's content is copied once whatever it holds is
        foreach (var alternate in part.Descendants(Mc + "AlternateContent").Reverse().ToArray())
        {
            var fallback = alternate.Element(Mc + "Fallback");
            var choice = alternate.Elements(Mc + "Choice").FirstOrDefault(copy => TextBoxes(copy, alternate).Any());
            if (fallback == null || choice == null)
            {
                continue;
            }

            var sources = TextBoxes(choice, alternate).ToArray();
            var targets = TextBoxes(fallback, alternate).ToArray();
            // a group holds its boxes in the same order in both copies; a fallback shaped otherwise is not a copy
            if (sources.Length != targets.Length)
            {
                continue;
            }

            for (var i = 0; i < sources.Length; i++)
            {
                if (!XNode.DeepEquals(sources[i], targets[i]))
                {
                    targets[i].ReplaceNodes(sources[i].Nodes());
                    changed = true;
                }
            }
        }
        return changed;
    }

    /// <summary>The text boxes of one copy of <paramref name="alternate"/>, leaving out those of an alternate content inside it.</summary>
    private static IEnumerable<XElement> TextBoxes(XElement copy, XElement alternate)
        => copy.Descendants(W + "txbxContent").Where(box => box.Ancestors(Mc + "AlternateContent").First() == alternate);
}
