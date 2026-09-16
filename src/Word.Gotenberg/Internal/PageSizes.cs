using Regira.Office.Models;

namespace Regira.Office.Word.Gotenberg.Internal;

/// <summary>
/// ISO 216 A-series paper sizes.
/// </summary>
internal static class PageSizes
{
    /// <summary>
    /// Portrait width and height in millimetres.
    /// </summary>
    public static (double Width, double Height) Millimetres(PageSize size)
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

    /// <summary>
    /// Portrait width and height in twentieths of a point, the unit of <c>w:pgSz</c>.
    /// A4 comes out as 11906 × 16838, the values Word itself writes.
    /// </summary>
    public static (uint Width, uint Height) Twips(PageSize size)
    {
        var (width, height) = Millimetres(size);
        return (ToTwips(width), ToTwips(height));
    }

    private static uint ToTwips(double millimetres)
        => (uint)Math.Round(millimetres * 1440 / 25.4, MidpointRounding.AwayFromZero);
}
