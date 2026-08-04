using Regira.Office.Models;

namespace Regira.Office.PDF.Models.DTO;

public class HtmlToPdfInputDto
{
    public string HtmlContent { get; set; } = null!;
    public IDictionary<string, object?>? HtmlParameters { get; set; } = new Dictionary<string, object?>();

    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public PageSize Format { get; set; } = PageSize.A4;
    public float[] Margins { get; set; } = [10f, 10, 10, 10];
    public string? HeaderHtmlContent { get; set; }
    public int HeaderHeight { get; set; }
    public string? FooterHtmlContent { get; set; }
    public int FooterHeight { get; set; }
}