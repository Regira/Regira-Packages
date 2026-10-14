using Regira.Office.PDF.Abstractions;
using Regira.Utilities;

namespace Regira.Office.PDF.Models;

public class HtmlInput(int dpi = DimensionsUtility.DPI.DEFAULT) : PdfInputBase(dpi)
{
    /// <summary>
    /// The HTML to convert. A backend fetches every URL it names from the host it runs on, so remove or check the URLs
    /// of HTML built from user input.
    /// </summary>
    public string? HtmlContent { get; set; }
    /// <summary>
    /// HTML fragment repeated at the top of every page, in a band of <see cref="HeaderHeight"/> below the top margin:
    /// the band's markup (a <c>&lt;div&gt;</c>), not a document. The page's stylesheets do not reach it, so it carries
    /// its own styles; <c>pageNumber</c> and <c>totalPages</c> spans are filled where the backend supports them.
    /// </summary>
    public string? HeaderHtmlContent { get; set; }
    /// <summary>
    /// Height of header in mm
    /// </summary>
    public int? HeaderHeight { get; set; }
    /// <summary>
    /// HTML fragment repeated at the bottom of every page, in a band of <see cref="FooterHeight"/> above the bottom
    /// margin: the band's markup (a <c>&lt;div&gt;</c>), not a document. The page's stylesheets do not reach it, so it
    /// carries its own styles; <c>pageNumber</c> and <c>totalPages</c> spans are filled where the backend supports them.
    /// </summary>
    public string? FooterHtmlContent { get; set; }
    /// <summary>
    /// Height of footer in mm
    /// </summary>
    public int? FooterHeight { get; set; }
}