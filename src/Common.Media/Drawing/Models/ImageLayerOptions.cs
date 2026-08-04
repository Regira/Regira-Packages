using Regira.Media.Drawing.Constants;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;

namespace Regira.Media.Drawing.Models;

public record ImageLayerOptions
{
    public ImageSize? Size { get; set; }
    public int Margin { get; set; } = ImageLayerDefaults.Margin;
    public ImagePosition Position { get; set; } = ImageLayerDefaults.Position;
    public ImageEdgeOffset? Offset { get; set; } = ImageLayerDefaults.Offset;
    public int Rotation { get; set; } = ImageLayerDefaults.Rotation;
    public float Opacity { get; set; } = ImageLayerDefaults.Opacity;
}