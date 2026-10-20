using Regira.Office.Word.Spire.Extensions;
using Regira.Office.Word.Templating;
using Regira.TreeList;
using Spire.Doc;
using Spire.Doc.Collections;
using Spire.Doc.Documents;
using Spire.Doc.Fields;

namespace Regira.Office.Word.Spire.Internal;

/// <summary>
/// <see cref="TemplateWalk{TNode}"/> over Spire.Doc's object model: the whole tree, apart from footnotes, endnotes and
/// comments. A text box's VML fallback copy is outside the model; <see cref="TextBoxFallbacks"/> rewrites it from the
/// copy resolved here when the document is saved.
/// </summary>
internal sealed class SpireTemplateWalk(Document doc) : TemplateWalk<DocumentObject>
{
    protected override IEnumerable<DocumentObject> Paragraphs()
        => doc.ToTreeList().FindAllParagraphs().Where(paragraph => !IsInNoteOrComment(paragraph));

    protected override string Text(DocumentObject paragraph) => GetVisibleText((Paragraph)paragraph);

    protected override DocumentObject? Parent(DocumentObject node) => node.Owner;

    protected override NodeKind KindOf(DocumentObject node)
        => node switch
        {
            Paragraph => NodeKind.Paragraph,
            // a row-level content control holds rows of its own, as a table does; Spire models it as a row of rows
            Table or StructureDocumentTagRow { ChildObjects: [TableRow, ..] } => NodeKind.Table,
            TableRow => NodeKind.Row,
            TableCell => NodeKind.Cell,
            _ => NodeKind.Other
        };

    protected override IEnumerable<IReadOnlyList<DocumentObject>> Segments(DocumentObject container)
        => container switch
        {
            Table table => [table.Rows.Cast<DocumentObject>().ToArray()],
            // a section's body is a container of its own, so a block never spans a section break
            _ => [Children(container).ToArray()]
        };

    protected override DocumentObject Clone(DocumentObject child) => child.Clone();

    protected override void InsertAfter(DocumentObject node, DocumentObject reference)
    {
        if (reference is TableRow { Owner: Table } row)
        {
            var rows = ((Table)row.Owner).Rows;
            rows.Insert(rows.IndexOf(row) + 1, (TableRow)node);
            return;
        }
        var children = reference.Owner.ChildObjects;
        children.Insert(children.IndexOf(reference) + 1, node);
    }

    protected override void Remove(DocumentObject child)
    {
        if (child is TableRow { Owner: Table } row)
        {
            ((Table)row.Owner).Rows.Remove(row);
            return;
        }
        child.Owner.ChildObjects.Remove(child);
    }

    protected override bool HoldsNoteOrComment(DocumentObject child)
        => Descendants(child).Any(node => node is Footnote or Comment or CommentMark);

    /// <summary>
    /// A copy keeps no bookmark, whose name the original keeps and the global pass fills, and its content controls take
    /// ids of their own: Spire writes a copied control's id as it is.
    /// </summary>
    protected override void Scrub(DocumentObject copy)
    {
        foreach (var bookmark in Descendants(copy).Where(node => node is BookmarkStart or BookmarkEnd).ToArray())
        {
            bookmark.Owner.ChildObjects.Remove(bookmark);
        }

        var renumbered = new Dictionary<decimal, decimal>();
        foreach (var properties in Descendants(copy).Prepend(copy).Select(ControlProperties).OfType<SDTProperties>())
        {
            if (!renumbered.TryGetValue(properties.Id, out var id))
            {
                _lastControlId ??= doc.ToTreeList().WithOffspring().Select(node => ControlProperties(node.Value)?.Id ?? 0).DefaultIfEmpty(0).Max();
                renumbered[properties.Id] = id = (_lastControlId += 1).Value;
            }
            properties.Id = id;
        }
    }

    private decimal? _lastControlId;

    private static SDTProperties? ControlProperties(DocumentObject node)
        => node switch
        {
            StructureDocumentTag control => control.SDTProperties,
            StructureDocumentTagInline control => control.SDTProperties,
            StructureDocumentTagRow control => control.SDTProperties,
            StructureDocumentTagCell control => control.SDTProperties,
            _ => null
        };

    protected override void Finish(DocumentObject container)
    {
        switch (container)
        {
            case Table { Rows.Count: 0 } table:
                var owner = table.Owner;
                owner.ChildObjects.Remove(table);
                Finish(owner);
                break;
            // a row-level content control left without rows goes, and so does its table if that leaves it without any
            case StructureDocumentTagRow { ChildObjects.Count: 0 } control when control.Owner is { } rows:
                Remove(control);
                Finish(rows);
                break;
            // whatever holds paragraphs ends with one: a body, cell, header, footer, text box or content control
            case Body body when body.ChildObjects.Count == 0 || body.ChildObjects[body.ChildObjects.Count - 1] is Table:
                body.AddParagraph();
                break;
        }
    }

    protected override void Fill(DocumentObject paragraph, IReadOnlyList<TemplateField> fields)
    {
        foreach (var field in fields)
        {
            // a line break within the paragraph, as the global pass writes one
            ((Paragraph)paragraph).Replace(field.Pattern, field.Value.ReplaceLineEndings("\v"));
        }
    }

    private static IEnumerable<DocumentObject> Children(DocumentObject node)
        => node.IsComposite && node.ChildObjects != null ? node.ChildObjects.Cast<DocumentObject>() : [];

    private static IEnumerable<DocumentObject> Descendants(DocumentObject node)
    {
        foreach (var child in Children(node))
        {
            yield return child;
            foreach (var offspring in Descendants(child))
            {
                yield return offspring;
            }
        }
    }

    /// <summary>
    /// Whether the paragraph belongs to a footnote, endnote or comment, which are not part of a template's blocks.
    /// </summary>
    private static bool IsInNoteOrComment(Paragraph paragraph)
    {
        for (var owner = paragraph.Owner; owner != null; owner = owner.Owner)
        {
            if (owner is Footnote or Comment)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The paragraph's text without its deleted revisions, and with an inline content control's — what
    /// <see cref="Paragraph.Text"/> gives neither. Spire keeps a field's code on the field, out of the text.
    /// </summary>
    private static string GetVisibleText(Paragraph paragraph)
    {
        var text = new VisibleText();
        Read(paragraph.ChildObjects);
        return text.ToString();

        void Read(DocumentObjectCollection items)
        {
            foreach (DocumentObject item in items)
            {
                switch (item)
                {
                    case Field:
                        text.FieldStart();
                        break;
                    case FieldMark { Type: FieldMarkType.FieldSeparator }:
                        text.FieldSeparator();
                        break;
                    case FieldMark { Type: FieldMarkType.FieldEnd }:
                        text.FieldEnd();
                        break;
                    case TextRange range:
                        text.Append(range.Text, range.IsDeleteRevision);
                        break;
                    case StructureDocumentTagInline control:
                        Read(control.SDTContent.ChildObjects);
                        break;
                }
            }
        }
    }
}
