using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Web.Analytics.GeoIP2.Config;
using Regira.Web.Analytics.GeoIP2.Models;
using Regira.Web.Analytics.GeoIP2.Services;
using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.GeoIP2;

public static class GeoIP2Extensions
{
    public const string SectionName = "Analytics:GeoIP2";

    /// <summary>
    /// Adds the MaxMind geo enricher for an entity implementing <see cref="IGeoPageView"/>. Bound from
    /// Analytics:GeoIP2; a pre-registered <see cref="IGeoLocationService"/> is kept, so the lookup is
    /// swappable. No-op when analytics is disabled; a repeat call keeps the first configuration.
    /// </summary>
    public static AnalyticsBuilder<TPageView> AddGeoIP2<TPageView>(this AnalyticsBuilder<TPageView> builder,
        IConfiguration configuration, Action<GeoIP2Config>? configure = null)
        where TPageView : class, IPageView, IGeoPageView, new()
    {
        if (!builder.Enabled)
            return builder;

        var config = configuration.GetSection(SectionName).Get<GeoIP2Config>() ?? new GeoIP2Config();
        configure?.Invoke(config);

        builder.Services.TryAddSingleton(config);
        builder.Services.TryAddSingleton<IGeoLocationService, GeoLite2LocationService>();
        return builder.AddEnricher<GeoIP2Enricher<TPageView>>();
    }
}