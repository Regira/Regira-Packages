using Regira.Office.Models;
using Regira.Utilities;

namespace Regira.Office.PDF.Models.DTO;

public class HtmlToPdfInputDto
{
    public string HtmlContent { get; set; } = null!;
    public IDictionary<string, object?>? HtmlParameters { get; set; } = new Dictionary<string, object?>();

    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public PageSize Format { get; set; } = PageSize.A4;
    /// <summary>
    /// Top, right, bottom and left, in units of 96 DPI (37.8 is 10 mm), as <see cref="HtmlInput"/> measures them at its
    /// default DPI. The default is 10 mm on every side.
    /// </summary>
    public float[] Margins { get; set; } = [DimensionsUtility.MmToPt(10f), DimensionsUtility.MmToPt(10f), DimensionsUtility.MmToPt(10f), DimensionsUtility.MmToPt(10f)];
    public string? HeaderHtmlContent { get; set; }
    public int HeaderHeight { get; set; }
    public string? FooterHtmlContent { get; set; }
    public int FooterHeight { get; set; }
}