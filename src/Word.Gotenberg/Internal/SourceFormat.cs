using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Regira.IO.Abstractions;

namespace Regira.Office.Word.Gotenberg.Internal;

/// <summary>
/// Picks the file extension a document is uploaded under. Gotenberg accepts a file only when its
/// extension is on the LibreOffice route's list, and LibreOffice leans on it to choose an import filter.
/// </summary>
internal static class SourceFormat
{
    /// <summary>
    /// The word-processing extensions the route accepts that this backend sends.
    /// </summary>
    private static readonly HashSet<string> Accepted = new(StringComparer.OrdinalIgnoreCase)
    {
        "doc", "dot", "docx", "dotx", "docm", "dotm", "odt", "ott", "rtf", "txt", "html", "htm", "epub"
    };

    private static readonly HashSet<string> OpenXmlExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "docx", "dotx", "docm", "dotm"
    };

    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B, 0x03, 0x04];
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    public static bool IsOpenXml(string extension) => OpenXmlExtensions.Contains(extension);

    /// <summary>
    /// A file name wins when it carries an accepted extension; otherwise the content decides.
    /// </summary>
    public static string Resolve(IMemoryFile file, byte[] bytes)
    {
        var fileName = (file as INamedFile)?.FileName;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var extension = Path.GetExtension(fileName).TrimStart('.');
            if (Accepted.Contains(extension))
            {
                return extension.ToLowerInvariant();
            }
        }

        return Sniff(bytes)
               ?? throw new NotSupportedException(
                   "Cannot tell the template's format from its content. Supply it as a named file (INamedFile) " +
                   "with a word-processing extension such as .docx, .doc, .odt or .rtf.");
    }

    public static string? Sniff(byte[] bytes)
    {
        // .doc and .dot share the OLE container; LibreOffice tells them apart itself
        if (StartsWith(bytes, OleSignature))
        {
            return "doc";
        }
        if (StartsWith(bytes, ZipSignature))
        {
            return SniffPackage(bytes);
        }

        var offset = StartsWith(bytes, Utf8Bom) ? Utf8Bom.Length : 0;
        var head = Encoding.UTF8.GetString(bytes, offset, Math.Min(bytes.Length - offset, 1024));
        if (head.StartsWith(@"{\rtf", StringComparison.Ordinal))
        {
            return "rtf";
        }
        if (head.TrimStart().StartsWith('<') && head.Contains("<html", StringComparison.OrdinalIgnoreCase))
        {
            return "html";
        }

        return null;
    }

    /// <summary>
    /// ODF and EPUB packages name their type in a leading <c>mimetype</c> entry; OOXML packages in the
    /// main part's content type.
    /// </summary>
    private static string? SniffPackage(byte[] bytes)
    {
        try
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);

            var mimetype = zip.GetEntry("mimetype");
            if (mimetype != null)
            {
                using var reader = new StreamReader(mimetype.Open());
                return reader.ReadToEnd().Trim() switch
                {
                    "application/vnd.oasis.opendocument.text" => "odt",
                    "application/vnd.oasis.opendocument.text-template" => "ott",
                    "application/epub+zip" => "epub",
                    _ => null
                };
            }

            var contentTypes = zip.GetEntry("[Content_Types].xml");
            if (contentTypes == null)
            {
                return null;
            }

            using var stream = contentTypes.Open();
            var types = XDocument.Load(stream)
                .Descendants()
                .Select(element => (string?)element.Attribute("ContentType"))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (types.Contains("application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"))
            {
                return "docx";
            }
            if (types.Contains("application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"))
            {
                return "dotx";
            }
            if (types.Contains("application/vnd.ms-word.document.macroEnabled.main+xml"))
            {
                return "docm";
            }
            if (types.Contains("application/vnd.ms-word.template.macroEnabledTemplate.main+xml"))
            {
                return "dotm";
            }
            return null;
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException)
        {
            // a zip signature on something that is not a readable package
            return null;
        }
    }

    private static bool StartsWith(byte[] bytes, byte[] signature)
        => bytes.Length >= signature.Length && bytes.AsSpan(0, signature.Length).SequenceEqual(signature);
}
