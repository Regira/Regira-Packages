using System.Text.RegularExpressions;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Replacing;
using Aspose.Words.Saving;
using Aspose.Words.Tables;
using AsposeHeaderFooterType = Aspose.Words.HeaderFooterType;
using AsposeParagraph = Aspose.Words.Paragraph;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;

namespace Regira.Office.Word.Aspose.Extensions;

internal static class WordDocumentExtensions
{
    public static Stream ToStream(this Document doc, SaveFormat format = SaveFormat.Docx)
    {
        var ms = new MemoryStream();
        doc.Save(ms, format);
        ms.Position = 0;
        return ms;
    }

    public static Stream ToStream(this Document doc, SaveOptions options)
    {
        var ms = new MemoryStream();
        doc.Save(ms, options);
        ms.Position = 0;
        return ms;
    }


    /// <summary>
    /// Every shape that carries an image — optionally only those whose Alt-Text Title or Description
    /// equals <paramref name="name"/>, which is how a template marks an image placeholder.
    /// </summary>
    public static IEnumerable<Shape> FindAllPictures(this CompositeNode node, string? name = null)
        => node.GetChildNodes(NodeType.Shape, true)
            .OfType<Shape>()
            .Where(shape => shape.HasImage && (name == null || shape.Title == name || shape.AlternativeText == name));

    public static IEnumerable<Table> FindAllTables(this CompositeNode node)
        => node.GetChildNodes(NodeType.Table, true).OfType<Table>();

    /// <summary>
    /// Finds a table by its Alt-Text Title, which is how a template marks a collection placeholder.
    /// </summary>
    public static Table? FindTable(this CompositeNode node, string title)
        => node.FindAllTables().FirstOrDefault(table => table.Title == title);

    public static IEnumerable<AsposeParagraph> FindAllParagraphs(this CompositeNode node)
        => node.GetChildNodes(NodeType.Paragraph, true).OfType<AsposeParagraph>();

    /// <summary>
    /// The paragraphs holding a match of <paramref name="pattern"/>, in document order. The search
    /// replaces nothing.
    /// </summary>
    public static List<AsposeParagraph> FindParagraphs(this Node node, Regex pattern)
    {
        var collector = new ParagraphCollector();
        node.Range.Replace(pattern, string.Empty, new FindReplaceOptions(collector));
        return collector.Paragraphs;
    }


    public static HeaderFooter GetHeader(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
        => doc.FirstSection.GetOrAddHeaderFooter(type switch
        {
            HeaderFooterType.FirstPage => AsposeHeaderFooterType.HeaderFirst,
            HeaderFooterType.Even => AsposeHeaderFooterType.HeaderEven,
            // Word has no separate odd-page story: the primary one serves odd pages
            _ => AsposeHeaderFooterType.HeaderPrimary
        });

    public static HeaderFooter GetFooter(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
        => doc.FirstSection.GetOrAddHeaderFooter(type switch
        {
            HeaderFooterType.FirstPage => AsposeHeaderFooterType.FooterFirst,
            HeaderFooterType.Even => AsposeHeaderFooterType.FooterEven,
            _ => AsposeHeaderFooterType.FooterPrimary
        });

    public static string GetHeaderText(this Document doc)
        => string.Join(Environment.NewLine, doc.HeadersFooters().Where(story => story.IsHeader).Select(story => story.GetText()));

    public static string GetFooterText(this Document doc)
        => string.Join(Environment.NewLine, doc.HeadersFooters().Where(story => !story.IsHeader).Select(story => story.GetText()));

    /// <summary>
    /// Replaces <paramref name="target"/>'s children with copies of <paramref name="source"/>, imported
    /// into <paramref name="target"/>'s document. The source nodes stay where they are, so they can be
    /// reused across several targets.
    /// </summary>
    public static void ReplaceChildNodes(this CompositeNode target, IEnumerable<Node> source)
    {
        var nodes = source.ToArray();
        target.RemoveAllChildren();
        if (nodes.Length == 0)
        {
            return;
        }

        var importer = new NodeImporter(nodes[0].Document, target.Document, ImportFormatMode.UseDestinationStyles);
        foreach (var node in nodes)
        {
            target.AppendChild(importer.ImportNode(node, true));
        }
    }

    public static bool HasEmptyBody(this Document doc)
        => doc.Sections.OfType<Section>().All(section => section.Body.IsEmpty());

    /// <summary>
    /// True when the story holds nothing but empty paragraphs; a table or any other block counts as content.
    /// </summary>
    public static bool IsEmpty(this Story story)
    {
        var children = story.ToArray();
        return children.All(child => child is AsposeParagraph { HasChildNodes: false });
    }


    private static HeaderFooter GetOrAddHeaderFooter(this Section section, AsposeHeaderFooterType type)
    {
        var story = section.HeadersFooters[type];
        if (story == null)
        {
            story = new HeaderFooter(section.Document, type);
            section.HeadersFooters.Add(story);
        }
        return story;
    }

    private static IEnumerable<HeaderFooter> HeadersFooters(this Document doc)
        => doc.Sections.OfType<Section>().SelectMany(section => section.HeadersFooters.OfType<HeaderFooter>());

    private sealed class ParagraphCollector : IReplacingCallback
    {
        public List<AsposeParagraph> Paragraphs { get; } = [];

        public ReplaceAction Replacing(ReplacingArgs args)
        {
            if (args.MatchNode.GetAncestor(NodeType.Paragraph) is AsposeParagraph paragraph && !Paragraphs.Contains(paragraph))
            {
                Paragraphs.Add(paragraph);
            }
            return ReplaceAction.Skip;
        }
    }
}
