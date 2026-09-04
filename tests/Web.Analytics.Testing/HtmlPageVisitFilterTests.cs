using Microsoft.AspNetCore.Http;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Services;

namespace Web.Analytics.Testing;

[TestFixture]
public class HtmlPageVisitFilterTests
{
    private static HttpRequest Request(string method = "GET", string path = "/page", string? accept = "text/html")
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        if (accept != null)
            context.Request.Headers.Accept = accept;
        return context.Request;
    }

    private static IVisitFilter Create(AnalyticsConfig? config = null)
        => new HtmlPageVisitFilter(config ?? new AnalyticsConfig());

    [Test]
    public void Tracks_HtmlGet()
        => Assert.That(Create().ShouldTrack(Request()), Is.True);

    [Test]
    public void Rejects_Post()
        => Assert.That(Create().ShouldTrack(Request(method: "POST")), Is.False);

    [Test]
    public void Rejects_WithoutHtmlAccept()
        => Assert.That(Create().ShouldTrack(Request(accept: "application/json")), Is.False);

    [TestCase("/favicon.ico")]
    [TestCase("/.well-known/security.txt")]
    [TestCase("/robots.txt")]
    [TestCase("/sitemap.xml")]
    [TestCase("/analytics/stats")]
    public void Rejects_BuiltInPrefixes(string path)
        => Assert.That(Create().ShouldTrack(Request(path: path)), Is.False);

    [Test]
    public void Rejects_ConfiguredIgnorePaths()
    {
        var filter = Create(new AnalyticsConfig { IgnorePaths = ["/api"] });
        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldTrack(Request(path: "/api/items")), Is.False);
            Assert.That(filter.ShouldTrack(Request(path: "/apidocs")), Is.False, "prefix match, not segment match");
            Assert.That(filter.ShouldTrack(Request(path: "/blog")), Is.True);
        });
    }

    [Test]
    public void Rejects_FileLikeLastSegment()
    {
        var filter = Create();
        Assert.Multiple(() =>
        {
            Assert.That(filter.ShouldTrack(Request(path: "/assets/site.css")), Is.False);
            Assert.That(filter.ShouldTrack(Request(path: "/v1.2/page")), Is.True, "a dot in an earlier segment is fine");
        });
    }

    [TestCase(200, true)]
    [TestCase(304, true)]
    [TestCase(302, false)]
    [TestCase(404, false)]
    [TestCase(500, false)]
    public void ShouldRecord_DefaultsTo200And304(int statusCode, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Response.StatusCode = statusCode;
        Assert.That(Create().ShouldRecord(context), Is.EqualTo(expected));
    }
}