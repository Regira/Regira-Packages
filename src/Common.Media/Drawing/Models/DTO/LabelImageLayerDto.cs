using Regira.Dimensions;

namespace Regira.Media.Drawing.Models.DTO;

public record LabelImageLayerDto
{
    public record LabelOptionsDto
    {
        public LengthUnit? DimensionUnit { get; set; }
        public float? FontSize { get; set; }
        public float? Padding { get; set; }
        public int? Dpi { get; set; }
        public string? FontName { get; set; }
        public string? TextColor { get; set; }
        public string? BackgroundColor { get; set; }
    }

    public string Text { get; set; } = null!;
    public LabelOptionsDto? LabelOptions { get; set; }
    public ImageLayerOptionsDto? DrawOptions { get; set; }
}