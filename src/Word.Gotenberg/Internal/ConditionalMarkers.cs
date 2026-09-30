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
    /// The most characters the scan reads of a part. Every OOXML conversion is scanned in-process: a part that
    /// decompresses to more — a zip bomb, or a document far larger than any template — stops the scan, and the document
    /// goes to Gotenberg as it is.
    /// </summary>
    internal const long MaxCharactersInPart = 32 * 1024 * 1024;

    /// <summary>
    /// Whether the body, a header or a footer holds a paragraph that opens a block — what makes a document use blocks,
    /// as the creators decide it. Nothing else does, marker text among other text included: that document converts as
    /// it is. A package that cannot be read holds none, a part that reaches <see cref="MaxCharactersInPart"/> before a
    /// block included: it goes to Gotenberg as it is, which reports what is wrong with it.
    /// </summary>
    public static bool Any(byte[] source)
        => Any(source, MaxCharactersInPart);

    internal static bool Any(byte[] source, long maxCharactersInPart)
    {
        try
        {
            return Find(source, maxCharactersInPart);
        }
        catch (Exception ex)
            when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException or XmlException)
        {
            return false;
        }
    }

    private static bool Find(byte[] source, long maxCharactersInPart)
    {
        using var stream = new MemoryStream(source, false);
        using var doc = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = maxCharactersInPart });
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
        {
            return false;
        }

        IEnumerable<OpenXmlPart> parts = [mainPart, .. mainPart.HeaderParts, .. mainPart.FooterParts];
        return parts.Any(PartOpensBlock);
    }

    /// <summary>
    /// Streams <paramref name="part"/> rather than loading it, so the scan holds no document model whatever the part
    /// holds, and stops at the first paragraph that opens a block. A paragraph's text is its visible text, as the
    /// backends that resolve the blocks read it: only its <see cref="Text"/> runs, so field codes and deleted revisions
    /// do not count, and not those of a text box inside it, whose paragraphs are read on their own.
    /// </summary>
    private static bool PartOpensBlock(OpenXmlPart part)
    {
        using var reader = OpenXmlReader.Create(part);
        // the text of each open paragraph, innermost on top: a text box's paragraphs sit inside the one that anchors it
        var paragraphs = new Stack<StringBuilder>();
        while (reader.Read())
        {
            if (reader.ElementType == typeof(Paragraph))
            {
                if (reader.IsStartElement)
                {
                    paragraphs.Push(new StringBuilder());
                }
                else if (reader.IsEndElement && ConditionalBlocks.OpensBlock(paragraphs.Pop().ToString()))
                {
                    return true;
                }
            }
            else if (reader.ElementType == typeof(Text) && reader.IsStartElement && paragraphs.TryPeek(out var text))
            {
                text.Append(reader.GetText());
            }
        }
        return false;
    }
}
