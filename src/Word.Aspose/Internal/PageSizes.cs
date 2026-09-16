using Regira.Office.Models;

namespace Regira.Office.Word.Aspose.Internal;

/// <summary>
/// ISO 216 A-series paper sizes. Aspose's <c>PaperSize</c> names only A3, A4 and A5, so page sizes are
/// set as a width and height instead.
/// </summary>
internal static class PageSizes
{
    /// <summary>
    /// Portrait width and height in points.
    /// </summary>
    public static (double Width, double Height) Points(PageSize size)
    {
        var (width, height) = Millimetres(size);
        return (ToPoints(width), ToPoints(height));
    }

    private static (double Width, double Height) Millimetres(PageSize size)
        => size switch
        {
            PageSize.A0 => (841, 1189),
            PageSize.A1 => (594, 841),
            PageSize.A2 => (420, 594),
            PageSize.A3 => (297, 420),
            PageSize.A4 => (210, 297),
            PageSize.A5 => (148, 210),
            PageSize.A6 => (105, 148),
            PageSize.A7 => (74, 105),
            PageSize.A8 => (52, 74),
            PageSize.A9 => (37, 52),
            PageSize.A10 => (26, 37),
            _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
        };

    // twentieths of a point, as Word stores page sizes, so A4 comes out as Word's own 595.3 × 841.9
    private static double ToPoints(double millimetres)
        => Math.Round(millimetres * 1440 / 25.4, MidpointRounding.AwayFromZero) / 20;
}
