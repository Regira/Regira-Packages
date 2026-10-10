using System.Collections;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Regira.Office.Word.Models;
using Regira.Utilities;

namespace Regira.Office.Word.Templating;

/// <summary>
/// What a template's tags read where they stand: the input at the root, and inside a loop the loop's current row,
/// then the rows of the loops around it, innermost first.
/// </summary>
internal sealed class TemplateScope
{
    // a tag the global pass would fill: {{ key }}, the spaces optional
    private static readonly Regex Tag = new(@"\{\{ *(?<key>[^{}]*?) *\}\}", RegexOptions.CultureInvariant);

    private readonly Func<string, bool> _evaluate;
    private readonly IReadOnlyCollection<KeyValuePair<string, ICollection<IDictionary<string, object>>>>? _collections;
    private readonly IEnumerable<KeyValuePair<string, object?>>? _row;
    // one pattern per key, shared by every scope of a document: a loop fills the same few keys in every row
    private readonly Dictionary<string, Regex> _patterns;

    private TemplateScope(Func<string, bool> evaluate, IReadOnlyCollection<KeyValuePair<string, ICollection<IDictionary<string, object>>>>? collections,
        TemplateScope? parent = null, IEnumerable<KeyValuePair<string, object?>>? row = null, int rowNumber = 0)
    {
        _evaluate = evaluate;
        _collections = collections;
        _patterns = parent?._patterns ?? new Dictionary<string, Regex>(StringComparer.OrdinalIgnoreCase);
        Parent = parent;
        _row = row;
        RowNumber = rowNumber;
    }

    /// <summary>The input's scope, outside every loop.</summary>
    public static TemplateScope Root(WordTemplateInput input)
        => new(key => TemplateBlocks.Evaluate(input, key), input.CollectionParameters?.ToArray());

    /// <summary>
    /// A scope outside every loop whose conditions <paramref name="evaluate"/> decides: it names no rows, so every loop in
    /// it writes its else. For the syntax's tests, which decide conditions without an input.
    /// </summary>
    internal static TemplateScope Root(Func<string, bool> evaluate) => new(evaluate, null);

    /// <summary>Whether this is the input's scope, outside every loop.</summary>
    public bool IsRoot => _row == null;

    /// <summary>The scope of the loop around this one; <c>null</c> at the root.</summary>
    public TemplateScope? Parent { get; }

    /// <summary>The row's position in its loop, from 1; 0 at the root.</summary>
    public int RowNumber { get; }

    /// <summary>
    /// The value a <c>{{Key}}</c> tag reads here: <c>row_number</c> is the current row's position, any other key the
    /// first row that has it, from the current row outward. Keys match regardless of case, an exact match in a row
    /// first. False at the root and for a key no row has: the global pass fills that tag.
    /// </summary>
    public bool TryGetField(string key, out object? value)
    {
        value = null;
        if (IsRoot)
        {
            return false;
        }
        if (string.Equals(key, "row_number", StringComparison.OrdinalIgnoreCase))
        {
            value = RowNumber;
            return true;
        }
        return TryGetRowValue(key, out value);
    }

    /// <summary>
    /// Whether <c>{{#if Key}}</c> holds here: the first row that has the key decides, from the current row outward,
    /// by whether its value <see cref="TemplateBlocks.IsTrue(object?)">is true</see>; outside every row, the input does
    /// (<see cref="TemplateBlocks.Evaluate"/>).
    /// </summary>
    public bool Evaluate(string key)
        => TryGetRowValue(key, out var value) ? TemplateBlocks.IsTrue(value) : _evaluate(key);

    /// <summary>
    /// The rows <c>{{#each Key}}</c> runs over, one scope each: those of the first row that has the key, from the
    /// current row outward, then those of the <see cref="WordTemplateInput.CollectionParameters"/> entry, the key
    /// matched regardless of case, an exact match first. A key that names no list of rows — a missing key, a
    /// <see cref="WordTemplateInput.GlobalParameters"/> entry, a row's field holding a single value — gives none.
    /// </summary>
    public IReadOnlyList<TemplateScope> Rows(string key)
    {
        IEnumerable<IEnumerable<KeyValuePair<string, object?>>> rows;
        if (TryGetRowValue(key, out var value))
        {
            rows = ReadRows(value);
        }
        else
        {
            rows = TemplateBlocks.TryFind(_collections, key, false, out var collection) || TemplateBlocks.TryFind(_collections, key, true, out collection)
                ? (collection ?? []).Select(row => ReadRow(row))
                : [];
        }
        return rows.Select((row, i) => new TemplateScope(_evaluate, _collections, this, row, i + 1)).ToArray();
    }

    /// <summary>
    /// The fields the tags in <paramref name="text"/> read here (<see cref="TryGetField"/>), each with the text that
    /// fills it: its value as the global pass writes one, <c>ToString()</c> in the current culture. None at the root.
    /// </summary>
    public IReadOnlyList<TemplateField> Fields(string? text)
    {
        if (IsRoot || string.IsNullOrEmpty(text) || !text.Contains("{{"))
        {
            return [];
        }
        var fields = new List<TemplateField>();
        foreach (var key in Tag.Matches(text).Select(match => match.Groups["key"].Value).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (key.Length > 0 && !TemplateBlocks.ContainsMarker($"{{{{{key}}}}}") && TryGetField(key, out var value))
            {
                if (!_patterns.TryGetValue(key, out var pattern))
                {
                    _patterns[key] = pattern = new Regex($"{{{{ *{Regex.Escape(key)} *}}}}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                }
                fields.Add(new TemplateField(key, Write(value), pattern));
            }
        }
        return fields;
    }

    private bool TryGetRowValue(string key, out object? value)
    {
        for (var scope = this; scope is { IsRoot: false }; scope = scope.Parent)
        {
            if (TemplateBlocks.TryFind(scope._row, key, false, out value) || TemplateBlocks.TryFind(scope._row, key, true, out value))
            {
                return true;
            }
        }
        value = null;
        return false;
    }

    /// <summary>
    /// The rows a row's field holds: a sequence of dictionaries or objects, or a JSON array of objects. Anything else —
    /// text, a single object, a JSON value of another kind — holds none.
    /// </summary>
    private static IEnumerable<IEnumerable<KeyValuePair<string, object?>>> ReadRows(object? value)
        => value switch
        {
            null or string or IDictionary or JsonObject => [],
            JsonElement { ValueKind: JsonValueKind.Array } json => json.EnumerateArray().Select(row => ReadRow(row)).ToArray(),
            JsonElement => [],
            JsonArray json => json.Select(row => ReadRow(row)).ToArray(),
            JsonNode => [],
            IEnumerable when IsDictionary(value) => [],
            IEnumerable items => items.Cast<object?>().Select(row => ReadRow(row)).ToArray(),
            _ => []
        };

    private static IEnumerable<KeyValuePair<string, object?>> ReadRow(object? row)
        => row switch
        {
            null => [],
            // IDictionary<string, object>, the rows' type, among them
            IEnumerable<KeyValuePair<string, object?>> pairs => pairs,
            IDictionary dictionary => dictionary.Cast<DictionaryEntry>().Select(entry => new KeyValuePair<string, object?>(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty, entry.Value)),
            JsonElement { ValueKind: JsonValueKind.Object } json => json.EnumerateObject().Select(property => new KeyValuePair<string, object?>(property.Name, property.Value)).ToArray(),
            JsonElement => [],
            JsonObject json => json.Select(property => new KeyValuePair<string, object?>(property.Key, property.Value)).ToArray(),
            JsonNode => [],
            string => [],
            // one level: a value stays as it is, so a reference back to the row around it — an ORM's navigation
            // property — is not followed, and a value that is no list is written as itself
            _ => DictionaryUtility.ToDictionary(row, RowProperties)
        };

    private static readonly DictionaryOptions RowProperties = new() { Recursive = false };

    // a dictionary is a single row, not a list of them, though it enumerates its entries
    private static bool IsDictionary(object value)
        => value.GetType().GetInterfaces().Any(type => type.IsGenericType
            && (type.GetGenericTypeDefinition() == typeof(IDictionary<,>) || type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)));

    /// <summary>A value as the global pass writes one: <c>ToString()</c>, a JSON string or value read by its kind.</summary>
    private static string Write(object? value)
        => value switch
        {
            null => string.Empty,
            JsonElement { ValueKind: JsonValueKind.String } json => json.GetString() ?? string.Empty,
            JsonElement { ValueKind: JsonValueKind.Null or JsonValueKind.Undefined } => string.Empty,
            JsonValue json when json.TryGetValue<string>(out var text) => text,
            _ => value.ToString() ?? string.Empty
        };
}

/// <summary>A tag a row fills.</summary>
/// <param name="Key">The key the tag names.</param>
/// <param name="Value">The text the tag is replaced with.</param>
/// <param name="Pattern"><c>{{ Key }}</c>, the spaces inside the braces optional, regardless of case.</param>
internal sealed record TemplateField(string Key, string Value, Regex Pattern);
