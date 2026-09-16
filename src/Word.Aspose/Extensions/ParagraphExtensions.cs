using Aspose.Words;
using Aspose.Words.Drawing;
using Regira.IO.Extensions;
using AsposeDocumentBuilder = Aspose.Words.DocumentBuilder;
using AsposeParagraph = Aspose.Words.Paragraph;
using RegiraHorizontalAlignment = Regira.Office.Word.Models.HorizontalAlignment;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;
using ShapeAlignment = Aspose.Words.Drawing.HorizontalAlignment;

namespace Regira.Office.Word.Aspose.Extensions;

internal static class ParagraphExtensions
{
    /// <summary>
    /// Replaces the paragraph's content with the rendered <paramref name="html"/>.
    /// </summary>
    public static void InjectHtml(this AsposeParagraph paragraph, string html)
    {
        paragraph.RemoveAllChildren();
        var builder = new AsposeDocumentBuilder((Document)paragraph.Document);
        builder.MoveTo(paragraph);
        builder.InsertHtml(html);
    }

    /// <summary>
    /// Fills an empty paragraph that is already part of a document.
    /// </summary>
    public static AsposeParagraph SetParagraph(this AsposeParagraph target, RegiraParagraph src)
    {
        var doc = (Document)target.Document;

        if (src.Style.HasValue && Enum.TryParse<StyleIdentifier>(src.Style.Value.ToString(), out var style))
        {
            // ParagraphStyle and StyleIdentifier share their member names, except for MacroText and NoStyle
            target.ParagraphFormat.StyleIdentifier = style;
        }

        if (!string.IsNullOrWhiteSpace(src.Text))
        {
            var run = new Run(doc, src.Text);
            if (src.TextColor.HasValue)
            {
                var color = src.TextColor.Value;
                run.Font.Color = System.Drawing.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
            }
            if (!string.IsNullOrWhiteSpace(src.FontName))
            {
                run.Font.Name = src.FontName;
            }
            if (src.FontSize.HasValue)
            {
                run.Font.Size = src.FontSize.Value;
            }
            target.AppendChild(run);
        }

        if (src.HorizontalAlignment.HasValue)
        {
            target.ParagraphFormat.Alignment = src.HorizontalAlignment.Value.ToAspose();
        }

        if (src.Image?.File?.GetBytes() is { } imageBytes)
        {
            var builder = new AsposeDocumentBuilder(doc);
            builder.MoveTo(target);
            var picture = src.Image.Size is { } size
                ? builder.InsertImage(imageBytes, size.Width, size.Height)
                : builder.InsertImage(imageBytes);
            if (!string.IsNullOrWhiteSpace(src.Image.Name))
            {
                picture.Title = src.Image.Name;
                picture.AlternativeText = src.Image.Name;
            }

            picture.WrapType = WrapType.Square;
            if (src.Image.HorizontalAlignment.HasValue)
            {
                picture.RelativeHorizontalPosition = RelativeHorizontalPosition.Column;
                picture.HorizontalAlignment = src.Image.HorizontalAlignment.Value.ToAsposeShape();
            }
        }

        target.ParagraphFormat.PageBreakBefore = src.PageBreakBefore;
        if (src.PageBreakAfter)
        {
            // Aspose has no page-break-after paragraph property; a page break character ends the paragraph instead
            target.AppendChild(new Run(doc, ControlChar.PageBreak));
        }

        return target;
    }

    public static ParagraphAlignment ToAspose(this RegiraHorizontalAlignment alignment)
        => alignment switch
        {
            RegiraHorizontalAlignment.Left => ParagraphAlignment.Left,
            RegiraHorizontalAlignment.Center => ParagraphAlignment.Center,
            RegiraHorizontalAlignment.Right => ParagraphAlignment.Right,
            RegiraHorizontalAlignment.Justify => ParagraphAlignment.Justify,
            _ => throw new ArgumentOutOfRangeException(nameof(alignment), alignment, null)
        };

    /// <summary>
    /// A shape cannot be justified; that value keeps the default position.
    /// </summary>
    public static ShapeAlignment ToAsposeShape(this RegiraHorizontalAlignment alignment)
        => alignment switch
        {
            RegiraHorizontalAlignment.Left => ShapeAlignment.Left,
            RegiraHorizontalAlignment.Center => ShapeAlignment.Center,
            RegiraHorizontalAlignment.Right => ShapeAlignment.Right,
            _ => ShapeAlignment.Default
        };

    public static bool IsEmpty(this AsposeParagraph paragraph)
        => !paragraph.HasChildNodes;
}
