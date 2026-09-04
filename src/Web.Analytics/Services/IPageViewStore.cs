using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Persistence hook — deliberately not shipped by the package. Called from the background writer only,
/// resolved from a fresh scope per batch, so scoped dependencies work.
/// </summary>
public interface IPageViewStore<in TPageView> where TPageView : IPageView
{
    /// <summary>Persists one batch; a thrown exception loses the batch but never the writer loop.</summary>
    Task SaveAsync(IReadOnlyList<TPageView> views, CancellationToken cancellationToken = default);
}

/// <summary>Optional retention hook, called every 24h. Scoped to one site so shared stores purge per host.</summary>
public interface IPageViewRetentionStore
{
    Task<int> PurgeAsync(string siteName, DateTime cutoffUtc, CancellationToken cancellationToken = default);
}

/// <summary>Optional read hook; <c>MapAnalyticsEndpoints</c> only maps when an implementation is registered.</summary>
public interface IPageViewStatsStore
{
    Task<PageViewStats> GetStatsAsync(PageViewStatsQuery query, CancellationToken cancellationToken = default);
}