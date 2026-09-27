using System.Collections;
using System.Text.Json;
using System.Text.RegularExpressions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Templating;

/// <summary>
/// Conditional blocks in a Word template. A paragraph holding only <c>{{#if Key}}</c> opens a block, one holding
/// only <c>{{else}}</c> starts its alternative, and one holding only <c>{{/if}}</c> closes it; <c>{{#if !Key}}</c>
/// negates the condition. The branch that holds stays, the other goes, and so do the marker paragraphs. Blocks
/// nest, and each one opens and closes among the children of one container — a body, table cell, header or footer.
/// <para>
/// The backends own the document model, this class owns the syntax and the decision: a backend lists a
/// container's children as texts — a paragraph's text, <c>null</c> for anything else, such as a table — and
/// removes the children <see cref="Resolve(IReadOnlyList{string?}, WordTemplateInput)"/> names.
/// </para>
/// </summary>
internal static class ConditionalBlocks
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private static readonly Regex AnyMarker = new(@"\{\{\s*(#if\b[^{}]*|else|/if)\s*\}\}", Options);
    private static readonly Regex IfMarker = new(@"^\{\{\s*#if\s+(?<not>!)?\s*(?<key>[^{}!\s][^{}]*?)\s*\}\}$", Options);
    private static readonly Regex ElseMarker = new(@"^\{\{\s*else\s*\}\}$", Options);
    private static readonly Regex EndMarker = new(@"^\{\{\s*/if\s*\}\}$", Options);

    private const string Containers = "body, table cell, header or footer";

    /// <summary>
    /// Whether the text holds a marker — the test a backend runs on every paragraph to find the containers
    /// that need resolving.
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

        for (var i = 0; i < children.Count; i++)
        {
            var text = Clean(children[i]);
            if (!ContainsMarker(text))
            {
                if (open.Any(block => !block.Holds))
                {
                    removed.Add(i);
                }
                continue;
            }

            removed.Add(i);
            if (IfMarker.Match(text!) is { Success: true } opening)
            {
                var condition = evaluate(opening.Groups["key"].Value);
                open.Push(new Block(text!, opening.Groups["not"].Success ? !condition : condition));
            }
            else if (ElseMarker.IsMatch(text!))
            {
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {text} has no {{{{#if}}}} before it in the same {Containers}.");
                }
                var block = open.Pop();
                if (block.InElse)
                {
                    throw new FormatException($"The template's {block.Marker} has more than one {{{{else}}}}.");
                }
                open.Push(block with { InElse = true });
            }
            else if (EndMarker.IsMatch(text!))
            {
                if (open.Count == 0)
                {
                    throw new FormatException($"The template's {text} has no {{{{#if}}}} before it in the same {Containers}.");
                }
                open.Pop();
            }
            else if (AnyMarker.Match(text!) is { Success: true } marker && marker.Length == text!.Length)
            {
                throw new FormatException(
                    $"The template's {text} is not a conditional marker. Write {{{{#if Key}}}}, {{{{#if !Key}}}}, {{{{else}}}} or {{{{/if}}}}.");
            }
            else
            {
                throw new FormatException(
                    $"The template's paragraph \"{text}\" holds a conditional marker among other text. " +
                    "Each {{#if Key}}, {{else}} and {{/if}} stands alone in its own paragraph.");
            }
        }

        if (open.Count > 0)
        {
            throw new FormatException($"The template's {open.Peek().Marker} has no {{{{/if}}}} after it in the same {Containers}.");
        }

        return removed;
    }

    /// <summary>
    /// A key found in <see cref="WordTemplateInput.GlobalParameters"/> holds when its value <see cref="IsTrue(object?)">is
    /// true</see>; one found in <see cref="WordTemplateInput.CollectionParameters"/> holds when the collection has
    /// rows. A key found in neither is false. Keys match regardless of case, as the <c>{{Key}}</c> substitution does.
    /// </summary>
    internal static bool Evaluate(WordTemplateInput input, string key)
    {
        if (TryFind(input.GlobalParameters, key, out var value))
        {
            return IsTrue(value);
        }
        if (TryFind(input.CollectionParameters, key, out var rows))
        {
            return rows?.Count > 0;
        }
        return false;
    }

    /// <summary>
    /// False for <c>null</c>, <c>false</c>, an empty or blank string, zero, and an empty collection; true for any
    /// other value. A JSON value — how parameters arrive when an API deserialises them — is read by its kind.
    /// </summary>
    internal static bool IsTrue(object? value)
        => value switch
        {
            null => false,
            bool flag => flag,
            string text => !string.IsNullOrWhiteSpace(text),
            JsonElement json => IsTrue(json),
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
            JsonValueKind.Object => true,
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

    private static bool TryFind<T>(IDictionary<string, T>? values, string key, out T? value)
    {
        value = default;
        if (values == null)
        {
            return false;
        }
        if (values.TryGetValue(key, out value))
        {
            return true;
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
