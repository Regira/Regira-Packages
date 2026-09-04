using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Background hook completing a page view before it is stored — receives the unmasked client IP (the
/// geolocation seam). Runs off the request thread; no HttpContext. Exceptions are logged and swallowed.
/// </summary>
public interface IPageViewEnricher<TPageView> where TPageView : IPageView
{
    ValueTask EnrichAsync(PendingPageView<TPageView> pending, CancellationToken cancellationToken = default);
}