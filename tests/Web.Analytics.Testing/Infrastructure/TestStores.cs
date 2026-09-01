using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Web.Analytics.Testing.Infrastructure;

/// <summary>In-memory store implementing all three interfaces; the typical fixture for writer/e2e tests.</summary>
public class InMemoryStore<TPageView> : IPageViewStore<TPageView>, IPageViewStatsStore, IPageViewRetentionStore
    where TPageView : IPageView
{
    private readonly Lock _lock = new();
    private readonly List<TPageView> _views = [];

    public int SaveCalls { get; private set; }
    public Func<Exception?>? FailWith { get; set; }
    public PageViewStatsQuery? LastStatsQuery { get; private set; }
    public (string SiteName, DateTime CutoffUtc)? LastPurge { get; private set; }

    public IReadOnlyList<TPageView> Views
    {
        get { lock (_lock) return _views.ToList(); }
    }

    public Task SaveAsync(IReadOnlyList<TPageView> views, CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            SaveCalls++;
            var failure = FailWith?.Invoke();
            if (failure != null)
                throw failure;
            _views.AddRange(views);
        }
        return Task.CompletedTask;
    }

    public Task<PageViewStats> GetStatsAsync(PageViewStatsQuery query, CancellationToken cancellationToken = default)
    {
        LastStatsQuery = query;
        lock (_lock)
        {
            var scoped = _views.Where(v => v.TimestampUtc >= query.SinceUtc
                && (query.SiteName == null || v.SiteName == query.SiteName)).ToList();
            var counted = query.IncludeBots ? scoped : scoped.Where(v => !v.IsBot).ToList();
            return Task.FromResult(new PageViewStats
            {
                HumanViews = scoped.Count(v => !v.IsBot),
                BotViews = scoped.Count(v => v.IsBot),
                TopPaths = counted.GroupBy(v => v.Path)
                    .Select(g => new KeyCount(g.Key, g.Count()))
                    .OrderByDescending(k => k.Views).Take(query.Top).ToList(),
                Recent = counted.OrderByDescending(v => v.TimestampUtc).Take(query.Top).Cast<IPageView>().ToList()
            });
        }
    }

    public Task<int> PurgeAsync(string siteName, DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        LastPurge = (siteName, cutoffUtc);
        lock (_lock)
            return Task.FromResult(_views.RemoveAll(v => v.SiteName == siteName && v.TimestampUtc < cutoffUtc));
    }
}

/// <summary>Store implementing only the required interface — proves stats/retention are not auto-wired.</summary>
public class SaveOnlyStore : IPageViewStore<PageView>
{
    public Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}