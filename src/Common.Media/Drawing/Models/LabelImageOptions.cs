using Regira.Media.Drawing.Constants;

namespace Regira.Media.Drawing.Models;

public record LabelImageOptions
{
    public string Text { get; set; } = null!;
    public int? FontSize { get; set; } = LabelImageDefaults.FontSize;
    public int? Padding { get; set; } = LabelImageDefaults.Padding;
    public string? FontName { get; set; } = LabelImageDefaults.FontName;
    public Color? TextColor { get; set; } = LabelImageDefaults.TextColor;
    public Color? BackgroundColor { get; set; } = LabelImageDefaults.BackgroundColor;

    public static implicit operator LabelImageOptions(string content) => new() { Text = content };
}