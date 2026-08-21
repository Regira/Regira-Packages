namespace Regira.Entities.Keywords;

/// <remarks>
/// Every keyword comes in two families, told apart by the <c>Trimmed</c> prefix: raw
/// (<see cref="Trimmed"/>, <see cref="TrimmedStartsWith"/>, <see cref="TrimmedEndsWith"/>,
/// <see cref="TrimmedQ"/>, <see cref="TrimmedQW"/>) and normalized (<see cref="Normalized"/>,
/// <see cref="StartsWith"/>, <see cref="EndsWith"/>, <see cref="Q"/>, <see cref="QW"/>).
/// Match a raw column against the raw family and a normalized column against the normalized one —
/// pairing them the other way compiles and silently matches nothing.
/// </remarks>
public class QKeyword
{
    /// <summary>
    /// Unmodified input
    /// </summary>
    public string? Keyword { get; set; }
    /// <summary>
    /// Has a wildcard at the beginning
    /// </summary>
    public bool HasWildcardAtStart { get; set; }
    /// <summary>
    /// Has a wildcard at the end
    /// </summary>
    public bool HasWildcardAtEnd { get; set; }
    /// <summary>
    /// Original input, but stripped from wildcards
    /// </summary>
    public string? Trimmed { get; set; }
    /// <summary>
    /// Trimmed keyword with wildcard at the end
    /// </summary>
    public string? TrimmedStartsWith { get; set; }
    /// <summary>
    /// Trimmed keyword with wildcard at the beginning
    /// </summary>
    public string? TrimmedEndsWith { get; set; }
    /// <summary>
    /// Trimmed keyword with wildcards if given
    /// </summary>
    public string? TrimmedQ { get; set; }
    /// <summary>
    /// Trimmed keyword with wildcards (always at start &amp; end)
    /// </summary>
    public string? TrimmedQW { get; set; }
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