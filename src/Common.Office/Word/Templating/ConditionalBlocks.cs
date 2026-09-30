using System.Collections;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Templating;

/// <summary>
/// Conditional blocks in a Word template. A paragraph holding only <c>{{#if Key}}</c> opens a block, one holding
/// only <c>{{else}}</c> starts its alternative, and one holding only <c>{{/if}}</c> closes it; <c>{{#if !Key}}</c>
/// negates the condition. The branch that holds stays, the other goes, and so do the marker paragraphs, with anything
/// else they carry. Blocks nest, and each one opens and closes among the children of one container — a body, table
/// cell, text box, content control around whole paragraphs, header or footer. Footnotes, endnotes and comments are not
/// read.
/// <para>
/// A document uses blocks when one of its paragraphs <see cref="OpensBlock">opens one</see>, and only then are its
/// blocks resolved. Everything else in a document that uses none stays as it is — marker text among other text, a
/// stray <c>{{else}}</c> or <c>{{/if}}</c>, another template language's <c>{{#each}}</c> — so a finished document
/// that writes about templates reads and converts unchanged. In a document that uses blocks, each of those throws.
/// </para>
/// <para>
/// The backends own the document model, this class owns the syntax and the decision: a backend lists a
/// container's children as texts — a paragraph's <see cref="VisibleText">visible text</see>, <c>null</c> for
/// anything else, such as a table — and
/// removes the children <see cref="Resolve(IReadOnlyList{string?}, WordTemplateInput)"/> names.
/// </para>
/// </summary>
internal static class ConditionalBlocks
{
    // NonBacktracking: the texts come from the document, and the #if marker's optional runs of white space would
    // otherwise take quadratic time on a long run of spaces
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;
    // #if, else and /if, and anything written like them — {{#unless X}}, {{else if X}}, {{/unless}} — so a marker
    // this syntax does not know fails rather than staying in the document as text
    private static readonly Regex AnyMarker = new(@"\{\{\s*(?:#|/|else\b)[^{}]*\}\}", Options);
    private static readonly Regex IfMarker = new(@"^\{\{\s*#if\s+(?<not>!)?\s*(?<key>[^{}!\s][^{}]*?)\s*\}\}$", Options);
    private static readonly Regex ElseMarker = new(@"^\{\{\s*else\s*\}\}$", Options);
    private static readonly Regex EndMarker = new(@"^\{\{\s*/if\s*\}\}$", Options);

    private const string Containers = "body, table cell, text box, content control, header or footer, within one section";

    /// <summary>
    /// Whether the paragraph text opens a block — <c>{{#if Key}}</c> or <c>{{#if !Key}}</c> and nothing else — the test
    /// that tells whether a document uses blocks. A bare <c>{{else}}</c> or a <c>{{#each}}</c> does not: other template
    /// languages write those on lines of their own.
    /// </summary>
    public static bool OpensBlock(string? text)
        => Clean(text) is { } clean && IfMarker.IsMatch(clean);

    /// <summary>
    /// Whether the text holds a marker — the test a backend runs on every paragraph of a document that uses blocks,
    /// to find the containers that need resolving.
    /// </summary>
    public static bool ContainsMarker(string? text)
        => text != null && text.Contains("{{") && AnyMarker.IsMatch(text);

    /// <summary>
    /// The indices of the children to remove: every marker, and the content of each branch that does not hold.
    /// </summary>
    /// <param name="children">The container's children in order: a paragraph's text, <c>null</c> for any other child.</param>
    /// <param name="input">The input whose parameters decide the conditions.</param>
    /// <exception cref="FormatException">
    /// A marker shares its paragraph with other text, or a block is not closed, closed twice, or given two
    /// <c>{{else}}</c>s among these children.
    /// </exception>
    public static ISet<int> Resolve(IReadOnlyList<string?> children, WordTemplateInput input)
        => Resolve(children, key => Evaluate(input, key));

    internal static ISet<int> Resolve(IReadOnlyList<string?> children, Func<string, bool> evaluate)
    {
        var removed = new HashSet<int>();
        var open = new Stack<Block>();
        // the open blocks whose current branch does not hold: a child goes while any of them is open
        var dropping = 0;
        void Push(Block block)
        {
            open.Push(block);
            dropping += block.Holds ? 0 : 1;
        }
        Block Pop()
        {
            var block = open.Pop();
            dropping -= block.Holds ? 0 : 1;
            return block;
        }

        for (var i = 0; i < children.Count; i++)
        {
            var text = Clean(children[i]);
            if (!ContainsMarker(text))
            {
                if (dropping > 0)
                {
                    removed.Add(i);
                }
                continue;
            }

            removed.Add(i);
            if (IfMarker.Match(text!) is { Success: true } opening)
            {
                var condition = evaluate(opening.Groups["key"].Value);
                Push(new Block(text!, opening.Groups["not"].Success ? !condition : condition));
            }
            else if (ElseMarker.IsMatch(text!))
            {
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {Excerpt(text!)} has no {{{{#if}}}} before it in the same {Containers}.");
                }
                var block = Pop();
                if (block.InElse)
                {
                    throw new FormatException($"The template's {Excerpt(block.Marker)} has more than one {{{{else}}}}.");
                }
                Push(block with { InElse = true });
            }
            else if (EndMarker.IsMatch(text!))
            {
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {Excerpt(text!)} has no {{{{#if}}}} before it in the same {Containers}.");
                }
                Pop();
            }
            else if (IsWholeMarker(text!))
            {
                throw new FormatException(
                    $"The template's {Excerpt(text!)} is not a conditional marker. Write {{{{#if Key}}}}, {{{{#if !Key}}}}, {{{{else}}}} or {{{{/if}}}}.");
            }
            else
            {
                throw new FormatException(
                    $"The template's paragraph \"{Excerpt(text!)}\" holds a conditional marker among other text. " +
                    "Each {{#if Key}}, {{else}} and {{/if}} stands alone in its own paragraph.");
            }
        }

        if (open.Count > 0)
        {
            throw new FormatException($"The template's {Excerpt(open.Peek().Marker)} has no {{{{/if}}}} after it in the same {Containers}.");
        }

        return removed;
    }

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

    private static bool TryFind<T>(IDictionary<string, T>? values, string key, bool ignoreCase, out T? value)
    {
        value = default;
        if (values == null)
        {
            return false;
        }
        if (!ignoreCase)
        {
            return values.TryGetValue(key, out value);
        }
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
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

    private readonly record struct Block(string Marker, bool Condition, bool InElse = false)
    {
        /// <summary>Whether the branch being read is the one that stays.</summary>
        public bool Holds => Condition != InElse;
    }
}
