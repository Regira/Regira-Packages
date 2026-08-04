using Regira.Dimensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.Utilities;
using Regira.Utilities;

namespace Regira.Office.PDF.Models;

public class ImagesInput(int dpi = DimensionsUtility.DPI.DEFAULT) : PdfInputBase(dpi)
{
    public ICollection<byte[]> Images { get; set; } = new List<byte[]>();
    public Size2D MaxDimensions => PageSizeUtility.GetPageSizeDimension(Format, LengthUnit.Points, Orientation, DPI);
}