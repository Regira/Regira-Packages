using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Docnet.Core;
using Docnet.Core.Models;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using SkiaSharp;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using V = DocumentFormat.OpenXml.Vml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// What a produced .docx holds, read with the Open XML SDK rather than with the backend that wrote it: a backend's
/// own reader renders its input as a template again, and reads only the copies of the content it knows.
/// </summary>
/// <remarks>
/// The facts are the body's, apart from <see cref="Leftovers"/>, <see cref="Headers"/> and <see cref="Footers"/>.
/// Unlicensed output adds a banner paragraph to the body and marks headers and footers — Aspose's watermark is a
/// header picture too — so a scenario asserts on what must be present or absent, and on structure the banners leave
/// alone: tables, body pictures, sections. It counts no paragraphs.
/// </remarks>
public sealed record DocxFacts
{
    /// <summary>The body's visible text, a paragraph a line, tables and text boxes included.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string BodyText => string.Join("\n", Paragraphs.Select(paragraph => paragraph.Text));
    /// <summary>The body's paragraphs in document order, those in tables and text boxes included.</summary>
    public required IReadOnlyList<ParagraphFact> Paragraphs { get; init; }
    /// <summary>The body's top-level paragraphs that hold no text and no picture, apart from one that ends a section.</summary>
    public required int EmptyParagraphs { get; init; }
    /// <summary>The body's page breaks (<c>w:br w:type="page"</c>), tables and text boxes included.</summary>
    public required int PageBreaks { get; init; }
    /// <summary>The paragraphs of any story that still hold a tag (see <see cref="Docx.Leftovers"/>).</summary>
    public required IReadOnlyList<string> Leftovers { get; init; }
    /// <summary>The rows of each body table that is not nested in another table, in document order.</summary>
    public required IReadOnlyList<int> TableRows { get; init; }
    /// <summary>The pictures the body shows, in document order.</summary>
    public required IReadOnlyList<PictureFact> Pictures { get; init; }
    public required IReadOnlyList<SectionFact> Sections { get; init; }
    public required IReadOnlyList<StoryFact> Headers { get; init; }
    public required IReadOnlyList<StoryFact> Footers { get; init; }

    public static DocxFacts Read(IMemoryFile file)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(file.GetBytes()!), false);
        var main = doc.MainDocumentPart!;
        var body = main.Document!.Body!;
        var sections = body.Descendants<W.SectionProperties>().ToList();

        return new DocxFacts
        {
            Paragraphs = body.Descendants<W.Paragraph>().Select(ParagraphFact.Of).ToList(),
            EmptyParagraphs = body.Elements<W.Paragraph>().Count(paragraph =>
                paragraph.InnerText.Length == 0 && !paragraph.Descendants<W.Drawing>().Any() && !paragraph.Descendants<W.Picture>().Any()
                && !paragraph.Descendants<W.SectionProperties>().Any()),
            PageBreaks = body.Descendants<W.Break>().Count(pageBreak => pageBreak.Type?.Value == W.BreakValues.Page),
            Leftovers = Docx.Leftovers(file),
            TableRows = body.Descendants<W.Table>()
                .Where(table => !table.Ancestors<W.Table>().Any())
                .Select(table => table.Elements<W.TableRow>().Count())
                .ToList(),
            // DrawingML pictures, and VML ones, which a converted .doc and a VML fallback copy hold; a picture in both copies counts once
            Pictures = body.Descendants()
                .Select(element => element switch
                {
                    A.Blip blip => (Id: blip.Embed?.Value, Extent: DrawingExtent(blip)),
                    V.ImageData imageData => (Id: imageData.RelationshipId?.Value, Extent: VmlExtent(imageData)),
                    _ => (Id: null, Extent: null)
                })
                .Where(picture => picture.Id != null)
                .DistinctBy(picture => picture.Id)
                .Select(picture => main.GetPartById(picture.Id!) is ImagePart part
                    ? PictureFact.Of(part) with { ExtentWidth = picture.Extent?.Width, ExtentHeight = picture.Extent?.Height }
                    : null)
                .OfType<PictureFact>()
                .ToList(),
            Sections = sections.Select(SectionFact.Of).ToList(),
            Headers = sections.SelectMany(section => section.Elements<W.HeaderReference>())
                .Select(reference => StoryFact.Of(reference.Type?.InnerText, ((HeaderPart)main.GetPartById(reference.Id!)).Header))
                .ToList(),
            Footers = sections.SelectMany(section => section.Elements<W.FooterReference>())
                .Select(reference => StoryFact.Of(reference.Type?.InnerText, ((FooterPart)main.GetPartById(reference.Id!)).Footer))
                .ToList()
        };
    }

    private const double EmusPerPoint = 12700;

    /// <summary>A DrawingML picture's size in the text flow (<c>wp:extent</c>), in EMUs.</summary>
    private static (long Width, long Height)? DrawingExtent(A.Blip blip)
        => blip.Ancestors<W.Drawing>().FirstOrDefault()?.Descendants<DW.Extent>().FirstOrDefault() is { Cx: not null, Cy: not null } extent
            ? (extent.Cx.Value, extent.Cy.Value)
            : null;

    /// <summary>A VML picture's size, from its shape's <c>width</c> and <c>height</c> style, in EMUs.</summary>
    private static (long Width, long Height)? VmlExtent(V.ImageData imageData)
    {
        var style = (imageData.Parent as V.Shape)?.Style?.Value ?? "";
        return Length(style, "width") is { } width && Length(style, "height") is { } height
            ? ((long)Math.Round(width * EmusPerPoint), (long)Math.Round(height * EmusPerPoint))
            : null;

        // a CSS length in points
        static double? Length(string style, string property)
        {
            var match = System.Text.RegularExpressions.Regex.Match(style, $@"(?:^|;)\s*{property}\s*:\s*([\d.]+)\s*(pt|in|px|cm|mm)?", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success || !double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }
            return match.Groups[2].Value.ToLowerInvariant() switch
            {
                "in" => value * 72,
                "px" => value * 0.75,
                "cm" => value * 72 / 2.54,
                "mm" => value * 72 / 25.4,
                _ => value
            };
        }
    }
}

/// <param name="Text">The visible text: deleted text and field codes left out, a text box's text included</param>
/// <param name="Alignment">The paragraph's own <c>w:jc</c>, null when it sets none</param>
/// <param name="RunSizes">The font size each of its runs sets, in half-points; a run that sets none is left out</param>
public sealed record ParagraphFact(string Text, string? Alignment, IReadOnlyList<int> RunSizes)
{
    public static ParagraphFact Of(W.Paragraph paragraph)
        => new(
            string.Concat(paragraph.Descendants<W.Text>().Select(text => text.Text)),
            paragraph.ParagraphProperties?.Justification?.Val?.InnerText,
            paragraph.Descendants<W.Run>()
                .Where(run => run.Ancestors<W.Paragraph>().First() == paragraph)
                .Select(run => run.RunProperties?.FontSize?.Val?.Value)
                .OfType<string>()
                .Select(int.Parse)
                .ToList());
}

/// <param name="Width">In pixels</param>
/// <param name="Height">In pixels</param>
/// <param name="Sha1">Of the picture's bytes, hexadecimal</param>
/// <param name="ExtentWidth">The width the document shows it at, in EMUs; null when the document does not say</param>
/// <param name="ExtentHeight">The height the document shows it at, in EMUs; null when the document does not say</param>
public sealed record PictureFact(int Width, int Height, string Sha1, long? ExtentWidth = null, long? ExtentHeight = null)
{
    public static PictureFact Of(ImagePart part)
    {
        using var stream = part.GetStream();
        using var bytes = new MemoryStream();
        stream.CopyTo(bytes);
        return Of(bytes.ToArray());
    }

    public static PictureFact Of(byte[] bytes)
    {
        using var codec = SKCodec.Create(new MemoryStream(bytes));
        return new PictureFact(codec?.Info.Width ?? 0, codec?.Info.Height ?? 0, Facts.Sha1(bytes));
    }
}

/// <param name="PageWidth">In twips; null when the section does not say</param>
/// <param name="PageHeight">In twips; null when the section does not say</param>
/// <param name="TitlePage">Whether the section's first page has headers and footers of its own</param>
/// <param name="Start">How the section starts: <c>nextPage</c> unless it says otherwise</param>
/// <param name="TextWidth">The page's width within its left and right margins, in twips; null when the section does not say</param>
public sealed record SectionFact(int? PageWidth, int? PageHeight, bool TitlePage, string Start, int? TextWidth)
{
    public static SectionFact Of(W.SectionProperties section)
    {
        var size = section.GetFirstChild<W.PageSize>();
        var margins = section.GetFirstChild<W.PageMargin>();
        return new SectionFact(
            (int?)size?.Width?.Value,
            (int?)size?.Height?.Value,
            section.GetFirstChild<W.TitlePage>() is { } titlePage && (titlePage.Val?.Value ?? true),
            section.GetFirstChild<W.SectionType>()?.Val?.InnerText ?? "nextPage",
            size?.Width?.Value is { } width && margins is { Left: not null, Right: not null }
                ? (int)width - (int)margins.Left.Value - (int)margins.Right.Value
                : null);
    }
}

/// <param name="Type"><c>default</c>, <c>first</c> or <c>even</c></param>
/// <param name="Text">The story's text, a paragraph a line</param>
public sealed record StoryFact(string Type, string Text)
{
    public static StoryFact Of(string? type, OpenXmlElement? story)
        => new(type ?? "default", string.Join("\n", story?.Descendants<W.Paragraph>().Select(paragraph => paragraph.InnerText) ?? []));
}

/// <summary>What a produced PDF holds, read with Docnet at scale 1, so a page measures in points.</summary>
public sealed record PdfFacts(int Pages, IReadOnlyList<(int Width, int Height)> PageSizes, IReadOnlyList<string> PageTexts)
{
    public static PdfFacts Read(IMemoryFile file) => Read(file.GetBytes()!);

    public static PdfFacts Read(byte[] pdf)
    {
        using var reader = DocLib.Instance.GetDocReader(pdf, new PageDimensions(1d));
        var pages = Enumerable.Range(0, reader.GetPageCount())
            .Select(i =>
            {
                using var page = reader.GetPageReader(i);
                return (Size: (page.GetPageWidth(), page.GetPageHeight()), Text: page.GetText());
            })
            .ToList();
        return new PdfFacts(pages.Count, pages.Select(page => page.Size).ToList(), pages.Select(page => page.Text).ToList());
    }
}

/// <param name="Ink">The share of sampled pixels darker than paper, 0 to 1: about 0 for a blank page</param>
public sealed record ImageFacts(int Width, int Height, double Ink)
{
    public static ImageFacts Read(IMemoryFile file)
    {
        using var bitmap = SKBitmap.Decode(file.GetBytes()!) ?? throw new FormatException("Not an image SkiaSharp can decode.");
        // a coarse grid is enough to tell a rendered page from a blank one
        var stepX = Math.Max(1, bitmap.Width / 40);
        var stepY = Math.Max(1, bitmap.Height / 60);
        int sampled = 0, inked = 0;
        for (var y = 0; y < bitmap.Height; y += stepY)
        {
            for (var x = 0; x < bitmap.Width; x += stepX)
            {
                var pixel = bitmap.GetPixel(x, y);
                sampled++;
                if (0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue < 200)
                {
                    inked++;
                }
            }
        }
        return new ImageFacts(bitmap.Width, bitmap.Height, (double)inked / sampled);
    }
}

internal static class Facts
{
    /// <summary>An A3 page in points (297 × 420 mm), as a PDF measures it.</summary>
    public static readonly (int Width, int Height) A3 = (842, 1191);
    /// <summary>An A4 page in points (210 × 297 mm), as a PDF measures it.</summary>
    public static readonly (int Width, int Height) A4 = (595, 842);
    /// <summary>An A4 page in twips, as a .docx section stores it.</summary>
    public static readonly (int Width, int Height) A4Twips = (11906, 16838);

    public static string Sha1(byte[] bytes) => Convert.ToHexString(SHA1.HashData(bytes));

    /// <summary>
    /// The format a file's content is in, read from its first bytes and, for a package, from the content type of its
    /// main part; null when it is none of the formats a Word backend writes.
    /// </summary>
    public static FileFormat? Sniff(byte[] bytes)
    {
        if (StartsWith(bytes, "%PDF-")) return FileFormat.Pdf;
        if (StartsWith(bytes, "{\\rtf")) return FileFormat.Rtf;
        if (bytes is [0xD0, 0xCF, 0x11, 0xE0, ..]) return FileFormat.Doc;
        if (bytes is [0x89, (byte)'P', (byte)'N', (byte)'G', ..]) return FileFormat.Png;
        if (bytes is [0xFF, 0xD8, 0xFF, ..]) return FileFormat.Jpeg;
        if (bytes is [(byte)'P', (byte)'K', ..])
        {
            using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
            if (zip.GetEntry("mimetype") is { } mimetype)
            {
                using var reader = new StreamReader(mimetype.Open());
                return reader.ReadToEnd().Trim() switch
                {
                    "application/vnd.oasis.opendocument.text" => FileFormat.Odt,
                    "application/epub+zip" => FileFormat.EPub,
                    _ => null
                };
            }
            return MainPartFormat(zip);
        }
        var head = Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 4096));
        return head.Contains("<html", StringComparison.OrdinalIgnoreCase) ? FileFormat.Html : null;
    }

    private static bool StartsWith(byte[] bytes, string prefix)
        => bytes.Length >= prefix.Length && Encoding.ASCII.GetString(bytes, 0, prefix.Length) == prefix;

    private static readonly Dictionary<string, FileFormat> MainPartContentTypes = new()
    {
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"] = FileFormat.Docx,
        ["application/vnd.openxmlformats-officedocument.wordprocessingml.template.main+xml"] = FileFormat.Dotx,
        ["application/vnd.ms-word.document.macroEnabled.main+xml"] = FileFormat.Docm,
        ["application/vnd.ms-word.template.macroEnabledTemplate.main+xml"] = FileFormat.Dotm
    };

    private static FileFormat? MainPartFormat(ZipArchive zip)
    {
        using var stream = zip.GetEntry("[Content_Types].xml")?.Open();
        if (stream == null)
        {
            return null;
        }
        return XDocument.Load(stream).Root!.Elements()
            .Where(element => element.Name.LocalName == "Override")
            .Select(element => (string?)element.Attribute("ContentType"))
            .Select(contentType => contentType != null && MainPartContentTypes.TryGetValue(contentType, out var format) ? format : (FileFormat?)null)
            .FirstOrDefault(format => format != null);
    }
}
