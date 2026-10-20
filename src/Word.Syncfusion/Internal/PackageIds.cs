using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Regira.Office.Word.Packaging;

namespace Regira.Office.Word.Syncfusion.Internal;

/// <summary>
/// Ids a document holds once, which DocIO writes again for a copied node — a template loop's copy among them: a
/// content control's id (<c>w:sdtPr/w:id</c>), which DocIO does not expose, and a grouped shape's VML id, which DocIO
/// writes from a name it does not expose either. In the written package, each id met again after its first takes a new
/// one.
/// </summary>
internal static class PackageIds
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <param name="package">The document as DocIO wrote it; an Open XML package is rewritten in place, any other format left as it is</param>
    public static void MakeUnique(Stream package)
    {
        if (!IsZip(package))
        {
            return;
        }

        var parts = new List<(string Name, XDocument Part)>();
        package.Position = 0;
        using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
        {
            var stories = WordPackageStories.Of(zip).Select(zip.GetEntry).OfType<ZipArchiveEntry>().ToArray();
            // streamed first, so a package whose ids are all unique — every one no loop copied a control or shape into —
            // is only read
            var controls = new HashSet<string>();
            if (!stories.Any(entry => HasRepeatedId(entry, controls)))
            {
                package.Position = 0;
                return;
            }
            foreach (var entry in stories)
            {
                using var stream = entry.Open();
                parts.Add((entry.FullName, XDocument.Load(stream, LoadOptions.PreserveWhitespace)));
            }
        }

        var changed = new HashSet<string>();
        foreach (var (name, part) in parts)
        {
            if (VmlShapeIds.MakeUnique(part))
            {
                changed.Add(name);
            }
        }

        // content-control ids are unique across the package, and signed 32-bit numbers
        var ids = parts.SelectMany(part => part.Part.Descendants(W + "sdtPr").Elements(W + "id").Select(id => (part.Name, Id: id))).ToArray();
        var used = ids.Select(id => (string?)id.Id.Attribute(W + "val")).OfType<string>().ToHashSet();
        var seen = new HashSet<string>();
        var next = ids.Select(id => long.TryParse((string?)id.Id.Attribute(W + "val"), out var value) ? value : 0).DefaultIfEmpty(0).Max();
        foreach (var (name, id) in ids)
        {
            if (seen.Add((string?)id.Attribute(W + "val") ?? ""))
            {
                continue;
            }
            // the next number no control holds, wrapping past the largest
            do
            {
                next = next >= int.MaxValue ? int.MinValue : next + 1;
            } while (!used.Add(next.ToString()));
            id.SetAttributeValue(W + "val", next);
            changed.Add(name);
        }

        if (changed.Count > 0)
        {
            package.Position = 0;
            using var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
            foreach (var (name, part) in parts.Where(part => changed.Contains(part.Name)))
            {
                WordPackageStories.Save(zip.GetEntry(name)!, part);
            }
        }
        package.Position = 0;
    }

    /// <summary>
    /// Whether the part holds a content-control id that <paramref name="controls"/>, or the part itself, already holds,
    /// or a VML shape id the part holds twice.
    /// </summary>
    private static bool HasRepeatedId(ZipArchiveEntry entry, HashSet<string> controls)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
        var shapes = new HashSet<string>();
        // the depth of the w:sdtPr being read, whose w:id children are the ids; -1 outside one
        var properties = -1;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == properties)
            {
                properties = -1;
            }
            else if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            else if (reader.NamespaceURI == W.NamespaceName)
            {
                if (reader.LocalName == "sdtPr" && !reader.IsEmptyElement)
                {
                    properties = reader.Depth;
                }
                else if (reader.LocalName == "id" && properties >= 0 && reader.Depth == properties + 1
                         && reader.GetAttribute("val", W.NamespaceName) is { } id && !controls.Add(id))
                {
                    return true;
                }
            }
            else if (VmlShapeIds.IsRepeated(reader, shapes))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsZip(Stream package)
    {
        package.Position = 0;
        var isZip = package.ReadByte() == 'P' && package.ReadByte() == 'K';
        package.Position = 0;
        return isZip;
    }
}
