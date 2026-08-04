using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;

namespace Regira.Media.Drawing.Models;

public record CanvasImageOptions
{
    public ImageSize Size { get; set; }
    public Color? BackgroundColor { get; set; }
    public ImageFormat? ImageFormat { get; set; }

    public static implicit operator CanvasImageOptions(ImageSize size) => new() { Size = size };
}