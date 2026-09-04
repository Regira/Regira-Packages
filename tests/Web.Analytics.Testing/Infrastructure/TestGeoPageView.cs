using Microsoft.AspNetCore.Http;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Web.Analytics.Testing.Infrastructure;

/// <summary>The docs' canonical customization sample, in test form: geo columns on a subclass.</summary>
public class TestGeoPageView : PageView
{
    public string? CountryCode { get; set; }
    public string? CapturedBy { get; set; }
}

/// <summary>Fills a custom property in-request, proving contributors receive the typed view.</summary>
public class MarkingContributor : IVisitContributor<TestGeoPageView>
{
    public ValueTask OnCapturedAsync(HttpContext context, TestGeoPageView view)
    {
        view.CapturedBy = nameof(MarkingContributor);
        return ValueTask.CompletedTask;
    }
}

/// <summary>Stamps geo data in the background, the way a real IP-based lookup would.</summary>
public class GeoStampEnricher : IPageViewEnricher<TestGeoPageView>
{
    public ValueTask EnrichAsync(PendingPageView<TestGeoPageView> pending, CancellationToken cancellationToken = default)
    {
        pending.View.CountryCode = "BE";
        return ValueTask.CompletedTask;
    }
}