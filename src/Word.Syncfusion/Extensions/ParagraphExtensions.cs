using Regira.IO.Extensions;
using Syncfusion.DocIO.DLS;
using RegiraHorizontalAlignment = Regira.Office.Word.Models.HorizontalAlignment;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;

namespace Regira.Office.Word.Syncfusion.Extensions;

internal static class ParagraphExtensions
{
    public static void InjectHtml(this WParagraph paragraph, string html)
    {
        paragraph.Text = string.Empty;
        paragraph.AppendHTML(html);
    }

    public static WParagraph SetParagraph(this WParagraph target, RegiraParagraph src)
    {
        if (!string.IsNullOrWhiteSpace(src.Text))
        {
            var textRange = target.AppendText(src.Text);
            if (src.TextColor.HasValue)
            {
                var color = src.TextColor.Value;
                textRange.CharacterFormat.TextColor = global::Syncfusion.Drawing.Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
            }
            if (!string.IsNullOrWhiteSpace(src.FontName))
            {
                textRange.CharacterFormat.FontName = src.FontName;
            }
            if (src.FontSize.HasValue)
            {
                textRange.CharacterFormat.FontSize = src.FontSize.Value;
            }
        }

        if (src.Style.HasValue)
        {
            // ParagraphStyle and BuiltinStyle share their member names.
            target.ApplyStyle(Enum.Parse<BuiltinStyle>(src.Style.Value.ToString()));
        }

        if (src.HorizontalAlignment.HasValue)
        {
            target.ParagraphFormat.HorizontalAlignment = src.HorizontalAlignment.Value.ToDocIO();
        }

        if (src.Image?.File?.GetBytes() is { } imageBytes)
        {
            var picture = target.AppendPicture(imageBytes);
            if (src.Image.Size.HasValue)
            {
                picture.Width = src.Image.Size.Value.Width;
                picture.Height = src.Image.Size.Value.Height;
            }
            if (!string.IsNullOrWhiteSpace(src.Image.Name))
            {
                picture.Title = src.Image.Name;
                picture.AlternativeText = src.Image.Name;
            }
            if (src.Image.HorizontalAlignment.HasValue)
            {
                picture.HorizontalAlignment = Enum.Parse<global::Syncfusion.DocIO.ShapeHorizontalAlignment>(src.Image.HorizontalAlignment.Value.ToString());
            }

            picture.TextWrappingStyle = TextWrappingStyle.Square;
        }

        target.ParagraphFormat.PageBreakBefore = src.PageBreakBefore;
        target.ParagraphFormat.PageBreakAfter = src.PageBreakAfter;

        return target;
    }

    /// <summary>
    /// Left, Center, Right and Justify are spelled identically in both enums.
    /// </summary>
    public static HorizontalAlignment ToDocIO(this RegiraHorizontalAlignment alignment)
        => Enum.Parse<HorizontalAlignment>(alignment.ToString());

    public static bool IsEmpty(this WParagraph paragraph)
        => paragraph.ChildEntities.Count == 0;
}
