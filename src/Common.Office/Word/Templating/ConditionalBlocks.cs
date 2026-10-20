using Regira.Office.Word.Models;

namespace Regira.Office.Word.Templating;

// Kept for the Word backends published at 6.5.0 (Spire, Syncfusion, Aspose, Mini, Gotenberg): they reach this internal
// class through InternalsVisibleTo and call these three members on every Create, Convert and Merge, so removing it breaks
// them as soon as an app pulls a newer Regira.Office. The current backends use TemplateBlocks.
/// <summary>
/// The conditions of <see cref="TemplateBlocks"/>, without its loops: <c>{{#if Key}}</c>, <c>{{#if !Key}}</c>,
/// <c>{{else}}</c> and <c>{{/if}}</c>. A loop marker is not part of this syntax, so a document holding one throws.
/// </summary>
internal static class ConditionalBlocks
{
    /// <summary>Whether the paragraph text opens a condition — <c>{{#if Key}}</c> or <c>{{#if !Key}}</c> and nothing else.</summary>
    public static bool OpensBlock(string? text) => TemplateBlocks.OpensCondition(text);

    /// <summary>Whether the text holds a marker.</summary>
    public static bool ContainsMarker(string? text) => TemplateBlocks.ContainsMarker(text);

    /// <summary>
    /// The indices of the children to remove: every marker, and the content of each branch that does not hold.
    /// </summary>
    /// <exception cref="FormatException">
    /// A marker shares its paragraph with other text, a block is not closed, closed twice or given two
    /// <c>{{else}}</c>s, or a child is a loop marker.
    /// </exception>
    public static ISet<int> Resolve(IReadOnlyList<string?> children, WordTemplateInput input)
    {
        if (children.FirstOrDefault(TemplateBlocks.IsLoopMarker) is { } loop)
        {
            throw new FormatException($"The template's {loop.Trim()} is not a conditional marker. Write {{{{#if Key}}}}, {{{{#if !Key}}}}, {{{{else}}}} or {{{{/if}}}}.");
        }
        var placed = TemplateBlocks.Resolve(children, TemplateScope.Root(input)).Select(placement => placement.Child).ToHashSet();
        return Enumerable.Range(0, children.Count).Where(i => !placed.Contains(i)).ToHashSet();
    }
}
