using Regira.Office.Models;
using Regira.Utilities;

namespace Regira.Office.PDF.Abstractions;

public abstract class PdfInputBase(int dpi = DimensionsUtility.DPI.DEFAULT)
{
    public int DPI { get; set; } = dpi;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public PageSize Format { get; set; } = PageSize.A4;

    /// <summary>
    /// Width of margins in points
    /// </summary>
    public Margins Margins { get; set; } = DimensionsUtility.MmToPt(10f, dpi);
}