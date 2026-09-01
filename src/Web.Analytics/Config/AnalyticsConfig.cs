namespace Regira.Web.Analytics.Config;

public class AnalyticsConfig
{
    public bool Enabled { get; set; } = true;

    /// <summary>Row discriminator when hosts share one store; empty resolves to the entry assembly name.</summary>
    public string SiteName { get; set; } = "";

    /// <summary>Truncate the stored client IP to /24 (IPv4) or /48 (IPv6); enrichers still see the full address.</summary>
    public bool MaskIpAddress { get; set; } = true;

    /// <summary>Record crawler traffic too (flagged with IsBot); false drops it before it is stored.</summary>
    public bool RecordBots { get; set; } = true;

    /// <summary>Purge cutoff in days; 0 keeps everything. Needs an IPageViewRetentionStore.</summary>
    public int RetentionDays { get; set; } = 365;

    /// <summary>X-Analytics-Key value for the stats route; empty = route not mapped.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Extra path prefixes the default filter never records.</summary>
    public string[] IgnorePaths { get; set; } = [];

    /// <summary>Queue bound; when full, page views are dropped rather than slowing requests down.</summary>
    public int QueueCapacity { get; set; } = 10_000;
    public int BatchSize { get; set; } = 200;
    public int FlushIntervalSeconds { get; set; } = 5;
}