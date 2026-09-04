using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Regira.Web.Analytics;
using Regira.Web.Analytics.GeoIP2;
using Regira.Web.Analytics.GeoIP2.Config;
using Regira.Web.Analytics.GeoIP2.Models;
using Regira.Web.Analytics.GeoIP2.Services;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;

namespace Web.Analytics.GeoIP2.Testing;

[TestFixture]
public class GeoIP2ExtensionsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] settings)
        => new ConfigurationBuilder().AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value)).Build();

    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new FakeEnvironment());
        return services;
    }

    [Test]
    public void AddGeoIP2_RegistersConfigServiceAndEnricher()
    {
        var services = Services();
        services.AddAnalytics<GeoPageView>(Config(("Analytics:GeoIP2:DatabasePath", "App_Data")))
            .WithStore<NullStore>()
            .AddGeoIP2(Config(("Analytics:GeoIP2:DatabasePath", "App_Data")));

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<GeoIP2Config>().DatabasePath, Is.EqualTo("App_Data"));
            Assert.That(provider.GetRequiredService<IGeoLocationService>(), Is.InstanceOf<GeoLite2LocationService>());
            Assert.That(scope.ServiceProvider.GetServices<IPageViewEnricher<GeoPageView>>().Single(),
                Is.InstanceOf<GeoIP2Enricher<GeoPageView>>());
        });
    }

    [Test]
    public void Disabled_RegistersNothing()
    {
        var services = Services();
        services.AddAnalytics<GeoPageView>(Config(("Analytics:Enabled", "false")))
            .WithStore<NullStore>()
            .AddGeoIP2(Config(("Analytics:GeoIP2:DatabasePath", "App_Data")));

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(GeoIP2Config)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(IGeoLocationService)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewEnricher<GeoPageView>)), Is.False);
        });
    }

    [Test]
    public void RepeatCall_KeepsTheFirstConfiguration_AndOneEnricher()
    {
        var services = Services();
        var builder = services.AddAnalytics<GeoPageView>(Config()).WithStore<NullStore>();
        builder.AddGeoIP2(Config(), geo => geo.DatabasePath = "first");
        builder.AddGeoIP2(Config(), geo => geo.DatabasePath = "second");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Multiple(() =>
        {
            Assert.That(provider.GetRequiredService<GeoIP2Config>().DatabasePath, Is.EqualTo("first"));
            Assert.That(scope.ServiceProvider.GetServices<IPageViewEnricher<GeoPageView>>().Count(), Is.EqualTo(1));
        });
    }

    [Test]
    public void PreRegisteredLookup_IsKept()
    {
        var services = Services();
        services.AddSingleton<IGeoLocationService, FixedLocation>();
        services.AddAnalytics<GeoPageView>(Config()).WithStore<NullStore>().AddGeoIP2(Config());

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IGeoLocationService>(), Is.InstanceOf<FixedLocation>());
    }

    [Test]
    public void Configure_OverridesConfiguration()
    {
        var services = Services();
        services.AddAnalytics<GeoPageView>(Config()).WithStore<NullStore>()
            .AddGeoIP2(Config(("Analytics:GeoIP2:DatabasePath", "from-config")), geo => geo.DatabasePath = "from-code");

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<GeoIP2Config>().DatabasePath, Is.EqualTo("from-code"));
    }

    private class NullStore : IPageViewStore<PageView>
    {
        public Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private class FixedLocation : IGeoLocationService
    {
        public GeoLocation? Lookup(IPAddress? ip) => new("BE", "Belgium", "Ghent");
    }

    private class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "GeoIP2.Testing";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}