using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;

namespace Regira.Office.Word.Syncfusion.Extensions;

internal static class WordDocumentExtensions
{
    public static Stream ToStream(this WordDocument doc, FormatType format = FormatType.Docx)
    {
        var ms = new MemoryStream();
        doc.Save(ms, format);
        ms.Position = 0;
        return ms;
    }


    /// <summary>
    /// Walks every descendant entity depth-first, in document order.
    /// </summary>
    public static IEnumerable<IEntity> Descendants(this IEntity? entity)
    {
        if (entity is not ICompositeEntity composite)
        {
            yield break;
        }

        foreach (var child in composite.ChildEntities.OfType<IEntity>())
        {
            yield return child;
            foreach (var offspring in child.Descendants())
            {
                yield return offspring;
            }
        }
    }

    public static IEnumerable<WPicture> FindAllPictures(this IEntity? entity, string? name = null)
        => entity.Descendants()
            .OfType<WPicture>()
            .Where(p => name == null || p.Title == name || p.AlternativeText == name);

    public static IEnumerable<WTable> FindAllTables(this IEntity? entity)
        => entity.Descendants().OfType<WTable>();

    /// <summary>
    /// Finds a table by its Alt-Text Title, which is how a template marks a collection placeholder.
    /// </summary>
    public static WTable? FindTable(this IEntity? entity, string title)
        => entity.FindAllTables().FirstOrDefault(t => t.Title == title);

    public static IEnumerable<WParagraph> FindAllParagraphs(this IEntity? entity)
        => entity.Descendants().OfType<WParagraph>();


    public static WTextBody GetHeader(this WordDocument doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var headersFooters = doc.Sections[0].HeadersFooters;
        return type switch
        {
            HeaderFooterType.FirstPage => headersFooters.FirstPageHeader,
            HeaderFooterType.Even => headersFooters.EvenHeader,
            HeaderFooterType.Odd => headersFooters.OddHeader,
            _ => headersFooters.Header
        };
    }

    public static WTextBody GetFooter(this WordDocument doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var headersFooters = doc.Sections[0].HeadersFooters;
        return type switch
        {
            HeaderFooterType.FirstPage => headersFooters.FirstPageFooter,
            HeaderFooterType.Even => headersFooters.EvenFooter,
            HeaderFooterType.Odd => headersFooters.OddFooter,
            _ => headersFooters.Footer
        };
    }

    public static string GetHeaderText(this WordDocument doc)
        => string.Join(Environment.NewLine, doc.Sections.OfType<WSection>()
            .SelectMany(s => new[] { s.HeadersFooters.Header, s.HeadersFooters.FirstPageHeader, s.HeadersFooters.EvenHeader, s.HeadersFooters.OddHeader })
            .SelectMany(body => body.FindAllParagraphs())
            .Select(p => p.Text));

    public static string GetFooterText(this WordDocument doc)
        => string.Join(Environment.NewLine, doc.Sections.OfType<WSection>()
            .SelectMany(s => new[] { s.HeadersFooters.Footer, s.HeadersFooters.FirstPageFooter, s.HeadersFooters.EvenFooter, s.HeadersFooters.OddFooter })
            .SelectMany(body => body.FindAllParagraphs())
            .Select(p => p.Text));


    /// <summary>
    /// Clears <paramref name="target"/> and clones <paramref name="source"/>'s entities into it, so the
    /// source stays attached to its own document and can be reused across several targets.
    /// </summary>
    public static void ReplaceChildEntities(this WTextBody target, EntityCollection source)
    {
        target.ChildEntities.Clear();
        foreach (var entity in source.OfType<Entity>().ToArray())
        {
            target.ChildEntities.Add(entity.Clone());
        }
    }

    public static bool HasEmptyBody(this WordDocument doc)
        => doc.Sections.OfType<WSection>().All(section => section.Body.IsEmpty());

    public static bool IsEmpty(this WTextBody body)
    {
        var paragraphs = body.ChildEntities.OfType<WParagraph>().ToArray();
        if (body.ChildEntities.Count > paragraphs.Length)
        {
            // a table or any other block means there is content
            return false;
        }
        return paragraphs.All(p => p.ChildEntities.Count == 0);
    }
}
