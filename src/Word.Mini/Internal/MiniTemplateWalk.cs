using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using Regira.Office.Word.Packaging;
using Regira.Office.Word.Templating;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Regira.Office.Word.Mini.Internal;

/// <summary>
/// <see cref="TemplateWalk{TNode}"/> over a package's Open XML: the body, headers and footers, both copies of a text
/// box among them. Footnotes, endnotes and comments live in parts of their own, which are not read. A paragraph's text
/// is its <c>w:t</c> elements, and every tag sits whole in one of them (<see cref="WordService.RewriteTags(byte[], IReadOnlyDictionary{string, string}, IReadOnlyDictionary{string, string})"/>),
/// so a row's field is filled within one.
/// </summary>
internal sealed class MiniTemplateWalk(WordprocessingDocument doc, IEnumerable<OpenXmlElement> roots) : TemplateWalk<OpenXmlElement>
{
    // the last block each container held before its blocks were resolved: one that ends as the template wrote it is left as it is
    private readonly Dictionary<OpenXmlElement, OpenXmlElement?> _lastBlocks = new(ReferenceEqualityComparer.Instance);
    private uint? _lastDrawingId;
    private int? _lastControlId;

    protected override IEnumerable<OpenXmlElement> Paragraphs() => roots.SelectMany(root => root.Descendants<W.Paragraph>());

    protected override string Text(OpenXmlElement paragraph) => GetOwnText((W.Paragraph)paragraph);

    protected override OpenXmlElement? Parent(OpenXmlElement node) => node.Parent;

    protected override NodeKind KindOf(OpenXmlElement node)
        => node switch
        {
            W.Paragraph => NodeKind.Paragraph,
            // a row-level content control's content holds rows of its own, as a table does
            W.Table or W.SdtContentRow => NodeKind.Table,
            W.TableRow => NodeKind.Row,
            W.TableCell => NodeKind.Cell,
            _ => NodeKind.Other
        };

    /// <summary>
    /// The container's block content — paragraphs, tables, content controls — cut into sections: a paragraph carrying
    /// section properties ends its section, and so does a content control holding one. A block cannot span a section
    /// break, as on the other backends, where each section has a body of its own. Everything else among the children
    /// (cell properties, bookmark ends, the final section properties) is never part of a block and stays. A table's
    /// children are its rows.
    /// </summary>
    protected override IEnumerable<IReadOnlyList<OpenXmlElement>> Segments(OpenXmlElement container)
    {
        _lastBlocks.TryAdd(container, LastBlock(container));
        if (container is W.Table or W.SdtContentRow)
        {
            yield return container.ChildElements.Where(IsRow).ToArray();
            yield break;
        }

        var segment = new List<OpenXmlElement>();
        foreach (var child in container.ChildElements.ToArray())
        {
            if (child is not (W.Paragraph or W.Table or W.SdtBlock))
            {
                continue;
            }
            segment.Add(child);
            if (child is W.Paragraph { ParagraphProperties.SectionProperties: not null }
                || child is W.SdtBlock control && control.Descendants<W.SectionProperties>().Any())
            {
                yield return segment;
                segment = [];
            }
        }
        yield return segment;
    }

    protected override OpenXmlElement Clone(OpenXmlElement child) => child.CloneNode(true);

    protected override void InsertAfter(OpenXmlElement node, OpenXmlElement reference) => reference.InsertAfterSelf(node);

    /// <summary>
    /// Removes a block's child. A paragraph carrying section properties keeps them and nothing else, so the section
    /// break stays and none of the paragraph's own formatting — numbering, a page break before it — is left behind
    /// on an empty line.
    /// </summary>
    protected override void Remove(OpenXmlElement child)
    {
        if (child is W.Paragraph { ParagraphProperties: { SectionProperties: not null } properties } sectionEnd)
        {
            foreach (var content in sectionEnd.ChildElements.Where(c => c != properties).ToArray())
            {
                content.Remove();
            }
            foreach (var property in properties.ChildElements.Where(c => c is not W.SectionProperties).ToArray())
            {
                property.Remove();
            }
            return;
        }
        child.Remove();
    }

    protected override bool HoldsNoteOrComment(OpenXmlElement child)
        => child.Descendants().Any(element => element is W.FootnoteReference or W.EndnoteReference
            or W.CommentRangeStart or W.CommentRangeEnd or W.CommentReference);

    /// <summary>
    /// A copy keeps no bookmark, whose name the original keeps, and takes drawing and content-control ids of its own.
    /// An id the copy holds twice — a picture in both copies of a text box, as Word writes it — keeps one new id. Its
    /// VML ids are made unique in the written package (<see cref="VmlShapeIds"/>).
    /// </summary>
    protected override void Scrub(OpenXmlElement copy)
    {
        foreach (var bookmark in copy.Descendants().Where(element => element is W.BookmarkStart or W.BookmarkEnd).ToArray())
        {
            bookmark.Remove();
        }

        var drawings = new Dictionary<uint, uint>();
        foreach (var properties in copy.Descendants<Wp.DocProperties>())
        {
            if (properties.Id?.Value is { } id)
            {
                if (!drawings.TryGetValue(id, out var renumbered))
                {
                    drawings[id] = renumbered = NextDrawingId();
                }
                properties.Id = renumbered;
            }
        }

        var controls = new Dictionary<int, int>();
        foreach (var control in copy.Descendants<W.SdtId>())
        {
            if (control.Val?.Value is { } id)
            {
                if (!controls.TryGetValue(id, out var renumbered))
                {
                    controls[id] = renumbered = NextControlId();
                }
                control.Val = renumbered;
            }
        }
    }

    /// <summary>
    /// Whatever holds paragraphs ends with one — a body, cell, header, footer, text box or content control; Word
    /// refuses a cell or text box without one — so one is added when the resolved blocks leave the container empty or
    /// ending in a table or a content control. A table left without rows goes.
    /// </summary>
    protected override void Finish(OpenXmlElement container)
    {
        if (container is W.Table or W.SdtContentRow)
        {
            // a table, or a row-level content control, left without rows goes
            var emptied = container is W.SdtContentRow ? container.Parent : container;
            if (!container.ChildElements.Any(IsRow) && emptied?.Parent is { } owner)
            {
                _lastBlocks.TryAdd(owner, LastBlock(owner));
                emptied.Remove();
                Finish(owner);
            }
            return;
        }

        // a container that ends as the template wrote it is left as it is
        var last = LastBlock(container);
        if (_lastBlocks.TryGetValue(container, out var before) && last == before || last is W.Paragraph)
        {
            return;
        }
        if (last != null)
        {
            last.InsertAfterSelf(new W.Paragraph());
        }
        else if (container.GetFirstChild<W.SectionProperties>() is { } sectionProperties)
        {
            sectionProperties.InsertBeforeSelf(new W.Paragraph());
        }
        else
        {
            container.AppendChild(new W.Paragraph());
        }
    }

    /// <summary>
    /// Fills each field in the <c>w:t</c> that holds its tag; a line ending in the value becomes a line break in the
    /// same run, as Word's Shift+Enter.
    /// </summary>
    protected override void Fill(OpenXmlElement paragraph, IReadOnlyList<TemplateField> fields)
    {
        foreach (var text in OwnTexts((W.Paragraph)paragraph).ToArray())
        {
            var value = text.Text;
            foreach (var field in fields)
            {
                // an evaluator, so a '$' in the value is written as it is
                value = field.Pattern.Replace(value, _ => field.Value);
            }
            if (value == text.Text)
            {
                continue;
            }

            var lines = value.ReplaceLineEndings("\n").Split('\n');
            text.Text = lines[0];
            text.Space = SpaceProcessingModeValues.Preserve;
            OpenXmlElement previous = text;
            foreach (var line in lines.Skip(1))
            {
                var lineBreak = new W.Break();
                previous.InsertAfterSelf(lineBreak);
                var next = new W.Text(line) { Space = SpaceProcessingModeValues.Preserve };
                lineBreak.InsertAfterSelf(next);
                previous = next;
            }
        }
    }

    /// <summary>
    /// The paragraph's text, without that of a text box inside it, whose paragraphs are read on their own.
    /// </summary>
    internal static string GetOwnText(W.Paragraph paragraph) => string.Concat(OwnTexts(paragraph).Select(text => text.Text));

    /// <summary>The paragraph's <c>w:t</c> elements, without those of a text box inside it.</summary>
    internal static IEnumerable<W.Text> OwnTexts(W.Paragraph paragraph)
        => paragraph.Descendants<W.Text>().Where(text => text.Ancestors<W.Paragraph>().First() == paragraph);

    // a table's row, or a content control around rows — a repeating section — which a block keeps or copies whole
    private static bool IsRow(OpenXmlElement child) => child is W.TableRow or W.SdtRow;

    private static OpenXmlElement? LastBlock(OpenXmlElement container)
        => container.ChildElements.LastOrDefault(child => child is W.Paragraph or W.Table or W.SdtBlock);

    private uint NextDrawingId()
    {
        _lastDrawingId ??= Parts().SelectMany(root => root.Descendants<Wp.DocProperties>())
            .Select(properties => properties.Id?.Value ?? 0).DefaultIfEmpty(0u).Max();
        _lastDrawingId++;
        return _lastDrawingId.Value;
    }

    private int NextControlId()
    {
        _lastControlId ??= Parts().SelectMany(root => root.Descendants<W.SdtId>())
            .Select(control => control.Val?.Value ?? 0).DefaultIfEmpty(0).Max();
        _lastControlId++;
        return _lastControlId.Value;
    }

    // every story a drawing or content control can sit in: ids are unique across the package
    private IEnumerable<OpenXmlElement> Parts()
    {
        var main = doc.MainDocumentPart;
        if (main == null)
        {
            return [];
        }
        return new OpenXmlPart?[] { main, main.FootnotesPart, main.EndnotesPart }
            .Concat(main.HeaderParts).Concat(main.FooterParts)
            .Select(part => part?.RootElement)
            .OfType<OpenXmlElement>();
    }
}
