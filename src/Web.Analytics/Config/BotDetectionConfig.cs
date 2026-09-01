namespace Regira.Web.Analytics.Config;

/// <summary>
/// Bound from Analytics:BotDetection. Arrays default to empty because the configuration binder appends
/// rather than replaces — a default here would be impossible to remove from a host's file.
/// </summary>
public class BotDetectionConfig
{
    /// <summary>User agents shorter than this are flagged as bots; 0 turns the check off.</summary>
    public int MinUserAgentLength { get; set; } = 12;

    /// <summary>Merge the package's built-in marker list under the configured one; false = configured only.</summary>
    public bool IncludeDefaultMarkers { get; set; } = true;

    /// <summary>User-agent substrings that flag a visit as a bot.</summary>
    public string[] Markers { get; set; } = [];

    /// <summary>Substrings that clear a user agent before the markers run (false positives of short markers).</summary>
    public string[] Exceptions { get; set; } = [];
}