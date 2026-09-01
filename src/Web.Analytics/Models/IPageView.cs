namespace Regira.Web.Analytics.Models;

public interface IPageView
{
    long Id { get; set; }
    DateTime TimestampUtc { get; set; }

    /// <summary>
    /// Which site served the page ("Regira.com", "Blog", ...), so several hosts can share one store.
    /// Comes from <c>Analytics:SiteName</c>, defaulting to the entry assembly name.
    /// </summary>
    string SiteName { get; set; }

    string Path { get; set; }
    string? QueryString { get; set; }

    /// <summary>Raw Referer header as sent by the browser.</summary>
    string? Referrer { get; set; }

    /// <summary>
    /// Host part of <see cref="Referrer"/> — the field to group by when asking "where does traffic come from".
    /// Null for direct visits and for self-referrals (navigation within our own site).
    /// </summary>
    string? ReferrerHost { get; set; }

    /// <summary>utm_source / ref / source from the query string, for links we post somewhere ourselves.</summary>
    string? UtmSource { get; set; }

    string? UserAgent { get; set; }

    /// <summary>
    /// Client IP, truncated to /24 (IPv4) or /48 (IPv6) unless masking is switched off. Enrichers run on the
    /// full address before this is written, so the untruncated address never leaves memory.
    /// </summary>
    string? IpAddress { get; set; }

    /// <summary>Flagged rather than dropped, so crawler traffic stays visible but filterable.</summary>
    bool IsBot { get; set; }

    int StatusCode { get; set; }
}