namespace Regira.Web.Analytics.Config;

/// <summary>
/// Bound from Analytics:BotDetection. Arrays default to empty because the configuration binder appends
/// rather than replaces — a default here would be impossible to remove from a host's file.
/// </summary>
public class BotDetectionConfig
{
    /// <summary>User agents shorter than this are flagged as bots; 0 turns the check off.</summary>
    public int MinUserAgentLength { get; set; } = 12;

    /// <summary>
    /// Flag user agents that name none of <see cref="BrowserTokens"/>. Catches HTTP clients and one-off
    /// crawlers no marker list knows about; false leaves detection to the markers alone.
    /// </summary>
    public bool RequireBrowserToken { get; set; } = true;

    /// <summary>
    /// Flag requests for a target no visitor could have navigated to — a path in
    /// <see cref="ProbePaths"/>, a dot-directory, or a path climbing out of the site root. The one
    /// signal that survives a scanner copying a real browser's user agent.
    /// </summary>
    public bool DetectProbeRequests { get; set; } = true;

    /// <summary>Merge the package's built-in rule lists under the configured ones; false = configured only.</summary>
    public bool IncludeDefaultMarkers { get; set; } = true;

    /// <summary>User-agent substrings that flag a visit as a bot.</summary>
    public string[] Markers { get; set; } = [];

    /// <summary>Substrings that neutralise a marker they overlap with (false positives of short markers).</summary>
    public string[] Exceptions { get; set; } = [];

    /// <summary>
    /// Substrings that mark a user agent as a browser, for <see cref="RequireBrowserToken"/>. Add the
    /// product token of a non-browser client whose visits should still count as human.
    /// </summary>
    public string[] BrowserTokens { get; set; } = [];

    /// <summary>
    /// Substrings that mark a request as a probe, for <see cref="DetectProbeRequests"/>, matched
    /// against path + query. Add the paths this site is swept for but does not serve.
    /// </summary>
    public string[] ProbePaths { get; set; } = [];
}
