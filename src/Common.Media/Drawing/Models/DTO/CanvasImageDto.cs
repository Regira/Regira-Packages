using Regira.Dimensions;
using Regira.Media.Drawing.Enums;

namespace Regira.Media.Drawing.Models.DTO;

public record CanvasImageDto
{
    public LengthUnit? DimensionUnit { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public int? Dpi { get; set; }
    public string? BackgroundColor { get; set; }
    public ImageFormat? ImageFormat { get; set; }
}