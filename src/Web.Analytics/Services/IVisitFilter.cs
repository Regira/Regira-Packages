using Microsoft.AspNetCore.Http;

namespace Regira.Web.Analytics.Services;

/// <summary>Decides which requests become page views; replace via <c>AnalyticsBuilder.WithFilter</c>.</summary>
public interface IVisitFilter
{
    /// <summary>Evaluated before the request runs, on the path as the visitor asked for it (pre-rewrite).</summary>
    bool ShouldTrack(HttpRequest request);

    /// <summary>Evaluated after the response; redirects are excluded by default to avoid double counting.</summary>
    bool ShouldRecord(HttpContext context) => context.Response.StatusCode is 200 or 304;
}