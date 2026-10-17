using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Regira.Office.Word.Packaging;

/// <summary>
/// A VML shape's or group's <c>id</c> and <c>o:spid</c> are unique in a part, and a shape type is defined once — Word
/// writes a text box's and a group's fallback copy, and an embedded object, in VML. A copied shape — a template loop's,
/// a collection table's row, a nested document's — keeps its original's; each one met again after its first takes a
/// new one here, an embedded object beside the shape is pointed at the new id, and a repeated shape type goes.
/// </summary>
internal static class VmlShapeIds
{
    public const string VmlNamespace = "urn:schemas-microsoft-com:vml";
    private const string OfficeNamespace = "urn:schemas-microsoft-com:office:office";
    private static readonly XNamespace V = VmlNamespace;
    private static readonly XNamespace O = OfficeNamespace;

    // the VML elements that draw a shape, a group among them: a text box is a v:shape or a v:rect, as the writer chose
    private static readonly HashSet<string> Shapes = ["shape", "group", "rect", "roundrect", "oval", "line", "polyline", "arc", "curve", "image"];

    /// <summary>
    /// Whether the VML element being read repeats a shape's id or <c>o:spid</c>, or a shape type's id, among those
    /// <paramref name="seen"/> holds; it adds its own.
    /// </summary>
    public static bool IsRepeated(XmlReader reader, HashSet<string> seen)
        => reader.NodeType == XmlNodeType.Element && reader.NamespaceURI == VmlNamespace
            && (Shapes.Contains(reader.LocalName)
                ? reader.GetAttribute("id") is { } id && !seen.Add("id:" + id) || reader.GetAttribute("spid", OfficeNamespace) is { } spid && !seen.Add("spid:" + spid)
                : reader.LocalName == "shapetype" && reader.GetAttribute("id") is { } type && !seen.Add("type:" + type));

    /// <summary>Whether the part repeats a VML id, read as a stream: a part that does not is never parsed.</summary>
    public static bool HasRepeated(string xml)
    {
        if (!xml.Contains(VmlNamespace, StringComparison.Ordinal))
        {
            return false;
        }
        using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var seen = new HashSet<string>();
        while (reader.Read())
        {
            if (IsRepeated(reader, seen))
            {
                return true;
            }
        }
        return false;
    }

    /// <returns>Whether any id changed.</returns>
    public static bool MakeUnique(XDocument part)
    {
        var changed = false;
        var types = new HashSet<string>();
        foreach (var type in part.Descendants(V + "shapetype").ToArray())
        {
            // a repeated definition is the same definition: the shapes that name it find the first
            if ((string?)type.Attribute("id") is { } id && !types.Add(id))
            {
                type.Remove();
                changed = true;
            }
        }

        var shapes = part.Descendants().Where(element => element.Name.Namespace == V && Shapes.Contains(element.Name.LocalName)).ToArray();
        foreach (var attribute in new[] { XName.Get("id"), O + "spid" })
        {
            var seen = new HashSet<string>();
            foreach (var shape in shapes)
            {
                if (shape.Attribute(attribute) is not { } id || seen.Add(id.Value))
                {
                    continue;
                }
                var copy = 2;
                while (!seen.Add($"{id.Value}_{copy}"))
                {
                    copy++;
                }
                var renumbered = $"{id.Value}_{copy}";
                if (attribute == XName.Get("id"))
                {
                    // an embedded object beside the shape names the shape that shows it
                    foreach (var embedded in shape.Parent?.Elements(O + "OLEObject") ?? [])
                    {
                        if (embedded.Attribute("ShapeID") is { } shapeId && shapeId.Value == id.Value)
                        {
                            shapeId.Value = renumbered;
                        }
                    }
                }
                id.Value = renumbered;
                changed = true;
            }
        }
        return changed;
    }

    /// <summary>Makes the VML ids unique in each of the package's stories that repeats one; rewritten in place.</summary>
    public static void MakeUnique(Stream package)
    {
        var rewritten = new List<(string Name, XDocument Part)>();
        package.Position = 0;
        using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
        {
            foreach (var entry in WordPackageStories.Of(zip).Select(zip.GetEntry).OfType<ZipArchiveEntry>())
            {
                string xml;
                using (var reader = new StreamReader(entry.Open()))
                {
                    xml = reader.ReadToEnd();
                }
                if (HasRepeated(xml))
                {
                    var part = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                    MakeUnique(part);
                    rewritten.Add((entry.FullName, part));
                }
            }
        }
        if (rewritten.Count > 0)
        {
            package.Position = 0;
            using var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
            foreach (var (name, part) in rewritten)
            {
                WordPackageStories.Save(zip.GetEntry(name)!, part);
            }
        }
        package.Position = 0;
    }
}
