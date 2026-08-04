using Regira.Dimensions;
using Regira.Utilities;

namespace Regira.Office.Models;

public class PageSizes
{
    public LengthUnit Unit { get; }
    private readonly IDictionary<PageSize, Size2D> _sizes;
    public PageSizes(LengthUnit unit = LengthUnit.Millimeters)
    {
        Unit = unit;
        _sizes = SizesMm.ToDictionary(
            k => k.Key,
            v =>
            {
                switch (unit)
                {
                    case LengthUnit.Inches:
                        return (Size2D)new[]
                        {
                            DimensionsUtility.MmToIn(v.Value.Width ),
                            DimensionsUtility.MmToIn(v.Value.Height)
                        };
                    case LengthUnit.Millimeters:
                        return v.Value;
                    default:
                        throw new NotSupportedException($"Unit {unit.ToString()} is not supported");
                }
            });
    }

    public Size2D this[PageSize format] => _sizes[format];

    #region HardcodedFormats
    public Size2D A0 => _sizes[PageSize.A0];
    public Size2D A1 => _sizes[PageSize.A1];
    public Size2D A2 => _sizes[PageSize.A2];
    public Size2D A3 => _sizes[PageSize.A3];
    public Size2D A4 => _sizes[PageSize.A4];
    public Size2D A5 => _sizes[PageSize.A5];
    public Size2D A6 => _sizes[PageSize.A6];
    public Size2D A7 => _sizes[PageSize.A7];
    public Size2D A8 => _sizes[PageSize.A8];
    public Size2D A9 => _sizes[PageSize.A9];
    public Size2D A10 => _sizes[PageSize.A10];
    #endregion

    #region Static
    // build page size dictionary
    private static IDictionary<PageSize, Size2D> SizesMm => Enum
        .GetNames(typeof(PageSize))
        .Aggregate(new Dictionary<PageSize, Size2D>(),
            (sizes, pageSizeName) =>
            {
                var index = int.Parse(pageSizeName.Substring(1));
                var pageSize = (PageSize)Enum.Parse(typeof(PageSize), pageSizeName, true);
                var dim = new[] { 841f, 1189 };
                for (var i = 0; i < index; i++)
                {
                    dim = [(float)Math.Floor(dim[1] / 2), dim[0]];
                }
                sizes[pageSize] = dim;
                return sizes;
            });

    /// <summary>
    /// Page sizes in mm
    /// </summary>
    public static PageSizes Mm => new();
    /// <summary>
    /// Page sizes in inches
    /// </summary>
    public static PageSizes Inches => new(LengthUnit.Inches);
    #endregion
}