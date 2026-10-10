using RazorEngineCore;
using System.Text.Encodings.Web;

namespace Regira.Web.HTML.RazorEngineCore;

/// <summary>
/// Template base that HTML-encodes every value a template writes with <c>@</c>, in text and in attribute values,
/// with <see cref="HtmlEncoder.Default"/>. The template's own text, attribute text included, is written as it stands.
/// <see cref="RazorTemplateParser"/> compiles against it when <see cref="RazorTemplateParser.Options.HtmlEncode"/> is set.
/// </summary>
public abstract class HtmlEncodingTemplateBase : RazorEngineTemplateBase
{
    private sealed class RawContent(object? value)
    {
        public object? Value { get; } = value;
        public override string? ToString() => Value?.ToString();
    }

    /// <summary>
    /// Writes <paramref name="value"/> unencoded: <c>@Raw(Model.Body)</c>.
    /// </summary>
    public object Raw(object? value) => new RawContent(value);

    public override void Write(object? obj = null)
        => base.Write(obj is RawContent raw ? raw.Value : Encode(obj));

    public override void WriteAttributeValue(string prefix, int prefixOffset, object? value, int valueOffset, int valueLength, bool isLiteral)
    {
        // a literal is the template's own attribute text, already written as HTML
        var output = isLiteral ? value : value is RawContent raw ? raw.Value : Encode(value);
        base.WriteAttributeValue(prefix, prefixOffset, output, valueOffset, valueLength, isLiteral);
    }

    private static string? Encode(object? value)
        => value == null ? null : HtmlEncoder.Default.Encode(value.ToString() ?? string.Empty);
}
