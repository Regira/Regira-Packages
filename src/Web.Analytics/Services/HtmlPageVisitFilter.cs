using Microsoft.AspNetCore.Http;
using Regira.Web.Analytics.Config;

namespace Regira.Web.Analytics.Services;

/// <summary>Default filter: browser page loads only. Non-HTML hosts register their own <see cref="IVisitFilter"/>.</summary>
public class HtmlPageVisitFilter(AnalyticsConfig config) : IVisitFilter
{
    /// <summary>Prefixes no site serves as a page; host-specific exclusions go in <see cref="AnalyticsConfig.IgnorePaths"/>.</summary>
    private static readonly string[] IgnoredPrefixes =
    [
        "/favicon", "/.well-known", "/robots.txt", "/sitemap", "/analytics"
    ];

    public bool ShouldTrack(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method))
            return false;

        var path = request.Path.Value;
        if (string.IsNullOrEmpty(path))
            return false;

        // Only document requests: fetch/XHR and asset requests don't ask for text/html.
        if (!request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var prefix in IgnoredPrefixes)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        foreach (var prefix in config.IgnorePaths)
        {
            if (!string.IsNullOrWhiteSpace(prefix) && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // A dot in the last segment means a file, not a route.
        var lastSlash = path.LastIndexOf('/');
        return !path.AsSpan(lastSlash + 1).Contains('.');
    }
}