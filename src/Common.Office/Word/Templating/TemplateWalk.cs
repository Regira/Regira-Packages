using System.Text.RegularExpressions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Templating;

/// <summary>
/// Resolves a document's <see cref="TemplateBlocks">template blocks</see> over a backend's document model, then fills
/// the fields its loops' rows hold. A backend supplies the model's operations; the order of the work lives here.
/// <para>
/// Containers are resolved from the outside in. A container whose own markers are resolved has its children placed
/// as <see cref="TemplateBlocks.Resolve(IReadOnlyList{string?}, TemplateScope, Func{int, bool}?)"/> names them: a
/// child's original stays where it can keep its order, every other placement inserts a copy, and every child placed
/// inside a loop remembers its row. The containers inside it, copies included, are resolved after it, each in the row of the nearest child
/// around it that remembers one. A table whose row holds a single marker and nothing else resolves as a container of
/// rows. Every marker is read before any field is filled, so a value holding marker text is written as text.
/// </para>
/// </summary>
/// <typeparam name="TNode">The model's node: a paragraph, table, row, cell or any other container or child.</typeparam>
internal abstract class TemplateWalk<TNode> where TNode : class
{
    protected enum NodeKind { Paragraph, Table, Row, Cell, Other }

    /// <summary>Every paragraph blocks are read from, in document order: none in a footnote, endnote or comment.</summary>
    protected abstract IEnumerable<TNode> Paragraphs();

    /// <summary>The paragraph's <see cref="VisibleText">visible text</see>.</summary>
    protected abstract string Text(TNode paragraph);

    /// <summary>The node that holds this one; <c>null</c> for the document.</summary>
    protected abstract TNode? Parent(TNode node);

    protected abstract NodeKind KindOf(TNode node);

    /// <summary>
    /// The container's children that blocks are made of, in order, cut into the stretches a block must open and
    /// close in: one per section. A table's children are its rows.
    /// </summary>
    protected abstract IEnumerable<IReadOnlyList<TNode>> Segments(TNode container);

    /// <summary>A deep copy of the child, not yet in the document.</summary>
    protected abstract TNode Clone(TNode child);

    /// <summary>Inserts <paramref name="node"/> right after <paramref name="reference"/>, in the same container.</summary>
    protected abstract void InsertAfter(TNode node, TNode reference);

    /// <summary>Removes the child from its container.</summary>
    protected abstract void Remove(TNode child);

    /// <summary>Whether the child holds a footnote, endnote or comment, which a loop cannot copy.</summary>
    protected abstract bool HoldsNoteOrComment(TNode child);

    /// <summary>
    /// Makes a copy unique in the document: a copy keeps no bookmark, whose name the original keeps, and takes
    /// ids of its own where the model does not assign them.
    /// </summary>
    protected virtual void Scrub(TNode copy)
    {
    }

    /// <summary>
    /// Leaves the container as Word requires it once its children changed: a body, cell, text box or content control
    /// ends with a paragraph, and a table without rows goes.
    /// </summary>
    protected abstract void Finish(TNode container);

    /// <summary>Replaces each field's <see cref="TemplateField.Pattern"/> in the paragraph with its value.</summary>
    protected abstract void Fill(TNode paragraph, IReadOnlyList<TemplateField> fields);

    /// <summary>Resolves the blocks and fills the rows' fields, when the document uses blocks.</summary>
    public void Run(WordTemplateInput input)
    {
        // the texts alone decide, so a document without blocks pays for no more
        var paragraphs = Paragraphs().Select(node => (Node: node, Text: Text(node))).ToArray();
        if (!paragraphs.Any(paragraph => TemplateBlocks.OpensBlock(paragraph.Text)))
        {
            return;
        }
        var read = Read(paragraphs);

        var scopes = new Dictionary<TNode, TemplateScope>(ReferenceEqualityComparer.Instance);
        var root = TemplateScope.Root(input);
        var resolved = new HashSet<TNode>(ReferenceEqualityComparer.Instance);
        while (true)
        {
            var containers = read.Paragraphs
                .Where(paragraph => TemplateBlocks.ContainsMarker(paragraph.Text))
                .Select(paragraph => ContainerOf(paragraph.Node, read))
                .OfType<TNode>()
                .Distinct(ReferenceEqualityComparer.Instance)
                .Cast<TNode>()
                .ToArray();
            if (containers.Length == 0)
            {
                break;
            }

            var marked = new HashSet<TNode>(containers, ReferenceEqualityComparer.Instance);
            // taken before any is resolved: a container a resolution drops or copies is read again next round, if at all
            var outermost = containers.Where(container => !Ancestors(container).Any(marked.Contains)).ToArray();
            foreach (var container in outermost)
            {
                // resolving a container removes all its markers; one that keeps a marker is a model this walk misreads
                if (!resolved.Add(container))
                {
                    throw new InvalidOperationException("Resolving the template's blocks left a marker in place.");
                }
                Resolve(container, ScopeOf(container, scopes) ?? root, scopes, read);
            }
            read = Read();
        }

        // inner paragraphs first: a text box's paragraphs before the paragraph that holds the text box
        var filled = read.Paragraphs
            .OrderByDescending(paragraph => Ancestors(paragraph.Node).Count())
            .Select(paragraph => (paragraph.Node, Fields: ScopeOf(paragraph.Node, scopes) is { IsRoot: false } scope ? scope.Fields(paragraph.Text) : []))
            .Where(paragraph => paragraph.Fields.Count > 0)
            .ToArray();
        if (!filled.Any(paragraph => paragraph.Fields.Any(field => field.Value.Contains("{{", StringComparison.Ordinal))))
        {
            foreach (var (node, fields) in filled)
            {
                Fill(node, fields);
            }
            return;
        }

        // no value may be read as a tag, and a paragraph's fill reaches the text boxes it holds on some models: each field
        // becomes a placeholder of its own first, from the Unicode private use area, and each placeholder its value after
        var placeholders = 0;
        var steps = filled.Select(paragraph => (paragraph.Node, paragraph.Fields, Placeholders: paragraph.Fields.Select(_ => Placeholder(placeholders++)).ToArray())).ToArray();
        foreach (var (node, fields, marks) in steps)
        {
            Fill(node, fields.Select((field, i) => field with { Value = marks[i].Text }).ToArray());
        }
        foreach (var (node, fields, marks) in steps)
        {
            Fill(node, fields.Select((field, i) => field with { Pattern = marks[i].Pattern }).ToArray());
        }
    }

    private static (string Text, Regex Pattern) Placeholder(int index)
    {
        var text = $"{(char)0xE000}{index}{(char)0xE001}";
        return (text, new Regex(Regex.Escape(text), RegexOptions.CultureInvariant));
    }

    private void Resolve(TNode container, TemplateScope scope, Dictionary<TNode, TemplateScope> scopes, Reading read)
    {
        var isTable = KindOf(container) == NodeKind.Table;
        foreach (var segment in Segments(container).ToArray())
        {
            // a table's children are its rows, and the content controls that hold rows: each is a marker or content
            var texts = segment.Select(child => isTable
                ? read.MarkerOf(child)
                : KindOf(child) == NodeKind.Paragraph ? read.TextOf(child) ?? Text(child) : null).ToArray();
            var placements = TemplateBlocks.Resolve(texts, scope, i => HoldsNoteOrComment(segment[i]));

            // a child's original stays where it is when it follows the last original kept, and so everything placed so
            // far; any other placement is a copy, right after what was placed before it. A row taking an else after an
            // earlier row's if is such a copy, its original going with the children not kept.
            var kept = new bool[segment.Count];
            var removed = new bool[segment.Count];
            var written = new bool[segment.Count];
            var lastKept = -1;
            TNode? previous = null;
            foreach (var placement in placements)
            {
                TNode node;
                var child = placement.Child;
                if (!written[child] && child > lastKept)
                {
                    kept[child] = true;
                    lastKept = child;
                    node = segment[child];
                }
                else
                {
                    node = Clone(segment[child]);
                    if (written[child])
                    {
                        Scrub(node);
                    }
                    else
                    {
                        // the first copy stands in for the original, bookmark and ids included: the original goes first,
                        // so the document never holds both
                        Remove(segment[child]);
                        removed[child] = true;
                    }
                    InsertAfter(node, previous!);
                }
                written[child] = true;
                if (!placement.Scope.IsRoot)
                {
                    scopes[node] = placement.Scope;
                }
                previous = node;
            }
            for (var i = 0; i < segment.Count; i++)
            {
                if (!kept[i] && !removed[i])
                {
                    Remove(segment[i]);
                }
            }
        }
        Finish(container);
    }

    /// <summary>
    /// The container a marker paragraph's block resolves in: the table, when the paragraph is the only text of its row —
    /// whatever holds it in its cell, a content control among them — otherwise the paragraph's own container.
    /// </summary>
    private TNode? ContainerOf(TNode paragraph, Reading read)
    {
        if (RowOf(paragraph) is { } row && read.MarkerOf(row) != null
            && Ancestors(row).FirstOrDefault(ancestor => KindOf(ancestor) == NodeKind.Table) is { } table)
        {
            return table;
        }
        return Parent(paragraph);
    }

    /// <summary>
    /// The row whose cell holds the paragraph as cell text, a content control between them or not; <c>null</c> outside a
    /// table, and for a paragraph in a text box, which holds text of its own.
    /// </summary>
    private TNode? RowOf(TNode paragraph)
    {
        foreach (var ancestor in Ancestors(paragraph))
        {
            switch (KindOf(ancestor))
            {
                case NodeKind.Row:
                    return ancestor;
                case NodeKind.Paragraph:
                    return null;
            }
        }
        return null;
    }

    private IEnumerable<TNode> Ancestors(TNode node)
    {
        for (var parent = Parent(node); parent != null; parent = Parent(parent))
        {
            yield return parent;
        }
    }

    /// <summary>The scope of the nearest node that remembers one, the node itself first; <c>null</c> for none.</summary>
    private TemplateScope? ScopeOf(TNode node, Dictionary<TNode, TemplateScope> scopes)
    {
        for (TNode? current = node; current != null; current = Parent(current))
        {
            if (scopes.TryGetValue(current, out var scope))
            {
                return scope;
            }
        }
        return null;
    }

    private Reading Read((TNode Node, string Text)[]? paragraphs = null)
    {
        paragraphs ??= Paragraphs().Select(node => (Node: node, Text: Text(node))).ToArray();
        // for each node from a cell paragraph's row up to the outermost table, the non-empty paragraphs it holds, and
        // whether they are its own: the paragraph's row, and what holds that row in its table — a content control around
        // rows — hold it as their own; an outer table's rows hold it as a nested table's
        var rows = new Dictionary<TNode, List<(string Text, bool Own)>>(ReferenceEqualityComparer.Instance);
        foreach (var (node, text) in paragraphs)
        {
            if (string.IsNullOrWhiteSpace(text) || RowOf(node) is not { } row)
            {
                continue;
            }
            var chain = Ancestors(node).SkipWhile(ancestor => !ReferenceEquals(ancestor, row)).ToList();
            var outermost = chain.FindLastIndex(ancestor => KindOf(ancestor) == NodeKind.Table);
            var own = true;
            foreach (var ancestor in chain.Take(outermost))
            {
                if (KindOf(ancestor) == NodeKind.Table)
                {
                    own = false;
                    continue;
                }
                if (!rows.TryGetValue(ancestor, out var held))
                {
                    rows[ancestor] = held = [];
                }
                held.Add((text, own));
            }
        }
        var markers = new Dictionary<TNode, string>(ReferenceEqualityComparer.Instance);
        foreach (var (row, held) in rows)
        {
            if (held is [{ Own: true } only] && TemplateBlocks.IsMarker(only.Text))
            {
                markers[row] = only.Text;
            }
        }
        var texts = new Dictionary<TNode, string>(ReferenceEqualityComparer.Instance);
        foreach (var (node, text) in paragraphs)
        {
            texts[node] = text;
        }
        return new Reading(paragraphs, texts, markers);
    }

    private sealed record Reading((TNode Node, string Text)[] Paragraphs, Dictionary<TNode, string> Texts, Dictionary<TNode, string> Markers)
    {
        public string? TextOf(TNode paragraph) => Texts.GetValueOrDefault(paragraph);
        /// <summary>The single marker that is the row's only text; <c>null</c> for any other row.</summary>
        public string? MarkerOf(TNode row) => Markers.GetValueOrDefault(row);
    }
}
