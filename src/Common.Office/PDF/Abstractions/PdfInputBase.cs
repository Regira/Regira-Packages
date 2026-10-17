using Regira.Office.Models;
using Regira.Utilities;

namespace Regira.Office.PDF.Abstractions;

public abstract class PdfInputBase(int dpi = DimensionsUtility.DPI.DEFAULT)
{
    public int DPI { get; set; } = dpi;
    public PageOrientation Orientation { get; set; } = PageOrientation.Portrait;
    public PageSize Format { get; set; } = PageSize.A4;

    /// <summary>
    /// Margins in units of the input's <see cref="DPI"/> (PDF points at 72; at the default 96, 37.8 is 10 mm).
    /// </summary>
    public Margins Margins { get; set; } = DimensionsUtility.MmToPt(10f, dpi);
}