using System.Xml;
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
    /// The most characters a part may hold for the scan to read it. Every OOXML conversion is scanned in-process, and a
    /// part is loaded whole: one that decompresses to more — a zip bomb, or a document far larger than any template —
    /// is not read, and goes to Gotenberg as it is.
    /// </summary>
    internal const long MaxCharactersInPart = 32 * 1024 * 1024;

    /// <summary>
    /// Whether the body, a header or a footer holds a paragraph that opens a block — what makes a document use blocks,
    /// as the creators decide it. Nothing else does, marker text among other text included: that document converts as
    /// it is. A package that cannot be read holds none, a part beyond <see cref="MaxCharactersInPart"/> included: it
    /// goes to Gotenberg as it is, which reports what is wrong with it.
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

        var roots = new List<DocumentFormat.OpenXml.OpenXmlElement?> { mainPart.Document?.Body };
        roots.AddRange(mainPart.HeaderParts.Select(part => part.Header));
        roots.AddRange(mainPart.FooterParts.Select(part => part.Footer));

        return roots
            .OfType<DocumentFormat.OpenXml.OpenXmlElement>()
            .SelectMany(root => root.Descendants<Paragraph>())
            .Any(paragraph => ConditionalBlocks.OpensBlock(GetOwnText(paragraph)));
    }

    /// <summary>
    /// The paragraph's visible text, as the backends that resolve the blocks read it: only its <see cref="Text"/>
    /// runs, so field codes and deleted revisions do not count, and not those of a text box inside it, whose
    /// paragraphs are read on their own.
    /// </summary>
    private static string GetOwnText(Paragraph paragraph)
        => string.Concat(paragraph.Descendants<Text>()
            .Where(text => text.Ancestors<Paragraph>().First() == paragraph)
            .Select(text => text.Text));
}
