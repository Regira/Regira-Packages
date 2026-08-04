using Regira.Dimensions;
using Regira.Office.Models;
using Regira.Utilities;

namespace Regira.Office.Utilities;

public static class PageSizeUtility
{
    public static Size2D GetPageSizeDimension(PageSize format, LengthUnit unit = LengthUnit.Millimeters, PageOrientation orientation = PageOrientation.Portrait, int dpi = DimensionsUtility.DPI.DEFAULT)
    {
        var entry = PageSizes.Mm[format];

        float[] portraitDimension;
        switch (unit)
        {
            case LengthUnit.Inches:
                portraitDimension = MmToInches(entry);
                break;
            case LengthUnit.Points:
                portraitDimension = MmToPoints(entry, dpi);
                break;
            default:
                portraitDimension = entry;
                break;
        }

        if (orientation == PageOrientation.Landscape)
        {
            portraitDimension = Enumerable
                .Reverse(portraitDimension)
                .ToArray();
        }

        return portraitDimension;
    }

    public static Size2D MmToInches(Size2D size)
    {
        return size / DimensionsUtility.MM_PER_INCH;
    }
    public static Size2D InchesToMm(Size2D size)
    {
        return size * DimensionsUtility.MM_PER_INCH;
    }

    public static Size2D MmToPoints(Size2D size, int dpi = DimensionsUtility.DPI.DEFAULT)
    {
        var inches = MmToInches(size);
        return InchesToPoints(inches, dpi);
    }
    public static Size2D InchesToPoints(Size2D size, int dpi = DimensionsUtility.DPI.DEFAULT)
    {
        return size * dpi;
    }
}