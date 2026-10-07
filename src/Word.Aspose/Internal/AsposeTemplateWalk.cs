using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
using Aspose.Words.Markup;
using Aspose.Words.Notes;
using Aspose.Words.Replacing;
using Aspose.Words.Tables;
using Regira.Office.Word.Aspose.Extensions;
using Regira.Office.Word.Templating;
using AsposeParagraph = Aspose.Words.Paragraph;

namespace Regira.Office.Word.Aspose.Internal;

/// <summary>
/// <see cref="TemplateWalk{TNode}"/> over Aspose.Words' node tree: every paragraph outside footnotes, endnotes and
/// comments, text boxes and content controls included.
/// </summary>
internal sealed class AsposeTemplateWalk(Document doc) : TemplateWalk<Node>
{
    protected override IEnumerable<Node> Paragraphs()
        => doc.FindAllParagraphs()
            // a footnote, endnote or comment is not part of a template's blocks, as on the other backends
            .Where(paragraph => paragraph.GetAncestor(NodeType.Footnote) == null
                && paragraph.GetAncestor(NodeType.Comment) == null);

    protected override string Text(Node paragraph) => GetVisibleText((AsposeParagraph)paragraph);

    protected override Node? Parent(Node node) => node.ParentNode;

    protected override NodeKind KindOf(Node node)
        => node switch
        {
            AsposeParagraph => NodeKind.Paragraph,
            // a row-level content control holds rows of its own, as a table does
            Table or StructuredDocumentTag { Level: MarkupLevel.Row } => NodeKind.Table,
            Row => NodeKind.Row,
            Cell => NodeKind.Cell,
            _ => NodeKind.Other
        };

    // a table's children are its rows, and a section's body is a container of its own, so a block never spans a
    // section break
    protected override IEnumerable<IReadOnlyList<Node>> Segments(Node container)
        => container is CompositeNode composite ? [composite.ToArray()] : [];

    protected override Node Clone(Node child) => child.Clone(true);

    protected override void InsertAfter(Node node, Node reference) => reference.ParentNode.InsertAfter(node, reference);

    protected override void Remove(Node child) => child.Remove();

    protected override bool HoldsNoteOrComment(Node child)
        => child is Footnote or Comment or CommentRangeStart or CommentRangeEnd
            || child is CompositeNode composite && composite.GetChildNodes(NodeType.Any, true)
                .Any(node => node is Footnote or Comment or CommentRangeStart or CommentRangeEnd);

    /// <summary>
    /// A copy keeps no bookmark, whose name the original keeps and the global pass fills, and its shapes take names of
    /// their own: Aspose writes a shape's or group's VML copy under its name, which an id must not repeat.
    /// </summary>
    protected override void Scrub(Node copy)
    {
        if (copy is not CompositeNode composite)
        {
            return;
        }
        foreach (var bookmark in composite.GetChildNodes(NodeType.Any, true).Where(node => node is BookmarkStart or BookmarkEnd).ToArray())
        {
            bookmark.Remove();
        }
        foreach (var shape in composite.GetChildNodes(NodeType.Any, true).OfType<ShapeBase>())
        {
            if (!string.IsNullOrEmpty(shape.Name))
            {
                _copies[shape.Name] = _copies.GetValueOrDefault(shape.Name, 1) + 1;
                shape.Name = $"{shape.Name}_{_copies[shape.Name]}";
            }
        }
    }

    // how many copies of each shape name there are so far, the original counted
    private readonly Dictionary<string, int> _copies = [];

    protected override void Finish(Node container)
    {
        if (container is Table table)
        {
            if (table.Rows.Count == 0)
            {
                var owner = table.ParentNode;
                table.Remove();
                Finish(owner);
            }
            return;
        }
        if (container is StructuredDocumentTag { Level: MarkupLevel.Row } control)
        {
            // a row-level content control left without rows goes, and so does its table if that leaves it without any
            if (!control.HasChildNodes)
            {
                var owner = control.ParentNode;
                control.Remove();
                Finish(owner);
            }
            return;
        }
        if (container is CompositeNode composite and not AsposeParagraph && composite.LastChild is null or Table)
        {
            // whatever holds paragraphs ends with one: a body, cell, header, footer, text box or content control
            composite.AppendChild(new AsposeParagraph(doc));
        }
    }

    protected override void Fill(Node paragraph, IReadOnlyList<TemplateField> fields)
    {
        foreach (var field in fields)
        {
            // a line break within the paragraph, as the global pass writes one
            var replacement = WordService.Literal(field.Value).ReplaceLineEndings(ControlChar.LineBreak);
            paragraph.Range.Replace(field.Pattern, replacement, new FindReplaceOptions());
        }
    }

    /// <summary>
    /// The paragraph's text without its field codes and deleted revisions, which <see cref="Node.GetText"/> holds,
    /// and without that of a text box inside it, whose paragraphs are read on their own.
    /// </summary>
    private static string GetVisibleText(AsposeParagraph paragraph)
    {
        var text = new VisibleText();
        foreach (Node node in paragraph.GetChildNodes(NodeType.Any, true))
        {
            if (node.GetAncestor(NodeType.Paragraph) != paragraph)
            {
                continue;
            }
            switch (node)
            {
                case FieldStart:
                    text.FieldStart();
                    break;
                case FieldSeparator:
                    text.FieldSeparator();
                    break;
                case FieldEnd:
                    text.FieldEnd();
                    break;
                case Run run:
                    text.Append(run.Text, run.IsDeleteRevision);
                    break;
            }
        }
        return text.ToString();
    }
}
