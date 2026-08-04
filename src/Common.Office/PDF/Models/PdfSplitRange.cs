namespace Regira.Office.PDF.Models;

public record PdfSplitRange
{
    /// <summary>
    /// First page to include (first is 1)
    /// </summary>
    public int Start { get; set; }
    /// <summary>
    /// Last page to include
    /// </summary>
    public int? End { get; set; }
}