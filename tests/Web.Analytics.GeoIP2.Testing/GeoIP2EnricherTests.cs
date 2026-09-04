using System.Net;
using Regira.Web.Analytics.GeoIP2.Models;
using Regira.Web.Analytics.GeoIP2.Services;
using Regira.Web.Analytics.Models;

namespace Web.Analytics.GeoIP2.Testing;

[TestFixture]
public class GeoIP2EnricherTests
{
    [Test]
    public async Task KnownAddress_FillsTheGeoColumns()
    {
        var enricher = new GeoIP2Enricher<GeoPageView>(new FixedLocation(new GeoLocation("BE", "Belgium", "Ghent")));
        var pending = new PendingPageView<GeoPageView>(new GeoPageView(), IPAddress.Parse("203.0.113.45"));

        await enricher.EnrichAsync(pending);

        Assert.Multiple(() =>
        {
            Assert.That(pending.View.CountryCode, Is.EqualTo("BE"));
            Assert.That(pending.View.Country, Is.EqualTo("Belgium"));
            Assert.That(pending.View.City, Is.EqualTo("Ghent"));
        });
    }

    [Test]
    public async Task CountryOnlyHit_DoesNotBlankAnExistingCity()
    {
        var enricher = new GeoIP2Enricher<GeoPageView>(new FixedLocation(new GeoLocation("BE", "Belgium", null)));
        var pending = new PendingPageView<GeoPageView>(new GeoPageView { City = "Ghent" }, IPAddress.Parse("203.0.113.45"));

        await enricher.EnrichAsync(pending);

        Assert.Multiple(() =>
        {
            Assert.That(pending.View.Country, Is.EqualTo("Belgium"));
            Assert.That(pending.View.City, Is.EqualTo("Ghent"));
        });
    }

    [Test]
    public async Task UnknownAddress_LeavesTheColumnsUntouched()
    {
        var enricher = new GeoIP2Enricher<GeoPageView>(new FixedLocation(null));
        var pending = new PendingPageView<GeoPageView>(new GeoPageView { Country = "preset" }, IPAddress.Parse("203.0.113.45"));

        await enricher.EnrichAsync(pending);

        Assert.That(pending.View.Country, Is.EqualTo("preset"));
    }

    private class FixedLocation(GeoLocation? location) : IGeoLocationService
    {
        public GeoLocation? Lookup(IPAddress? ip) => location;
    }
}