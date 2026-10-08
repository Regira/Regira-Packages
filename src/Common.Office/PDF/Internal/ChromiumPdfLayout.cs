using System.Globalization;
using Regira.Dimensions;
using Regira.Office.PDF.Models;
using Regira.Office.Utilities;
using Regira.Utilities;

namespace Regira.Office.PDF.Internal;

/// <summary>
/// An <see cref="HtmlInput"/>'s page settings as Chromium's print-to-PDF takes them: CSS lengths in millimetres, and
/// the header and footer as templates.
/// <para>
/// Chromium draws a header or footer template inside the page margin, so the top margin grows by the header's height
/// and the bottom margin by the footer's, and the template places its content in that band: below the top margin or
/// above the bottom one, between the left and right margins. A header without a height takes 45 pt and a footer 35 pt,
/// SelectPdf's defaults.
/// </para>
/// </summary>
internal sealed record ChromiumPdfLayout(
    string Width,
    string Height,
    string MarginTop,
    string MarginRight,
    string MarginBottom,
    string MarginLeft,
    string? HeaderTemplate,
    string? FooterTemplate)
{
    // in millimetres, from PDF points (72 to the inch)
    private static readonly float DefaultHeaderHeight = DimensionsUtility.PtToMm(45f, DimensionsUtility.DPI.MAC_PPI);
    private static readonly float DefaultFooterHeight = DimensionsUtility.PtToMm(35f, DimensionsUtility.DPI.MAC_PPI);
    // Chromium draws its own date, title and URL when only one of the two templates is given
    private const string EmptyTemplate = "<span></span>";

    public bool DisplayHeaderFooter => HeaderTemplate != null || FooterTemplate != null;

    public static ChromiumPdfLayout From(HtmlInput input)
    {
        // the orientation is applied to the size here, so Chromium's landscape flag stays off
        var page = PageSizeUtility.GetPageSizeDimension(input.Format, LengthUnit.Millimeters, input.Orientation);
        // HtmlInput measures its margins in units of its DPI
        var top = DimensionsUtility.PtToMm(input.Margins.Top, input.DPI);
        var right = DimensionsUtility.PtToMm(input.Margins.Right, input.DPI);
        var bottom = DimensionsUtility.PtToMm(input.Margins.Bottom, input.DPI);
        var left = DimensionsUtility.PtToMm(input.Margins.Left, input.DPI);

        string? header = null;
        var headerBand = top;
        if (input.HeaderHtmlContent != null)
        {
            var height = input.HeaderHeight ?? DefaultHeaderHeight;
            headerBand = top + height;
            header = Band(input.HeaderHtmlContent, $"top:{Mm(top)}", height, right, left);
        }

        string? footer = null;
        var footerBand = bottom;
        if (input.FooterHtmlContent != null)
        {
            var height = input.FooterHeight ?? DefaultFooterHeight;
            footerBand = bottom + height;
            footer = Band(input.FooterHtmlContent, $"bottom:{Mm(bottom)}", height, right, left);
        }

        if (header != null || footer != null)
        {
            header ??= EmptyTemplate;
            footer ??= EmptyTemplate;
        }

        return new ChromiumPdfLayout(
            Mm(page.Width), Mm(page.Height),
            Mm(headerBand), Mm(right), Mm(footerBand), Mm(left),
            header, footer);
    }

    // A template is laid out in a box as wide as the page and as high as the margin it sits in, which Chromium pads
    // itself; the band is placed in that box absolutely
    private static string Band(string html, string edge, float height, float right, float left)
        => $"<div style=\"position:absolute;{edge};left:{Mm(left)};right:{Mm(right)};height:{Mm(height)};overflow:hidden;font-size:16px\">{html}</div>";

    private static string Mm(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture) + "mm";
}
