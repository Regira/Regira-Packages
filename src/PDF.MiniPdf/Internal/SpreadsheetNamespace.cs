using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Regira.Office.PDF.MiniPdf.Internal;

/// <summary>
/// MiniPdf finds a workbook's elements in the default namespace of each part's root, so a part that binds
/// SpreadsheetML to a prefix (<c>&lt;x:worksheet xmlns:x="…"&gt;</c>, as the Open XML SDK writes — and with it
/// ClosedXML and MiniExcel) reads as empty: a prefixed <c>workbook.xml</c> gives one blank page, a prefixed sheet a
/// sheet without content. Excel itself declares the namespace as the default.
/// </summary>
internal static class SpreadsheetNamespace
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    // Office parts carry no DTD
    private static readonly XmlReaderSettings ReaderSettings = new() { DtdProcessing = DtdProcessing.Prohibit };

    /// <summary>
    /// The workbook with SpreadsheetML declared as the default namespace of every part under <c>xl/</c> that binds it
    /// to a prefix; the workbook itself when none does. Each part is checked: a tool that edits one sheet of an
    /// Excel workbook through the Open XML SDK writes only that part with the prefix.
    /// </summary>
    public static byte[] AsDefault(byte[] xlsx)
    {
        using var package = new ZipArchive(new MemoryStream(xlsx, false), ZipArchiveMode.Read);
        var prefixed = package.Entries
            .Where(entry => entry.FullName.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)
                            && entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Where(IsPrefixed)
            .ToHashSet();
        if (prefixed.Count == 0)
        {
            return xlsx;
        }

        var output = new MemoryStream();
        using (var rewritten = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var entry in package.Entries)
            {
                using var target = rewritten.CreateEntry(entry.FullName, CompressionLevel.Fastest).Open();
                using var source = entry.Open();
                if (prefixed.Contains(entry))
                {
                    using var reader = XmlReader.Create(source, ReaderSettings);
                    var doc = XDocument.Load(reader);
                    // without a prefix bound to it, XLinq writes every SpreadsheetML element unprefixed
                    doc.Descendants().Attributes()
                        .Where(a => a.IsNamespaceDeclaration && a.Value == Main.NamespaceName)
                        .Remove();
                    doc.Root!.SetAttributeValue("xmlns", Main.NamespaceName);
                    doc.Save(target, SaveOptions.DisableFormatting);
                }
                else
                {
                    source.CopyTo(target);
                }
            }
        }
        return output.ToArray();
    }

    /// <summary>
    /// Whether the part's root is a SpreadsheetML element under a prefix. Reads the root element only. A part that
    /// is not well-formed XML is left for MiniPdf to report.
    /// </summary>
    private static bool IsPrefixed(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, ReaderSettings);
        try
        {
            reader.MoveToContent();
            return reader.NamespaceURI == Main.NamespaceName && reader.LookupNamespace(string.Empty) != Main.NamespaceName;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
