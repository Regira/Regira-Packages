using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Web.Analytics;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

/// <summary>
/// End-to-end over TestServer: the full middleware → queue → writer → store pipeline with a custom
/// entity, mirroring the docs' geolocation sample.
/// </summary>
[TestFixture]
public class CustomPageViewTests
{
    private static readonly Dictionary<string, string?> Settings = new()
    {
        ["Analytics:SiteName"] = "TestSite",
        ["Analytics:FlushIntervalSeconds"] = "1",
        ["Analytics:RetentionDays"] = "0",
    };

    [Test]
    public async Task CustomEntity_FlowsThroughMiddlewareContributorEnricherAndStore()
    {
        using var host = await TestHostFactory.StartAsync(Settings,
            (ctx, services) =>
            {
                // Singleton so the test can observe the same instance the scoped forwarders resolve.
                services.AddSingleton<InMemoryStore<TestGeoPageView>>();
                services.AddAnalytics<TestGeoPageView>(ctx.Configuration)
                    .WithStore<InMemoryStore<TestGeoPageView>>()
                    .AddContributor<MarkingContributor>()
                    .AddEnricher<GeoStampEnricher>();
            },
            app =>
            {
                app.UseAnalytics();
                app.Run(async ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    await ctx.Response.WriteAsync("ok");
                });
            });

        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/some-page");
        request.Headers.Accept.ParseAdd("text/html");
        request.Headers.TryAddWithoutValidation("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36");
        var response = await client.SendAsync(request);
        Assert.That((int)response.StatusCode, Is.EqualTo(200));

        var store = host.Services.GetRequiredService<InMemoryStore<TestGeoPageView>>();
        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);

        var view = store.Views[0];
        Assert.Multiple(() =>
        {
            Assert.That(view, Is.InstanceOf<TestGeoPageView>(), "middleware must create the registered subclass");
            Assert.That(view.SiteName, Is.EqualTo("TestSite"));
            Assert.That(view.Path, Is.EqualTo("/some-page"));
            Assert.That(view.CapturedBy, Is.EqualTo(nameof(MarkingContributor)), "contributor gets the typed view");
            Assert.That(view.CountryCode, Is.EqualTo("BE"), "enricher gets the typed view");
        });

        await host.StopAsync();
    }

    [Test]
    public async Task WithoutAStore_NoMiddlewareIsAdded_AndNothingIsQueued()
    {
        using var host = await TestHostFactory.StartAsync(Settings,
            (ctx, services) => services.AddAnalytics(ctx.Configuration),   // no WithStore on purpose
            app =>
            {
                app.UseAnalytics();
                app.Run(ctx =>
                {
                    ctx.Response.StatusCode = 200;
                    return Task.CompletedTask;
                });
            });

        var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/page");
        request.Headers.Accept.ParseAdd("text/html");
        var response = await client.SendAsync(request);
        Assert.That((int)response.StatusCode, Is.EqualTo(200), "the site itself is unaffected");

        var queue = host.Services.GetRequiredService<PageViewQueue<PageView>>();
        Assert.That(queue.Reader.TryRead(out _), Is.False);

        await host.StopAsync();
    }

    [Test]
    public void SecondAddAnalytics_WithADifferentEntity_Throws()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddAnalytics(configuration);

        Assert.Throws<InvalidOperationException>(() => services.AddAnalytics<TestGeoPageView>(configuration));
    }

    [Test]
    public void SecondAddAnalytics_WithTheSameEntity_IsHarmless()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();
        services.AddAnalytics(configuration);

        Assert.DoesNotThrow(() => services.AddAnalytics(configuration));
        Assert.That(services.Count(d => d.ServiceType == typeof(PageViewQueue<PageView>)), Is.EqualTo(1));
    }
}