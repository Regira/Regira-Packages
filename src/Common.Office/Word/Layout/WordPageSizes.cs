using Regira.Office.Models;

namespace Regira.Office.Word.Layout;

/// <summary>
/// The ISO 216 A-series sizes of <see cref="PageSizes"/>, in the units a Word page is set in. Word stores a page size in
/// twentieths of a point, so A4 comes out as Word's own 11906 × 16838 twips, 595.3 × 841.9 points.
/// </summary>
internal static class WordPageSizes
{
    // PageSizes.Mm builds its table on every read; the table never changes
    private static readonly PageSizes Millimetres = PageSizes.Mm;

    /// <summary>Portrait width and height in twentieths of a point, the unit of <c>w:pgSz</c>.</summary>
    public static (uint Width, uint Height) Twips(PageSize size)
    {
        var millimetres = Millimetres[size];
        return (ToTwips(millimetres.Width), ToTwips(millimetres.Height));
    }

    /// <summary>Portrait width and height in points.</summary>
    public static (double Width, double Height) Points(PageSize size)
    {
        var (width, height) = Twips(size);
        return (width / 20d, height / 20d);
    }

    private static uint ToTwips(double millimetres)
        => (uint)Math.Round(millimetres * 1440 / 25.4, MidpointRounding.AwayFromZero);
}
