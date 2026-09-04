using Regira.Web.Analytics.GeoIP2.Models;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Regira.Web.Analytics.GeoIP2.Services;

/// <summary>Fills the <see cref="IGeoPageView"/> columns from the unmasked client IP, before masking.</summary>
public class GeoIP2Enricher<TPageView>(IGeoLocationService geo) : IPageViewEnricher<TPageView>
    where TPageView : IPageView, IGeoPageView
{
    public ValueTask EnrichAsync(PendingPageView<TPageView> pending, CancellationToken cancellationToken = default)
    {
        var location = geo.Lookup(pending.ClientIp);
        if (location != null)
        {
            pending.View.CountryCode = location.CountryCode;
            pending.View.Country = location.Country;
            // A Country database must not blank a city another enricher set.
            if (location.City != null)
                pending.View.City = location.City;
        }
        return ValueTask.CompletedTask;
    }
}