using Regira.IO.Extensions;
using Regira.TreeList;
using Spire.Doc;
using Spire.Doc.Documents;
using Spire.Doc.Fields;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;
using SpireHorizontalAlignement = Spire.Doc.Documents.HorizontalAlignment;
using SpireParagraph = Spire.Doc.Documents.Paragraph;

namespace Regira.Office.Word.Spire.Extensions;

internal static class ParagraphExtensions
{
    public static SpireParagraph SetSpireParagraph(this SpireParagraph target, RegiraParagraph src)
    {
        //var target = new SpireParagraph(doc);
        if (!string.IsNullOrWhiteSpace(src.Text))
        {
            var textRange = target.AppendText(src.Text);
            if (src.TextColor.HasValue)
            {
                var textColor = src.TextColor!.Value;
                textRange.CharacterFormat.TextColor = System.Drawing.Color.FromArgb(textColor.Alpha, textColor.Red, textColor.Green, textColor.Blue);
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
            var spireStyle = (BuiltinStyle)Enum.Parse(typeof(BuiltinStyle), src.Style.Value.ToString());
            target.ApplyStyle(spireStyle);
        }

        if (src.HorizontalAlignment.HasValue)
        {
            target.Format.HorizontalAlignment = (SpireHorizontalAlignement)Enum.Parse(typeof(SpireHorizontalAlignement), src.HorizontalAlignment.Value.ToString());
        }

        if (src.Image != null)
        {
            var docPicture = target.AppendPicture(src.Image.File?.GetBytes());
            if (src.Image.Size.HasValue)
            {
                docPicture.Width = src.Image.Size.Value.Width;
            }
            if (src.Image.Size.HasValue)
            {
                docPicture.Height = src.Image.Size.Value.Height;
            }
            if (!string.IsNullOrWhiteSpace(src.Image.Name))
            {
                docPicture.Title = src.Image.Name;
                docPicture.AlternativeText = src.Image.Name;
            }
            if (src.Image.HorizontalAlignment.HasValue)
            {
                docPicture.HorizontalAlignment = (ShapeHorizontalAlignment)Enum.Parse(typeof(ShapeHorizontalAlignment), src.Image.HorizontalAlignment.Value.ToString());
            }

            docPicture.TextWrappingStyle = TextWrappingStyle.Square;
        }

        target.Format.BeforeAutoSpacing = true;
        target.Format.AfterAutoSpacing = true;

        target.Format.PageBreakBefore = src.PageBreakBefore;
        target.Format.PageBreakAfter = src.PageBreakAfter;

        return target;
    }

    public static bool IsEmpty(this SpireParagraph p)
    {
        var tree = p.ToTreeList();
        var offspring = tree.GetOffspring().ToArray();
        return !offspring.Any();// || offspring.All(o => o.Value is TextRange tr && string.IsNullOrEmpty(tr.Text));
    }

    public static SpireParagraph ApplyStyle(this SpireParagraph paragraph, Style spireStyle)
    {
        if (spireStyle is not ParagraphStyle paragraphStyle)
        {
            return paragraph;
        }

        var textRanges = paragraph.ChildObjects.Cast<DocumentObject>()
            .Where(child => child is TextRange)
            .Select(child => child! as TextRange)
            .ToArray();
        foreach (var textRange in textRanges)
        {
            textRange!.CharacterFormat.FontName = paragraphStyle.CharacterFormat.FontName;
            textRange.CharacterFormat.FontSize = paragraphStyle.CharacterFormat.FontSize;
        }

        return paragraph;
    }

    public static void InjectHtml(this SpireParagraph paragraph, string html)
    {
        paragraph.Text = string.Empty;
        paragraph.AppendHTML(html);
    }
}