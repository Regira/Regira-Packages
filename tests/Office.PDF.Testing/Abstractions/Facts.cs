using System.Text.Json.Serialization;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Enums;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;

namespace Office.PDF.Testing.Abstractions;

/// <summary>
/// What a PDF holds, read with the PdfPig library rather than with the backend that wrote it.
/// </summary>
/// <remarks>
/// For the PdfPig backend the reader shares a parser with the writer, so a scenario compares with the facts of its
/// input or with a fixed expectation, never with what the backend itself reads: a page the writer drops or reorders
/// still shows.
/// </remarks>
public sealed record PdfFacts(IReadOnlyList<PageFact> Pages)
{
    [JsonIgnore]
    public int PageCount => Pages.Count;
    [JsonIgnore]
    public IReadOnlyList<string> PageTexts => Pages.Select(page => page.Text).ToList();

    public static PdfFacts Read(IMemoryFile file) => Read(file.GetBytes()!);

    public static PdfFacts Read(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return new PdfFacts(document.GetPages().Select(PageFact.Of).ToList());
    }
}

/// <summary>A page, measured in points from its top-left corner.</summary>
/// <param name="Words">
/// The words whose centre lies on the page, in the order the page draws them. A PDF can draw more than its page
/// shows — SelectPdf draws the whole document on every page and clips it — and only what lies on the page counts.
/// </param>
/// <param name="Images">The bounds of each image the page draws</param>
public sealed record PageFact(double Width, double Height, [property: JsonIgnore] IReadOnlyList<WordFact> Words, IReadOnlyList<Box> Images)
{
    /// <summary>The page's words, space-joined.</summary>
    public string Text => string.Join(" ", Words.Select(word => word.Text));

    /// <summary>The first word on the page that reads <paramref name="text"/>; the assertion fails when there is none.</summary>
    public WordFact Word(string text)
    {
        var word = Words.FirstOrDefault(word => word.Text == text);
        Assert.That(word, Is.Not.Null, $"'{text}' is not on the page: {Text}");
        return word!;
    }

    public static PageFact Of(Page page)
    {
        var width = page.Width;
        var height = page.Height;
        return new PageFact(
            Math.Round(width, 2),
            Math.Round(height, 2),
            page.GetWords()
                .Select(word => new WordFact(word.Text, Box.Of(word.BoundingBox, height)))
                .Where(word => word.Box.CentreX >= 0 && word.Box.CentreX <= width && word.Box.CentreY >= 0 && word.Box.CentreY <= height)
                .ToList(),
            page.GetImages().Select(image => Box.Of(image.BoundingBox, height)).ToList());
    }
}

public sealed record WordFact(string Text, Box Box);

/// <summary>A rectangle on a page, in points from the page's top-left corner.</summary>
public sealed record Box(double Left, double Top, double Right, double Bottom)
{
    [JsonIgnore]
    public double CentreX => (Left + Right) / 2;
    [JsonIgnore]
    public double CentreY => (Top + Bottom) / 2;

    /// <summary>A PDF rectangle, whose coordinates start at the page's bottom-left corner.</summary>
    public static Box Of(PdfRectangle rectangle, double pageHeight)
        => new(Math.Round(rectangle.Left, 2), Math.Round(pageHeight - rectangle.Top, 2), Math.Round(rectangle.Right, 2), Math.Round(pageHeight - rectangle.Bottom, 2));
}

/// <param name="Format">The format the bytes are in, read from their signature rather than from what the image declares</param>
/// <param name="Ink">The share of pixels darker than paper, 0 to 1: 0 for a blank page</param>
public sealed record ImageFacts(ImageFormat? Format, int Width, int Height, double Ink)
{
    public static ImageFacts Read(IMemoryFile file)
    {
        var bytes = file.GetBytes()!;
        using var bitmap = SKBitmap.Decode(bytes) ?? throw new FormatException("Not an image SkiaSharp can decode.");
        // every pixel: a sampling grid steps over the thin strokes of a sharp render
        var pixels = bitmap.Pixels;
        // a transparent pixel shows the paper
        var inked = pixels.Count(pixel => pixel.Alpha > 127 && 0.299 * pixel.Red + 0.587 * pixel.Green + 0.114 * pixel.Blue < 200);
        return new ImageFacts(Sniff(bytes), bitmap.Width, bitmap.Height, Math.Round((double)inked / pixels.Length, 4));
    }

    /// <summary>The image format a file's first bytes are the signature of; null when they are none of these.</summary>
    public static ImageFormat? Sniff(byte[] bytes) => bytes switch
    {
        [0x89, (byte)'P', (byte)'N', (byte)'G', ..] => ImageFormat.Png,
        [0xFF, 0xD8, 0xFF, ..] => ImageFormat.Jpeg,
        [(byte)'G', (byte)'I', (byte)'F', (byte)'8', ..] => ImageFormat.Gif,
        [(byte)'B', (byte)'M', ..] => ImageFormat.Bmp,
        [(byte)'I', (byte)'I', 0x2A, 0x00, ..] or [(byte)'M', (byte)'M', 0x00, 0x2A, ..] => ImageFormat.Tiff,
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', _, _, _, _, (byte)'W', (byte)'E', (byte)'B', (byte)'P', ..] => ImageFormat.Webp,
        _ => null
    };
}
