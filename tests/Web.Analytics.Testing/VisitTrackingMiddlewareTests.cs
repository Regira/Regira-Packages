using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Middleware;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;
using System.Net;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

[TestFixture]
public class VisitTrackingMiddlewareTests
{
    private const string ChromeUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static PageViewQueue<PageView> CreateQueue(AnalyticsConfig config)
        => new(config, NullLogger<PageViewQueue<PageView>>.Instance);

    private static VisitTrackingMiddleware<PageView> CreateMiddleware(
        RequestDelegate next, PageViewQueue<PageView> queue, AnalyticsConfig config,
        IEnumerable<IVisitContributor<PageView>>? contributors = null,
        ILogger<VisitTrackingMiddleware<PageView>>? logger = null)
    {
        var detector = new BotDetector(new TestOptionsMonitor<BotDetectionConfig>(new BotDetectionConfig()),
            NullLogger<BotDetector>.Instance);
        return new VisitTrackingMiddleware<PageView>(next, queue, detector,
            new HtmlPageVisitFilter(config), contributors ?? [], config,
            logger ?? NullLogger<VisitTrackingMiddleware<PageView>>.Instance);
    }

    private static DefaultHttpContext CreateContext(string path = "/page", string? userAgent = ChromeUa)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = path;
        context.Request.Host = new HostString("example.com");
        context.Request.Headers.Accept = "text/html";
        if (userAgent != null)
            context.Request.Headers.UserAgent = userAgent;
        context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.45");
        return context;
    }

    private static RequestDelegate Respond(int statusCode) => ctx =>
    {
        ctx.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    };

    [Test]
    public async Task Success_IsEnqueued_WithCapturedBasics()
    {
        var config = new AnalyticsConfig { SiteName = "TestSite" };
        var queue = CreateQueue(config);
        var context = CreateContext();
        context.Request.QueryString = new QueryString("?ref=newsletter");
        context.Request.Headers.Referer = "https://google.com/search";

        await CreateMiddleware(Respond(200), queue, config).InvokeAsync(context);

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(pending!.View.SiteName, Is.EqualTo("TestSite"));
            Assert.That(pending.View.Path, Is.EqualTo("/page"));
            Assert.That(pending.View.StatusCode, Is.EqualTo(200));
            Assert.That(pending.View.UtmSource, Is.EqualTo("newsletter"));
            Assert.That(pending.View.ReferrerHost, Is.EqualTo("google.com"));
            Assert.That(pending.View.IsBot, Is.False);
            Assert.That(pending.ClientIp, Is.EqualTo(IPAddress.Parse("203.0.113.45")));
            Assert.That(pending.View.IpAddress, Is.Null, "masking happens in the writer, not here");
        });
    }

    [Test]
    public async Task FailureStatus_IsNotEnqueued()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);

        await CreateMiddleware(Respond(404), queue, config).InvokeAsync(CreateContext());

        Assert.That(queue.Reader.TryRead(out _), Is.False);
    }

    [Test]
    public async Task Path_IsCapturedBeforeDownstreamRewrites()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);
        var middleware = CreateMiddleware(ctx =>
        {
            ctx.Request.Path = "/rewritten";
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        }, queue, config);

        await middleware.InvokeAsync(CreateContext("/original"));

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.That(pending!.View.Path, Is.EqualTo("/original"));
    }

    [Test]
    public async Task SelfReferral_KeepsRawReferrer_ButNullsHost()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);
        var context = CreateContext();
        context.Request.Headers.Referer = "https://example.com/other";

        await CreateMiddleware(Respond(200), queue, config).InvokeAsync(context);

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(pending!.View.Referrer, Is.EqualTo("https://example.com/other"));
            Assert.That(pending.View.ReferrerHost, Is.Null);
        });
    }

    [Test]
    public async Task Bot_IsFlagged_WhenRecordBots()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);

        await CreateMiddleware(Respond(200), queue, config)
            .InvokeAsync(CreateContext(userAgent: "curl/8.5.0 something"));

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.That(pending!.View.IsBot, Is.True);
    }

    [Test]
    public async Task ProbeRequest_IsFlagged_BehindARealBrowserUserAgent()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);

        // Nothing about the agent gives this away; only the target does.
        await CreateMiddleware(Respond(200), queue, config)
            .InvokeAsync(CreateContext(path: "/wp-admin/"));

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.That(pending!.View.IsBot, Is.True);
    }

    [Test]
    public async Task ProbeQueryString_IsFlagged_OnASitesOwnPage()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);
        var context = CreateContext(path: "/");
        context.Request.QueryString = new QueryString("?file=../../../../etc/passwd");

        await CreateMiddleware(Respond(200), queue, config).InvokeAsync(context);

        Assert.That(queue.Reader.TryRead(out var pending), Is.True);
        Assert.That(pending!.View.IsBot, Is.True);
    }

    [Test]
    public async Task Bot_IsDropped_WhenRecordBotsOff()
    {
        var config = new AnalyticsConfig { RecordBots = false };
        var queue = CreateQueue(config);
        var context = CreateContext(userAgent: "curl/8.5.0 something");

        await CreateMiddleware(Respond(200), queue, config).InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(queue.Reader.TryRead(out _), Is.False);
            Assert.That(context.Response.StatusCode, Is.EqualTo(200), "the request itself is untouched");
        });
    }

    [Test]
    public async Task Contributors_RunInOrder_BothPhases()
    {
        var calls = new List<string>();
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);
        var middleware = CreateMiddleware(Respond(200), queue, config,
            [new RecordingContributor("A", calls), new RecordingContributor("B", calls)]);

        await middleware.InvokeAsync(CreateContext());

        Assert.That(calls, Is.EqualTo(new[] { "A.capturing", "B.capturing", "A.captured", "B.captured" }));
    }

    [Test]
    public async Task ThrowingContributor_IsLogged_AndCostsNeitherRequestNorView()
    {
        var config = new AnalyticsConfig();
        var queue = CreateQueue(config);
        var logger = new CapturingLogger<VisitTrackingMiddleware<PageView>>();
        var context = CreateContext();
        var middleware = CreateMiddleware(Respond(200), queue, config, [new ThrowingContributor()], logger);

        await middleware.InvokeAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.Response.StatusCode, Is.EqualTo(200));
            Assert.That(queue.Reader.TryRead(out _), Is.True);
            Assert.That(logger.Entries.Count(e => e.Level == LogLevel.Warning), Is.EqualTo(2),
                "one warning per failing phase");
        });
    }

    private class RecordingContributor(string name, List<string> calls) : IVisitContributor<PageView>
    {
        public void OnCapturing(HttpContext context, PageView view) => calls.Add($"{name}.capturing");

        public ValueTask OnCapturedAsync(HttpContext context, PageView view)
        {
            calls.Add($"{name}.captured");
            return ValueTask.CompletedTask;
        }
    }

    private class ThrowingContributor : IVisitContributor<PageView>
    {
        public void OnCapturing(HttpContext context, PageView view) => throw new InvalidOperationException("boom");
        public ValueTask OnCapturedAsync(HttpContext context, PageView view) => throw new InvalidOperationException("boom");
    }
}