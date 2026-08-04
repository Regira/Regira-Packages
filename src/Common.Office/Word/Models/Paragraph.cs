using Regira.Media.Drawing.Models;

namespace Regira.Office.Word.Models;

public record Paragraph
{
    public string? Text { get; set; }
    public WordImage? Image { get; set; }
    public string? FontName { get; set; }
    /// <summary>
    /// Font size in Pixels
    /// </summary>
    public int? FontSize { get; set; }
    /// <summary>
    /// Color (hex)
    /// </summary>
    public Color? TextColor { get; set; }
    public HorizontalAlignment? HorizontalAlignment { get; set; }
    public ParagraphStyle? Style { get; set; }

    public bool PageBreakBefore { get; set; }
    public bool PageBreakAfter { get; set; }
}