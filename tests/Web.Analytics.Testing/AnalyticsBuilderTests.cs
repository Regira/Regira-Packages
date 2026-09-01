using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Web.Analytics;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

[TestFixture]
public class AnalyticsBuilderTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] settings)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

    [Test]
    public void WithStore_WiresStatsAndRetention_WhenTheStoreImplementsThem()
    {
        var services = new ServiceCollection();
        services.AddAnalytics(Config()).WithStore<InMemoryStore<PageView>>();

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewStore<PageView>)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewStatsStore)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewRetentionStore)), Is.True);
        });

        // All three must resolve to the same instance, or stats would read a store nothing writes to.
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageViewStore<PageView>>();
        Assert.Multiple(() =>
        {
            Assert.That(scope.ServiceProvider.GetRequiredService<IPageViewStatsStore>(), Is.SameAs(store));
            Assert.That(scope.ServiceProvider.GetRequiredService<IPageViewRetentionStore>(), Is.SameAs(store));
        });
    }

    [Test]
    public void ScopedDependencyStore_SurvivesScopeValidation_AndResolvesPerScope()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<ScopedDep>();
        services.AddAnalytics(Config()).WithStore<ScopedDependentStore>();

        // WebApplicationBuilder enables both flags in Development — the DbContext-backed store from the
        // docs must survive builder.Build() there.
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IPageViewStore<PageView>>();

        Assert.DoesNotThrowAsync(() => store.SaveAsync([new PageView { SiteName = "s", Path = "/" }]));
    }

    [Test]
    public void PreRegisteredSingletonStore_KeepsItsLifetime()
    {
        var services = new ServiceCollection();
        services.AddSingleton<InMemoryStore<PageView>>();
        services.AddAnalytics(Config()).WithStore<InMemoryStore<PageView>>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var singleton = provider.GetRequiredService<InMemoryStore<PageView>>();
        using var scope = provider.CreateScope();

        Assert.That(scope.ServiceProvider.GetRequiredService<IPageViewStore<PageView>>(), Is.SameAs(singleton));
    }

    [Test]
    public void WithStore_DoesNotWireInterfacesTheStoreLacks()
    {
        var services = new ServiceCollection();
        services.AddAnalytics(Config()).WithStore<SaveOnlyStore>();

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewStore<PageView>)), Is.True);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewStatsStore)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewRetentionStore)), Is.False);
        });
    }

    [Test]
    public void WithFilter_ReplacesTheDefault()
    {
        var services = new ServiceCollection();
        services.AddAnalytics(Config()).WithFilter<AllRequestsFilter>();

        using var provider = services.BuildServiceProvider();
        Assert.That(provider.GetRequiredService<IVisitFilter>(), Is.InstanceOf<AllRequestsFilter>());
    }

    [Test]
    public void Disabled_RegistersNothingButConfig_AndBuilderMethodsNoOp()
    {
        var services = new ServiceCollection();
        services.AddAnalytics(Config(("Analytics:Enabled", "false")))
            .WithStore<InMemoryStore<PageView>>()
            .WithFilter<AllRequestsFilter>();

        Assert.Multiple(() =>
        {
            Assert.That(services.Any(d => d.ServiceType == typeof(PageViewQueue<PageView>)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(IPageViewStore<PageView>)), Is.False);
            Assert.That(services.Any(d => d.ServiceType == typeof(IVisitFilter)), Is.False);
        });
    }

    [Test]
    public void SiteName_FallsBackToEntryAssembly_AndIsTruncated()
    {
        var services = new ServiceCollection();
        services.AddAnalytics(Config());
        using var fallbackProvider = services.BuildServiceProvider();
        Assert.That(fallbackProvider.GetRequiredService<Regira.Web.Analytics.Config.AnalyticsConfig>().SiteName,
            Is.Not.Empty);

        var longName = new string('x', 100);
        var services2 = new ServiceCollection();
        services2.AddAnalytics(Config(("Analytics:SiteName", longName)));
        using var truncatedProvider = services2.BuildServiceProvider();
        Assert.That(truncatedProvider.GetRequiredService<Regira.Web.Analytics.Config.AnalyticsConfig>().SiteName,
            Has.Length.EqualTo(64));
    }

    private class AllRequestsFilter : IVisitFilter
    {
        public bool ShouldTrack(HttpRequest request) => true;
    }

    private class ScopedDep;

    private class ScopedDependentStore(ScopedDep dep) : IPageViewStore<PageView>
    {
        public Task SaveAsync(IReadOnlyList<PageView> views, CancellationToken cancellationToken = default)
        {
            _ = dep;
            return Task.CompletedTask;
        }
    }
}