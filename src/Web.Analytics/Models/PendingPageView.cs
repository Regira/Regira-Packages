using System.Net;

namespace Regira.Web.Analytics.Models;

/// <summary>
/// A page view on its way to the store, carrying the full client IP for enrichers; the writer masks
/// the address before persisting, so the full one exists in memory only.
/// </summary>
public record PendingPageView<TPageView>(TPageView View, IPAddress? ClientIp)
    where TPageView : IPageView;