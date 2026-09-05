using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Regira.Web.Analytics.Middleware;

/// <summary>
/// Records visits. Must be registered before the static-file middleware, which answers requests this
/// middleware would otherwise never see.
/// </summary>
public class VisitTrackingMiddleware<TPageView>(RequestDelegate next, PageViewQueue<TPageView> queue, BotDetector botDetector,
    IVisitFilter filter, IEnumerable<IVisitContributor<TPageView>> contributors, AnalyticsConfig config, ILogger<VisitTrackingMiddleware<TPageView>> logger)
    where TPageView : class, IPageView, new()
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Capture before the request runs: routing can rewrite Request.Path.
        var pending = filter.ShouldTrack(context.Request) ? Capture(context) : null;

        if (pending != null)
        {
            foreach (var contributor in contributors)
            {
                // A broken contributor must not break the request, nor cost the page view.
                try
                {
                    contributor.OnCapturing(context, pending.View);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Analytics: contributor {Contributor} failed while capturing",
                        contributor.GetType().Name);
                }
            }
        }

        await next(context);

        if (pending == null)
            return;

        if (!filter.ShouldRecord(context))
            return;

        pending.View.StatusCode = context.Response.StatusCode;

        foreach (var contributor in contributors)
        {
            try
            {
                await contributor.OnCapturedAsync(context, pending.View);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Analytics: contributor {Contributor} failed after capture",
                    contributor.GetType().Name);
            }
        }

        queue.Enqueue(pending);
    }

    private PendingPageView<TPageView>? Capture(HttpContext context)
    {
        var request = context.Request;

        var userAgent = Truncate(request.Headers.UserAgent.ToString(), 512);
        var path = Truncate(request.Path.Value, 256) ?? "/";
        var queryString = Truncate(request.QueryString.Value, 512);

        // Two independent signals: what the client claims to be, and what it asked for.
        var isBot = botDetector.IsBot(userAgent) || botDetector.IsProbe(path, queryString);
        if (isBot && !config.RecordBots)
            return null;

        var referrer = Truncate(request.Headers.Referer.ToString(), 512);

        var view = new TPageView
        {
            TimestampUtc = DateTime.UtcNow,
            SiteName = config.SiteName,
            Path = path,
            QueryString = queryString,
            Referrer = referrer,
            ReferrerHost = ExternalReferrerHost(referrer, request.Host.Host),
            UtmSource = Truncate(
                request.Query["utm_source"].FirstOrDefault()
                ?? request.Query["ref"].FirstOrDefault()
                ?? request.Query["source"].FirstOrDefault(), 128),
            UserAgent = userAgent,
            IsBot = isBot
        };

        return new PendingPageView<TPageView>(view, IpMasker.Normalize(context.Connection.RemoteIpAddress));
    }

    /// <summary>Host of the referrer, or null when missing or a self-referral.</summary>
    private static string? ExternalReferrerHost(string? referrer, string requestHost)
    {
        if (string.IsNullOrEmpty(referrer) || !Uri.TryCreate(referrer, UriKind.Absolute, out var uri))
            return null;

        return string.Equals(uri.Host, requestHost, StringComparison.OrdinalIgnoreCase)
            ? null
            : Truncate(uri.Host, 256);
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Length <= maxLength ? value : value[..maxLength];
    }
}