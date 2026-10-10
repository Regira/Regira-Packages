using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Templating;

/// <summary>
/// Template blocks in a Word template: conditions and loops.
/// <para>
/// A paragraph holding only <c>{{#if Key}}</c> opens a condition, one holding only <c>{{else}}</c> starts its
/// alternative, and one holding only <c>{{/if}}</c> closes it; <c>{{#if !Key}}</c> negates the condition. The branch
/// that holds stays, the other goes.
/// </para>
/// <para>
/// A paragraph holding only <c>{{#each Key}}</c> opens a loop and one holding only <c>{{/each}}</c> closes it. What
/// lies between is written once per row the key names (<see cref="TemplateScope.Rows"/>), and its <c>{{Field}}</c>
/// tags read that row; an <c>{{else}}</c> starts what is written when there are no rows.
/// </para>
/// <para>
/// The marker paragraphs go, with anything else they carry. Blocks nest, and each one opens and closes among the
/// children of one container — a body, table cell, text box, content control around whole paragraphs, header or
/// footer, or a table whose rows hold the markers — within one section. Footnotes, endnotes and comments are not read.
/// </para>
/// <para>
/// A document uses blocks when one of its paragraphs <see cref="OpensBlock">opens one</see>, and only then are its
/// blocks resolved. Everything else in a document that uses none stays as it is — marker text among other text, a
/// stray <c>{{else}}</c> or <c>{{/if}}</c> — so a finished document that writes about templates reads and converts
/// unchanged. In a document that uses blocks, each of those throws.
/// </para>
/// <para>
/// The backends own the document model, this class owns the syntax and the decision: a backend lists a
/// container's children as texts — a paragraph's <see cref="VisibleText">visible text</see>, <c>null</c> for
/// anything else, such as a table — and writes the children <see cref="Resolve(IReadOnlyList{string?}, TemplateScope, Func{int, bool}?)"/>
/// places, each in the scope it names. <see cref="TemplateWalk{TNode}"/> does that for a whole document.
/// </para>
/// </summary>
internal static class TemplateBlocks
{
    // NonBacktracking: the texts come from the document, and the markers' optional runs of white space would
    // otherwise take quadratic time on a long run of spaces
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    // #if, #each, else, /if and /each, and anything written like them — {{#unless X}}, {{else if X}}, {{/unless}} — so a
    // marker this syntax does not know fails rather than staying in the document as text
    private static readonly Regex AnyMarker = new(@"\{\{\s*(?:#|/|else\b)[^{}]*\}\}", Options);
    private static readonly Regex IfMarker = new(@"^\{\{\s*#if\s+(?<not>!)?\s*(?<key>[^{}!\s][^{}]*?)\s*\}\}$", Options);
    // no negation: {{#each !Key}} is a marker this syntax does not know
    private static readonly Regex EachMarker = new(@"^\{\{\s*#each\s+(?<key>[^{}!\s][^{}]*?)\s*\}\}$", Options);
    private static readonly Regex ElseMarker = new(@"^\{\{\s*else\s*\}\}$", Options);
    private static readonly Regex EndIfMarker = new(@"^\{\{\s*/if\s*\}\}$", Options);
    private static readonly Regex EndEachMarker = new(@"^\{\{\s*/each\s*\}\}$", Options);

    private const string Containers = "body, table, table cell, text box, content control, header or footer, within one section";

    /// <summary>
    /// Whether the paragraph text opens a block — <c>{{#if Key}}</c>, <c>{{#if !Key}}</c> or <c>{{#each Key}}</c> and
    /// nothing else — the test that tells whether a document uses blocks. A bare <c>{{else}}</c>, <c>{{/if}}</c> or
    /// <c>{{/each}}</c> does not: other template languages write those on lines of their own.
    /// </summary>
    public static bool OpensBlock(string? text)
        => Clean(text) is { } clean && (IfMarker.IsMatch(clean) || EachMarker.IsMatch(clean));

    /// <summary>
    /// Whether the text holds a marker — the test a backend runs on every paragraph of a document that uses blocks,
    /// to find the containers that need resolving.
    /// </summary>
    public static bool ContainsMarker(string? text)
        => text != null && text.Contains("{{") && AnyMarker.IsMatch(text);

    /// <summary>
    /// Whether the text is one marker and nothing else, known to this syntax or not — what makes a table row whose
    /// only text it is a marker row.
    /// </summary>
    public static bool IsMarker(string? text)
        => Clean(text) is { } clean && IsWholeMarker(clean);

    /// <summary>
    /// The container's new children in order: for each, the original child it is written from and the scope it is
    /// filled in. A child placed more than once is copied for each placement after its first; a child not placed —
    /// every marker, the content of a branch that does not hold, a loop without rows — goes.
    /// </summary>
    /// <param name="children">The container's children in order: a paragraph's text, <c>null</c> for any other child.</param>
    /// <param name="scope">The scope the container is read in: the input's, or a row of a loop around the container.</param>
    /// <param name="holdsNote">
    /// Whether a child holds a footnote, endnote or comment, which a loop cannot copy; <c>null</c> when the
    /// container cannot hold one.
    /// </param>
    /// <exception cref="FormatException">
    /// A marker shares its paragraph with other text, a block is not closed, closed twice or by the other kind's
    /// closer, a block has two <c>{{else}}</c>s among these children, or a loop holds a footnote, endnote or comment.
    /// </exception>
    public static IReadOnlyList<Placement> Resolve(IReadOnlyList<string?> children, TemplateScope scope, Func<int, bool>? holdsNote = null)
    {
        var placements = new List<Placement>();
        Place(Parse(children, holdsNote), scope, placements);
        return placements;
    }

    private static List<Node> Parse(IReadOnlyList<string?> children, Func<int, bool>? holdsNote)
    {
        var root = new List<Node>();
        var open = new Stack<Block>();
        List<Node> Current() => open.Count == 0 ? root : open.Peek().Current;

        for (var i = 0; i < children.Count; i++)
        {
            var text = Clean(children[i]);
            if (!ContainsMarker(text))
            {
                // the loop copies this child, unless it sits in the loop's else
                if (holdsNote != null && open.FirstOrDefault(block => block.IsLoop && !block.InElse) is { } loop && holdsNote(i))
                {
                    throw new FormatException(
                        $"The template's {Excerpt(loop.Marker)} holds a footnote, endnote or comment, which a loop cannot copy. Move it out of the loop.");
                }
                Current().Add(new Child(i));
                continue;
            }

            Block? opened = null;
            if (IfMarker.Match(text!) is { Success: true } condition)
            {
                opened = new Block(text!, condition.Groups["key"].Value, isLoop: false, negated: condition.Groups["not"].Success);
            }
            else if (EachMarker.Match(text!) is { Success: true } loop)
            {
                opened = new Block(text!, loop.Groups["key"].Value, isLoop: true, negated: false);
            }
            else if (ElseMarker.IsMatch(text!))
            {
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {Excerpt(text!)} has no {{{{#if}}}} or {{{{#each}}}} before it in the same {Containers}.");
                }
                var block = open.Peek();
                if (block.InElse)
                {
                    throw new FormatException($"The template's {Excerpt(block.Marker)} has more than one {{{{else}}}}.");
                }
                block.InElse = true;
            }
            else if (EndIfMarker.IsMatch(text!) || EndEachMarker.IsMatch(text!))
            {
                var closesLoop = EndEachMarker.IsMatch(text!);
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {Excerpt(text!)} has no {Opener(closesLoop)} before it in the same {Containers}.");
                }
                var block = open.Peek();
                if (block.IsLoop != closesLoop)
                {
                    throw new FormatException($"The template's {Excerpt(text!)} closes {Excerpt(block.Marker)}, which closes with {Closer(block.IsLoop)}.");
                }
                open.Pop();
            }
            else if (IsWholeMarker(text!))
            {
                throw new FormatException(
                    $"The template's {Excerpt(text!)} is not a template marker. Write {{{{#if Key}}}}, {{{{#if !Key}}}}, {{{{#each Key}}}}, {{{{else}}}}, {{{{/if}}}} or {{{{/each}}}}.");
            }
            else
            {
                throw new FormatException(
                    $"The template's paragraph \"{Excerpt(text!)}\" holds a template marker among other text. " +
                    "Each {{#if Key}}, {{#each Key}}, {{else}}, {{/if}} and {{/each}} stands alone in its own paragraph.");
            }

            if (opened != null)
            {
                Current().Add(opened);
                open.Push(opened);
            }
        }

        if (open.Count > 0)
        {
            var block = open.Peek();
            throw new FormatException($"The template's {Excerpt(block.Marker)} has no {Closer(block.IsLoop)} after it in the same {Containers}.");
        }

        return root;
    }

    private static void Place(IEnumerable<Node> nodes, TemplateScope scope, List<Placement> placements)
    {
        foreach (var node in nodes)
        {
            switch (node)
            {
                case Child child:
                    placements.Add(new Placement(child.Index, scope));
                    break;
                case Block { IsLoop: false } condition:
                    Place(scope.Evaluate(condition.Key) != condition.Negated ? condition.Body : condition.Else, scope, placements);
                    break;
                case Block loop:
                    var rows = scope.Rows(loop.Key);
                    if (rows.Count == 0)
                    {
                        Place(loop.Else, scope, placements);
                    }
                    foreach (var row in rows)
                    {
                        Place(loop.Body, row, placements);
                    }
                    break;
            }
        }
    }

    private static string Opener(bool loop) => loop ? "{{#each}}" : "{{#if}}";
    private static string Closer(bool loop) => loop ? "{{/each}}" : "{{/if}}";

    // enough of a paragraph or marker to find it, without a whole page of document text in the message
    private static string Excerpt(string text)
        => text.Length <= 80 ? text : text[..80] + "…";

    private static bool IsWholeMarker(string text)
        => text.StartsWith("{{", StringComparison.Ordinal)
            && AnyMarker.Match(text) is { Success: true, Index: 0 } marker && marker.Length == text.Length;

    /// <summary>
    /// A key found in <see cref="WordTemplateInput.GlobalParameters"/> holds when its value <see cref="IsTrue(object?)">is
    /// true</see>; one found in <see cref="WordTemplateInput.CollectionParameters"/> holds when the collection has
    /// rows. A key found in neither is false. Keys match regardless of case, as the <c>{{Key}}</c> substitution does,
    /// and an exact match in either comes before a match that differs in case.
    /// </summary>
    internal static bool Evaluate(WordTemplateInput input, string key)
    {
        foreach (var ignoreCase in new[] { false, true })
        {
            if (TryFind(input.GlobalParameters, key, ignoreCase, out var value))
            {
                return IsTrue(value);
            }
            if (TryFind(input.CollectionParameters, key, ignoreCase, out var rows))
            {
                return rows?.Count > 0;
            }
        }
        return false;
    }

    /// <summary>
    /// False for <c>null</c>, <c>false</c>, an empty or blank string, zero, and an empty collection; true for any
    /// other value. A JSON value — a <see cref="JsonElement"/> or a <see cref="JsonNode"/>, how parameters arrive when
    /// an API deserialises them — is read by its kind, and a JSON object counts as a collection of its properties.
    /// </summary>
    internal static bool IsTrue(object? value)
        => value switch
        {
            null => false,
            bool flag => flag,
            string text => !string.IsNullOrWhiteSpace(text),
            JsonElement json => IsTrue(json),
            JsonValue json => json.TryGetValue<JsonElement>(out var element)
                ? IsTrue(element)
                : IsTrue(json.GetValue<object>()),
            Enum => true,
            IConvertible convertible => IsTrue(convertible),
            ICollection collection => collection.Count > 0,
            IEnumerable items => HasAny(items),
            _ => true
        };

    private static bool IsTrue(JsonElement json)
        => json.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.String => !string.IsNullOrWhiteSpace(json.GetString()),
            JsonValueKind.Number => !json.TryGetDouble(out var number) || number != 0,
            JsonValueKind.Array => json.GetArrayLength() > 0,
            JsonValueKind.Object => json.EnumerateObject().Any(),
            // False, Null, Undefined
            _ => false
        };

    /// <summary>
    /// The primitive numbers, and any other value that reports its type code — a JSON library's value
    /// wrapper, for example.
    /// </summary>
    private static bool IsTrue(IConvertible value)
    {
        switch (value.GetTypeCode())
        {
            case TypeCode.Empty:
            case TypeCode.DBNull:
                return false;
            case TypeCode.Boolean:
                return value.ToBoolean(null);
            case TypeCode.String:
                return !string.IsNullOrWhiteSpace(value.ToString(null));
            case TypeCode.SByte:
            case TypeCode.Byte:
            case TypeCode.Int16:
            case TypeCode.UInt16:
            case TypeCode.Int32:
            case TypeCode.UInt32:
            case TypeCode.Int64:
            case TypeCode.UInt64:
            case TypeCode.Single:
            case TypeCode.Double:
            case TypeCode.Decimal:
                var number = value.ToDouble(null);
                return number != 0 && !double.IsNaN(number);
            default:
                return true;
        }
    }

    private static bool HasAny(IEnumerable items)
    {
        var enumerator = items.GetEnumerator();
        try
        {
            return enumerator.MoveNext();
        }
        finally
        {
            (enumerator as IDisposable)?.Dispose();
        }
    }

    internal static bool TryFind<T>(IEnumerable<KeyValuePair<string, T>>? values, string key, bool ignoreCase, out T? value)
    {
        value = default;
        if (values == null)
        {
            return false;
        }
        if (!ignoreCase)
        {
            if (values is IDictionary<string, T> dictionary)
            {
                return dictionary.TryGetValue(key, out value);
            }
            if (values is IReadOnlyDictionary<string, T> readOnly)
            {
                return readOnly.TryGetValue(key, out value);
            }
        }
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                value = pair.Value;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// The paragraph's text without the white space and control characters around it: a backend may report a
    /// paragraph or cell end with it.
    /// </summary>
    private static string? Clean(string? text)
    {
        if (text == null)
        {
            return null;
        }
        var start = 0;
        var end = text.Length - 1;
        while (start <= end && IsPadding(text[start])) start++;
        while (end >= start && IsPadding(text[end])) end--;
        return text.Substring(start, end - start + 1);

        static bool IsPadding(char c) => char.IsWhiteSpace(c) || char.IsControl(c);
    }

    private abstract class Node;

    private sealed class Child(int index) : Node
    {
        public int Index { get; } = index;
    }

    private sealed class Block(string marker, string key, bool isLoop, bool negated) : Node
    {
        public string Marker { get; } = marker;
        public string Key { get; } = key;
        public bool IsLoop { get; } = isLoop;
        public bool Negated { get; } = negated;
        public List<Node> Body { get; } = [];
        public List<Node> Else { get; } = [];
        public bool InElse { get; set; }
        /// <summary>The branch the next child joins.</summary>
        public List<Node> Current => InElse ? Else : Body;
    }
}

/// <summary>A container's child as <see cref="TemplateBlocks.Resolve(IReadOnlyList{string?}, TemplateScope, Func{int, bool}?)"/> places it.</summary>
/// <param name="Child">The index of the original child it is written from.</param>
/// <param name="Scope">The scope its fields are filled in.</param>
internal readonly record struct Placement(int Child, TemplateScope Scope);
