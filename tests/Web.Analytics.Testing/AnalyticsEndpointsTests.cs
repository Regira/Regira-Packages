using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Web.Analytics;
using Regira.Web.Analytics.Endpoints;
using Regira.Web.Analytics.Models;
using System.Text.Json;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

[TestFixture]
public class AnalyticsEndpointsTests
{
    private const string ApiKey = "test-key";

    private static Task<IHost> StartHostAsync(bool withApiKey = true, bool statsCapableStore = true)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Analytics:SiteName"] = "TestSite",
        };
        if (withApiKey)
            settings["Analytics:ApiKey"] = ApiKey;

        return TestHostFactory.StartAsync(settings,
            (ctx, services) =>
            {
                services.AddRouting();
                // Singleton so seeded data is the same instance the request-scoped forwarders resolve.
                services.AddSingleton<InMemoryStore<PageView>>();
                var builder = services.AddAnalytics(ctx.Configuration);
                if (statsCapableStore)
                    builder.WithStore<InMemoryStore<PageView>>();
                else
                    builder.WithStore<SaveOnlyStore>();
            },
            app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapAnalyticsEndpoints());
            });
    }

    private static async Task SeedAsync(IHost host)
    {
        var store = host.Services.GetRequiredService<InMemoryStore<PageView>>();
        var now = DateTime.UtcNow;
        await store.SaveAsync(
        [
            new PageView { TimestampUtc = now, SiteName = "TestSite", Path = "/a" },
            new PageView { TimestampUtc = now, SiteName = "TestSite", Path = "/a" },
            new PageView { TimestampUtc = now, SiteName = "TestSite", Path = "/bot", IsBot = true },
            new PageView { TimestampUtc = now, SiteName = "OtherSite", Path = "/x" },
        ]);
    }

    [Test]
    public async Task WithoutApiKey_TheRouteIsNotMapped()
    {
        using var host = await StartHostAsync(withApiKey: false);
        var response = await host.GetTestClient().GetAsync("/analytics/stats");
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
        await host.StopAsync();
    }

    [Test]
    public async Task WithoutAStatsCapableStore_TheRouteIsNotMapped()
    {
        using var host = await StartHostAsync(statsCapableStore: false);
        var response = await host.GetTestClient().GetAsync("/analytics/stats");
        Assert.That((int)response.StatusCode, Is.EqualTo(404));
        await host.StopAsync();
    }

    [Test]
    public async Task WrongKey_IsUnauthorized()
    {
        using var host = await StartHostAsync();
        var client = host.GetTestClient();

        var noKey = await client.GetAsync("/analytics/stats");
        var request = new HttpRequestMessage(HttpMethod.Get, "/analytics/stats");
        request.Headers.Add("X-Analytics-Key", "wrong");
        var wrongKey = await client.SendAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That((int)noKey.StatusCode, Is.EqualTo(401));
            Assert.That((int)wrongKey.StatusCode, Is.EqualTo(401));
        });
        await host.StopAsync();
    }

    [Test]
    public async Task CorrectKey_ReturnsTheStoresAggregates()
    {
        using var host = await StartHostAsync();
        await SeedAsync(host);

        var request = new HttpRequestMessage(HttpMethod.Get, "/analytics/stats?includeBots=true");
        request.Headers.Add("X-Analytics-Key", ApiKey);
        var response = await host.GetTestClient().SendAsync(request);

        Assert.That((int)response.StatusCode, Is.EqualTo(200));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = json.RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(root.GetProperty("site").GetString(), Is.EqualTo("TestSite"));
            Assert.That(root.GetProperty("stats").GetProperty("humanViews").GetInt32(), Is.EqualTo(2),
                "OtherSite's row must not count");
            Assert.That(root.GetProperty("stats").GetProperty("botViews").GetInt32(), Is.EqualTo(1));
        });
        await host.StopAsync();
    }

    [Test]
    public async Task Days_IsClamped()
    {
        using var host = await StartHostAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/analytics/stats?days=9999");
        request.Headers.Add("X-Analytics-Key", ApiKey);
        var response = await host.GetTestClient().SendAsync(request);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(json.RootElement.GetProperty("days").GetInt32(), Is.EqualTo(730));
        await host.StopAsync();
    }

    [Test]
    public async Task Recent_SerializesCustomEntityColumns()
    {
        using var host = await TestHostFactory.StartAsync(
            new Dictionary<string, string?>
            {
                ["Analytics:SiteName"] = "TestSite",
                ["Analytics:ApiKey"] = ApiKey,
            },
            (ctx, services) =>
            {
                services.AddRouting();
                services.AddSingleton<InMemoryStore<TestGeoPageView>>();
                services.AddAnalytics<TestGeoPageView>(ctx.Configuration).WithStore<InMemoryStore<TestGeoPageView>>();
            },
            app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapAnalyticsEndpoints());
            });

        var store = host.Services.GetRequiredService<InMemoryStore<TestGeoPageView>>();
        await store.SaveAsync([new TestGeoPageView
        {
            TimestampUtc = DateTime.UtcNow, SiteName = "TestSite", Path = "/x", CountryCode = "BE"
        }]);

        var request = new HttpRequestMessage(HttpMethod.Get, "/analytics/stats");
        request.Headers.Add("X-Analytics-Key", ApiKey);
        var body = await (await host.GetTestClient().SendAsync(request)).Content.ReadAsStringAsync();

        Assert.That(body, Does.Contain("\"countryCode\":\"BE\""),
            "recent rows must serialize as their runtime type, not cut down to IPageView");
        await host.StopAsync();
    }

    [Test]
    public async Task SiteWildcard_AsksTheStoreForAllSites()
    {
        using var host = await StartHostAsync();
        var request = new HttpRequestMessage(HttpMethod.Get, "/analytics/stats?site=*");
        request.Headers.Add("X-Analytics-Key", ApiKey);
        await host.GetTestClient().SendAsync(request);

        var store = host.Services.GetRequiredService<InMemoryStore<PageView>>();
        Assert.Multiple(() =>
        {
            Assert.That(store.LastStatsQuery, Is.Not.Null);
            Assert.That(store.LastStatsQuery!.SiteName, Is.Null, "null spans every site");
        });
        await host.StopAsync();
    }
}