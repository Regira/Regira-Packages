using Regira.Dimensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Utilities;

namespace Regira.Media.Drawing.Constants;

public static class ImageLayerDefaults
{
    public static LengthUnit DimensionUnit { get; set; } = LengthUnit.Points;
    public static int Margin { get; set; } = 0;
    public static ImagePosition Position { get; set; } = ImagePosition.Absolute;
    public static ImageEdgeOffset Offset { get; set; } = new(0, 0);
    public static int Rotation { get; set; } = 0;
    public static float Opacity { get; set; } = 1;
    public static int Dpi { get; set; } = DimensionsUtility.DPI.DEFAULT;
}