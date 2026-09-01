using System.ComponentModel.DataAnnotations;

namespace Regira.Web.Analytics.Models;

/// <summary>
/// Default <see cref="IPageView"/> implementation. Deliberately holds nothing that ties two visits to the
/// same person; derive from it (or implement <see cref="IPageView"/>) to add host-specific dimensions.
/// </summary>
public class PageView : IPageView
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }

    [MaxLength(64)]
    public string SiteName { get; set; } = null!;

    [MaxLength(256)]
    public string Path { get; set; } = null!;
    [MaxLength(512)]
    public string? QueryString { get; set; }

    [MaxLength(512)]
    public string? Referrer { get; set; }
    [MaxLength(256)]
    public string? ReferrerHost { get; set; }
    [MaxLength(128)]
    public string? UtmSource { get; set; }

    [MaxLength(512)]
    public string? UserAgent { get; set; }

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    public bool IsBot { get; set; }
    public int StatusCode { get; set; }
}