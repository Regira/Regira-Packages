using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Regira.Office.Word.Mini.Internal;

/// <summary>
/// MiniWord splits every text it writes on what reads as a tag — <c>&lt;</c> then a letter or <c>/</c>, up to a
/// <c>&gt;</c> — and puts each piece on a line of its own, losing a space at times. A value is written whole when a word
/// joiner (U+2060) follows each such <c>&lt;</c>; <see cref="Restore"/> takes the joiners out of the written document,
/// so its text equals the values as given. Template text is left alone: MiniWord's own <c>{{if(a &lt; b)}}</c> uses
/// <c>&lt;</c>.
/// </summary>
internal static class TagLikeText
{
    private const string Joined = "<\u2060";
    private static readonly Regex TagStart = new("<(?=[A-Za-z/])", RegexOptions.Compiled);

    public static string Neutralise(string value) => TagStart.Replace(value, Joined);

    public static object Neutralise(object value) => value is string text ? Neutralise(text) : value;

    /// <summary>The row itself when none of its texts reads as a tag, otherwise a copy holding them neutralised.</summary>
    public static IDictionary<string, object> Neutralise(IDictionary<string, object> row)
        => row.Values.Any(value => value is string text && TagStart.IsMatch(text))
            ? row.ToDictionary(field => field.Key, field => Neutralise(field.Value))
            : row;

    /// <summary>Whether a text in <paramref name="roots"/> holds a joiner <see cref="Neutralise(string)"/> wrote.</summary>
    public static bool IsHeldIn(IEnumerable<OpenXmlElement> roots)
        => roots.SelectMany(root => root.Descendants<W.Text>()).Any(text => text.Text.Contains(Joined));

    public static void Restore(IEnumerable<OpenXmlElement> roots)
    {
        foreach (var text in roots.SelectMany(root => root.Descendants<W.Text>()).Where(text => text.Text.Contains(Joined)))
        {
            text.Text = text.Text.Replace(Joined, "<");
        }
    }
}
