namespace Regira.Web.Analytics.Models;

/// <summary>Stats request; the endpoint clamps values before they get here (<see cref="Top"/> is 1–100).</summary>
public class PageViewStatsQuery
{
    public DateTime SinceUtc { get; set; }
    /// <summary>Site to answer for; null spans every site in the store.</summary>
    public string? SiteName { get; set; }
    public bool IncludeBots { get; set; }
    public int Top { get; set; } = 20;
}

public record KeyCount(string? Key, int Views);

public record DayCount(DateTime Date, int Views);

/// <summary>Aggregates a stats store hands back; custom dimensions go into <see cref="Breakdowns"/>.</summary>
public class PageViewStats
{
    public int HumanViews { get; set; }
    public int BotViews { get; set; }
    public IReadOnlyList<DayCount> PerDay { get; set; } = [];
    public IReadOnlyList<KeyCount> TopPaths { get; set; } = [];
    public IReadOnlyList<KeyCount> TopReferrers { get; set; } = [];
    public IReadOnlyList<KeyCount> PerSite { get; set; } = [];
    public IReadOnlyList<IPageView> Recent { get; set; } = [];
    /// <summary>Store-defined extra dimensions, e.g. "country" or "tool" → top-N counts.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<KeyCount>> Breakdowns { get; set; } = new Dictionary<string, IReadOnlyList<KeyCount>>();
}