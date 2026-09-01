namespace Regira.Web.Analytics.GeoIP2.Config;

/// <summary>Bound from Analytics:GeoIP2.</summary>
public class GeoIP2Config
{
    /// <summary>
    /// A GeoIP2/GeoLite2 .mmdb file, or a directory holding one (City preferred over Country). Relative
    /// paths resolve against the content root, then the application base directory. Empty = lookup disabled.
    /// </summary>
    public string? DatabasePath { get; set; }
}