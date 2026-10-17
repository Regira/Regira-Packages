using System.Text.RegularExpressions;
using Regira.Office.Word.Syncfusion.Extensions;
using Regira.Office.Word.Templating;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;

namespace Regira.Office.Word.Syncfusion.Internal;

/// <summary>
/// <see cref="TemplateWalk{TNode}"/> over DocIO's entities: each section's body and its eight header and footer
/// slots, with the text boxes and shapes in them. Footnotes, endnotes and comments are not read.
/// </summary>
internal sealed class SyncfusionTemplateWalk(WordDocument doc) : TemplateWalk<Entity>
{
    // what a marker opens with — not a placeholder's {{ — read as the blocks read it: in any case, else a whole word
    private static readonly Regex MarkerStartRegex = new(@"\{\{\s*(?:#|/|else\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    // a whole tag — a marker or a field — as the blocks and the rows' fields read one
    private static readonly Regex TagRegex = new(@"\{\{[^{}]*\}\}", RegexOptions.CultureInvariant);

    private int _reads;

    protected override IEnumerable<Entity> Paragraphs()
    {
        var stories = doc.Sections.OfType<WSection>()
            .SelectMany(section => new[]
            {
                section.Body,
                section.HeadersFooters.Header, section.HeadersFooters.FirstPageHeader, section.HeadersFooters.EvenHeader, section.HeadersFooters.OddHeader,
                section.HeadersFooters.Footer, section.HeadersFooters.FirstPageFooter, section.HeadersFooters.EvenFooter, section.HeadersFooters.OddFooter
            });
        // a group's text bodies, which no walk reaches: those holding a tag and still in the document — not a dropped
        // branch's, nor an original a copy stands in for. The first read tells whether the document uses blocks, so it
        // searches for markers alone; the reads after it fill fields too, and a template whose groups hold no tag is
        // searched for them once: its copies hold none either
        var firstRead = _reads++ == 0;
        var searched = firstRead || _groupsHoldTags
            ? TagParagraphs(firstRead ? MarkerStartRegex : TagRegex)
                .Where(InGroup)
                .Select(paragraph => paragraph.Owner)
                .OfType<Entity>()
                .Where(IsInDocument)
                .ToArray()
            : [];
        if (!firstRead)
        {
            _groupsHoldTags = searched.Length > 0;
        }
        return stories
            .Concat(searched)
            .SelectMany(BlockParagraphs)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<Entity>();
    }

    protected override string Text(Entity paragraph) => GetVisibleText((WParagraph)paragraph);

    protected override Entity? Parent(Entity node) => node.Owner;

    protected override NodeKind KindOf(Entity node)
        => node switch
        {
            WParagraph => NodeKind.Paragraph,
            WTable => NodeKind.Table,
            WTableRow => NodeKind.Row,
            WTableCell => NodeKind.Cell,
            _ => NodeKind.Other
        };

    // a section's body is a container of its own, so a block never spans a section break
    protected override IEnumerable<IReadOnlyList<Entity>> Segments(Entity container)
        => container switch
        {
            WTable table => [table.Rows.OfType<Entity>().ToArray()],
            ICompositeEntity composite => [composite.ChildEntities.OfType<Entity>().ToArray()],
            _ => []
        };

    protected override Entity Clone(Entity child) => child.Clone();

    protected override void InsertAfter(Entity node, Entity reference)
    {
        if (reference is WTableRow row)
        {
            var rows = ((WTable)row.Owner).Rows;
            rows.Insert(rows.IndexOf(row) + 1, (WTableRow)node);
            return;
        }
        var children = ((ICompositeEntity)reference.Owner).ChildEntities;
        children.Insert(children.IndexOf(reference) + 1, node);
    }

    protected override void Remove(Entity child)
    {
        if (child is WTableRow row)
        {
            ((WTable)row.Owner).Rows.Remove(row);
            return;
        }
        ((ICompositeEntity)child.Owner).ChildEntities.Remove(child);
    }

    protected override bool HoldsNoteOrComment(Entity child)
        => Descendants(child).Prepend(child).Any(entity => entity is WFootnote or WComment or WCommentMark);

    /// <summary>
    /// A copy keeps no bookmark, whose name the original keeps and the global pass fills. DocIO writes a copied content
    /// control's id, and a copied shape's VML id, as they were; <see cref="PackageIds"/> renumbers them in the saved
    /// package, since DocIO exposes neither.
    /// </summary>
    protected override void Scrub(Entity copy)
    {
        foreach (var bookmark in Descendants(copy).Where(entity => entity is BookmarkStart or BookmarkEnd).ToArray())
        {
            ((ICompositeEntity)bookmark.Owner).ChildEntities.Remove(bookmark);
        }
    }

    protected override void Finish(Entity container)
    {
        switch (container)
        {
            case WTable { Rows.Count: 0 } table:
                var owner = table.Owner;
                ((ICompositeEntity)owner).ChildEntities.Remove(table);
                Finish(owner);
                break;
            // whatever holds paragraphs ends with one: a body, cell, header, footer, text box or content control
            case WTextBody body when body.ChildEntities.Count == 0 || body.ChildEntities[body.ChildEntities.Count - 1] is WTable:
                body.AddParagraph();
                break;
        }
    }

    protected override void Fill(Entity paragraph, IReadOnlyList<TemplateField> fields)
    {
        foreach (var field in fields)
        {
            // \v is Word's soft line break within a paragraph, as the global pass writes one
            ((WParagraph)paragraph).Replace(field.Pattern, field.Value.ReplaceLineEndings("\v"));
        }
    }

    private static IEnumerable<Entity> Descendants(Entity entity)
    {
        IEntity? inner = entity switch
        {
            WTextBox textBox => textBox.TextBoxBody,
            Shape shape => shape.TextBody,
            _ => entity
        };
        if (inner is not ICompositeEntity composite)
        {
            yield break;
        }
        foreach (var child in composite.ChildEntities.OfType<Entity>())
        {
            yield return child;
            foreach (var offspring in Descendants(child))
            {
                yield return offspring;
            }
        }
    }

    /// <summary>
    /// The paragraphs blocks are read from: the story's own, its tables' and content controls', and those of its
    /// text boxes and shapes, which <see cref="WordDocumentExtensions.Descendants"/> does not enter. A footnote,
    /// endnote or comment is not part of a template's blocks, as on the other backends.
    /// </summary>
    private static IEnumerable<WParagraph> BlockParagraphs(IEntity? entity)
    {
        if (entity is not ICompositeEntity composite)
        {
            yield break;
        }

        foreach (var child in composite.ChildEntities.OfType<IEntity>())
        {
            var inner = child switch
            {
                WFootnote or WComment => null,
                WTextBox textBox => textBox.TextBoxBody,
                Shape shape => shape.TextBody,
                _ => child
            };
            if (child is WParagraph paragraph)
            {
                yield return paragraph;
            }
            foreach (var offspring in BlockParagraphs(inner))
            {
                yield return offspring;
            }
        }
    }

    /// <summary>
    /// The paragraphs that hold <paramref name="tag"/>, found by search, in a document holding a group of text boxes (<c>wpg:wgp</c>):
    /// a group loads as a <c>GroupShape</c>, whose shapes the public object model does not expose, so
    /// <see cref="BlockParagraphs"/> cannot walk into it. Only then: reading a match splits and merges the runs around
    /// it, which a document without a group is spared. A footnote, endnote or comment is left out, as there.
    /// </summary>
    private IEnumerable<WParagraph> TagParagraphs(Regex tag)
        => (doc.FindAllItemsByProperty(EntityType.GroupShape, null, null) is not { Count: > 0 } ? [] : doc.FindAll(tag) ?? [])
            .Select(selection => selection.GetAsOneRange()?.OwnerParagraph)
            .OfType<WParagraph>()
            .Where(paragraph => !InNoteOrComment(paragraph));

    private bool _groupsHoldTags = true;

    private static bool InGroup(IEntity entity)
    {
        for (var owner = entity.Owner; owner != null; owner = owner.Owner)
        {
            if (owner is GroupShape or ChildShape)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsInDocument(Entity entity)
    {
        for (IEntity? owner = entity; owner != null; owner = owner.Owner)
        {
            if (ReferenceEquals(owner, doc))
            {
                return true;
            }
        }
        return false;
    }

    private static bool InNoteOrComment(IEntity entity)
    {
        for (var owner = entity.Owner; owner != null; owner = owner.Owner)
        {
            if (owner is WFootnote or WComment)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The paragraph's text without its field codes and deleted revisions, which <see cref="WParagraph.Text"/> holds.
    /// </summary>
    private static string GetVisibleText(WParagraph paragraph)
    {
        var text = new VisibleText();
        Read(paragraph.Items);
        return text.ToString();

        void Read(ParagraphItemCollection items)
        {
            foreach (ParagraphItem item in items)
            {
                switch (item)
                {
                    case WField:
                        text.FieldStart();
                        break;
                    case WFieldMark { Type: FieldMarkType.FieldSeparator }:
                        text.FieldSeparator();
                        break;
                    case WFieldMark { Type: FieldMarkType.FieldEnd }:
                        text.FieldEnd();
                        break;
                    case WTextRange range:
                        text.Append(range.Text, range.IsDeleteRevision);
                        break;
                    case InlineContentControl control:
                        Read(control.ParagraphItems);
                        break;
                }
            }
        }
    }
}
