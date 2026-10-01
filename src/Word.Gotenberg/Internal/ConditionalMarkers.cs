using System.IO.Compression;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Regira.Office.Word.Templating;

namespace Regira.Office.Word.Gotenberg.Internal;

/// <summary>
/// Finds the conditional blocks of an OOXML template. A template holding one needs an <c>IWordCreator</c> even
/// without a single parameter: a condition on a key the input does not give is false, and its block has to go.
/// </summary>
internal static class ConditionalMarkers
{
    /// <summary>
    /// The most a document's parts may hold together, uncompressed, in bytes. Every OOXML conversion is scanned in-process,
    /// and a document that opens a block is then created in-process, where its parts are loaded whole — the XML, and the
    /// images too, which Word.Spire and Word.Aspose decode on load: a document declaring more — a zip bomb, or one far larger
    /// than any template — is not opened, and goes to Gotenberg as it is, its blocks unresolved.
    /// </summary>
    internal const long MaxBytes = 32 * 1024 * 1024;
    /// <summary>The most text a paragraph can hold and still be read as a marker; the scan keeps no more of it.</summary>
    private const int MaxMarkerLength = 1024;

    /// <summary>
    /// Whether the body, a header or a footer holds a paragraph that opens a block — what makes a document use blocks,
    /// as the creators decide it. Nothing else does, marker text among other text included: that document converts as
    /// it is. A package that cannot be read holds none, and so does one whose parts exceed <see cref="MaxBytes"/>
    /// together: it goes to Gotenberg as it is, which reports what is wrong with it.
    /// </summary>
    public static bool Any(byte[] source)
        => Any(source, MaxBytes);

    internal static bool Any(byte[] source, long maxBytes)
    {
        try
        {
            return !Exceeds(source, maxBytes) && Find(source);
        }
        catch (Exception ex) when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException or XmlException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the parts declare more than <paramref name="maxBytes"/> together, judged from the zip's directory before
    /// anything is read: opening the package parses its relationships and content types whole, and a creator loads every
    /// part. A size declared smaller than its part inflates to is no way around it: the zip reader stops each part at its
    /// declared size. A stored part is read to its compressed size, so the larger of the two counts.
    /// </summary>
    private static bool Exceeds(byte[] source, long maxBytes)
    {
        using var zip = new ZipArchive(new MemoryStream(source, false), ZipArchiveMode.Read);
        var total = 0L;
        foreach (var entry in zip.Entries)
        {
            var size = Math.Max(entry.Length, entry.CompressedLength);
            // compared before it is added, so a forged size cannot overflow the total
            if (size < 0 || size > maxBytes - total)
            {
                return true;
            }
            total += size;
        }
        return false;
    }

    private static bool Find(byte[] source)
    {
        using var stream = new MemoryStream(source, false);
        using var doc = WordprocessingDocument.Open(stream, false);
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
        {
            return false;
        }

        OpenXmlPart[] parts = [mainPart, .. mainPart.HeaderParts, .. mainPart.FooterParts];
        return parts.Distinct().Any(PartOpensBlock);
    }

    /// <summary>
    /// Streams <paramref name="part"/> rather than loading it, so the scan holds no document model whatever the part
    /// holds. A paragraph's text is its visible text, as the backends that resolve the blocks read it: only its
    /// <see cref="Text"/> runs, so field codes and deleted revisions do not count, and not those of a text box inside it,
    /// whose paragraphs are read on their own.
    /// </summary>
    private static bool PartOpensBlock(OpenXmlPart part)
    {
        using var reader = new OpenXmlPartReader(part.GetStream(FileMode.Open, FileAccess.Read), part.Features,
            new OpenXmlPartReaderOptions { CloseStream = true });
        // the text of each open paragraph, innermost on top: a text box's paragraphs sit inside the one that anchors it;
        // null once it is too long to be a marker
        var paragraphs = new Stack<StringBuilder?>();
        while (reader.Read())
        {
            if (reader.ElementType == typeof(Paragraph))
            {
                if (reader.IsStartElement)
                {
                    paragraphs.Push(new StringBuilder());
                }
                else if (reader.IsEndElement && paragraphs.Pop() is { } text && ConditionalBlocks.OpensBlock(text.ToString()))
                {
                    return true;
                }
            }
            else if (reader.ElementType == typeof(Text) && reader.IsStartElement && paragraphs.TryPeek(out var text) && text != null)
            {
                var value = reader.GetText();
                if (text.Length + value.Length > MaxMarkerLength)
                {
                    paragraphs.Pop();
                    paragraphs.Push(null);
                }
                else
                {
                    text.Append(value);
                }
            }
        }
        return false;
    }
}
