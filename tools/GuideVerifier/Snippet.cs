namespace Regira.GuideVerifier;

/// <summary>
/// How a snippet is emitted into the generated compilation.
/// </summary>
public enum SnippetKind
{
    /// <summary>Top-level type / namespace / using declarations — emitted at namespace scope.</summary>
    Declarations,

    /// <summary>Statements or expressions — wrapped in an async method body.</summary>
    Statements,
}

/// <summary>
/// One fenced <c>```csharp</c> block pulled from a guide, with enough provenance to report a failure as
/// <c>file.md § heading</c>.
/// </summary>
public record Snippet(
    string RelativeFile,   // e.g. src/Common.Entities/ai/entities.examples.md
    string Heading,        // nearest preceding markdown heading (or "(intro)")
    int FenceLine,         // 1-based line of the opening fence, for the report
    string Code,
    SnippetKind Kind)
{
    /// <summary>Unique identifier used for the generated file / type / method names.</summary>
    public string Id { get; init; } = "";

    public string Location => $"{RelativeFile} § {Heading}";
}
