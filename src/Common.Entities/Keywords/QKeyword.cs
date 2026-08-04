namespace Regira.Entities.Keywords;

public class QKeyword
{
    /// <summary>
    /// Unmodified input
    /// </summary>
    public string? Keyword { get; set; }
    /// <summary>
    /// Has a wildcard at the end
    /// </summary>
    public bool HasWildcardAtStart { get; set; }
    /// <summary>
    /// Has a wildcard at the beginning
    /// </summary>
    public bool HasWildcardAtEnd { get; set; }
    /// <summary>
    /// Original input, but stripped from wildcards
    /// </summary>
    public string? Trimmed { get; set; }
    /// <summary>
    /// Normalized keyword
    /// </summary>
    public string? Normalized { get; set; }
    /// <summary>
    /// Normalized keyword with wildcard at the end
    /// </summary>
    public string? StartsWith { get; set; }
    /// <summary>
    /// Normalized keyword with wildcard at the beginning
    /// </summary>
    public string? EndsWith { get; set; }
    /// <summary>
    /// Normalized keyword with wildcards if given
    /// </summary>
    public string? Q { get; set; }
    /// <summary>
    /// Normalized keyword with wildcards (always at start &amp; end)
    /// </summary>
    public string? QW { get; set; }

    public bool HasWildcard => HasWildcardAtStart || HasWildcardAtEnd;
}