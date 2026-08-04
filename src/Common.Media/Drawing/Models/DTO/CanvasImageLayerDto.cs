using Regira.Media.Drawing.Enums;

namespace Regira.Media.Drawing.Models.DTO;

public record CanvasImageLayerDto : CanvasImageDto
{
    public record CanvasDrawOptionsDto
    {
        public float? Margin { get; set; }
        public ImagePosition? Position { get; set; }
        public float? Top { get; set; }
        public float? Left { get; set; }
        public float? Bottom { get; set; }
        public float? Right { get; set; }
        public int? Rotation { get; set; }
        public float? Opacity { get; set; }
    }

    public CanvasDrawOptionsDto? DrawOptions { get; set; } = null!;
}