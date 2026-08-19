using Regira.Entities.Keywords.Abstractions;
using Regira.Normalizing;
using Regira.Normalizing.Abstractions;

namespace Regira.Entities.Keywords;

public class QKeywordHelperOptions
{
    /// <summary>
    /// Wildcard character the consumer is using
    /// </summary>
    public string WildcardInput { get; set; } = "*";
    /// <summary>
    /// Wildcard character the data store is using
    /// </summary>
    public string WildcardOutput { get; set; } = "%";
    public bool ApplyNormalize { get; set; } = true;
}
public class QKeywordHelper(QKeywordHelperOptions? options = null, INormalizer? normalizer = null) : IQKeywordHelper
{
    QKeywordHelperOptions Options => options ?? new QKeywordHelperOptions();
    private INormalizer Normalizer => Options.ApplyNormalize
        ? normalizer ?? NormalizingDefaults.DefaultPropertyNormalizer ?? new DefaultNormalizer()
        : null!;



    public ParsedKeywordCollection Parse(string? input)
    {
        var parsedKeywords = input
            ?.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseKeyword)
            ?? [];
        return new ParsedKeywordCollection(parsedKeywords, Options.ApplyNormalize ? Normalizer.Normalize(input) : input);
    }
    public QKeyword ParseKeyword(string? input)
    {
        var isStartingWith = input?.StartsWith(Options.WildcardInput) == true;
        var isEndingWith = input?.EndsWith(Options.WildcardInput) == true;
        var trimmed = input?.Trim(Options.WildcardInput.ToCharArray());

        var startsWith = $"{trimmed}{Options.WildcardOutput}";
        var endsWith = $"{Options.WildcardOutput}{trimmed}";
        var trimmedQ = $"{(isStartingWith ? Options.WildcardOutput : "")}{trimmed}{(isEndingWith ? Options.WildcardOutput : "")}";
        var trimmedQW = $"{Options.WildcardOutput}{trimmed}{Options.WildcardOutput}";

        var normalized = Options.ApplyNormalize ? Normalizer.Normalize(trimmed) : trimmed;
        var normalizedStartsWith = $"{normalized}{Options.WildcardOutput}";
        var normalizedEndsWith = $"{Options.WildcardOutput}{normalized}";
        var q = $"{(isStartingWith ? Options.WildcardOutput : "")}{normalized}{(isEndingWith ? Options.WildcardOutput : "")}";
        var qw = $"{Options.WildcardOutput}{normalized}{Options.WildcardOutput}";
        return new QKeyword
        {
            Keyword = input,
            HasWildcardAtStart = isStartingWith,
            HasWildcardAtEnd = isEndingWith,
            Trimmed = trimmed,
            StartsWith = startsWith,
            EndsWith = endsWith,
            TrimmedQ = trimmedQ,
            TrimmedQW = trimmedQW,
            Normalized = normalized,
            NormalizedStartsWith = normalizedStartsWith,
            NormalizedEndsWith = normalizedEndsWith,
            Q = q,
            QW = qw
        };
    }
}