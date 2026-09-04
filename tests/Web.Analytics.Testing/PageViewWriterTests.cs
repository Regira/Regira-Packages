using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Models;
using Regira.Web.Analytics.Services;
using System.Net;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

[TestFixture]
public class PageViewWriterTests
{
    private static PendingPageView<PageView> Pending(string path = "/page", string ip = "203.0.113.45")
        => new(new PageView { TimestampUtc = DateTime.UtcNow, SiteName = "TestSite", Path = path },
            IPAddress.Parse(ip));

    private static (PageViewWriter<PageView> Writer, PageViewQueue<PageView> Queue) Create(
        AnalyticsConfig config, Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        var queue = new PageViewQueue<PageView>(config, NullLogger<PageViewQueue<PageView>>.Instance);
        var writer = new PageViewWriter<PageView>(queue, config,
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<PageViewWriter<PageView>>.Instance);
        return (writer, queue);
    }

    [Test]
    public async Task Batch_IsSavedAsOneCall_WithMaskedIps()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending("/a"));
        queue.Enqueue(Pending("/b"));
        queue.Enqueue(Pending("/c"));

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 3);
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.SaveCalls, Is.EqualTo(1));
            Assert.That(store.Views.Select(v => v.IpAddress), Is.All.EqualTo("203.0.113.0"));
        });
    }

    [Test]
    public async Task Backlog_IsDrainedInOneWake_NotOneBatchPerInterval()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, BatchSize = 2, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        for (var i = 0; i < 5; i++)
            queue.Enqueue(Pending($"/{i}"));

        // One-batch-per-interval would need ~3 intervals; draining in one wake fits well inside two.
        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 5, timeoutMs: 2500);
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.SaveCalls, Is.EqualTo(3));
    }

    [Test]
    public async Task ConfiguredPrefixLength_IsApplied()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, Ipv4PrefixLength = 16, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views[0].IpAddress, Is.EqualTo("203.0.0.0"));
    }

    [Test]
    public async Task OutOfRangePrefixLength_MasksToTheDefault_AndWarns()
    {
        var store = new InMemoryStore<PageView>();
        // The obvious slip: the IPv6 value in the IPv4 slot. Must not fail open to full addresses.
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, Ipv4PrefixLength = 48, RetentionDays = 0 };
        var services = new ServiceCollection();
        services.AddSingleton<IPageViewStore<PageView>>(store);
        var provider = services.BuildServiceProvider();
        var queue = new PageViewQueue<PageView>(config, NullLogger<PageViewQueue<PageView>>.Instance);
        var logger = new CapturingLogger<PageViewWriter<PageView>>();
        var writer = new PageViewWriter<PageView>(queue, config, provider.GetRequiredService<IServiceScopeFactory>(), logger);

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.Views[0].IpAddress, Is.EqualTo("203.0.113.0"));
            Assert.That(logger.Entries.Any(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning
                && e.Message.Contains("out of range")), Is.True);
        });
    }

    [Test]
    public async Task MaskingOff_StoresTheFullNormalizedAddress()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, MaskIpAddress = false, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views[0].IpAddress, Is.EqualTo("203.0.113.45"));
    }

    [Test]
    public async Task Enricher_SeesTheUnmaskedIp_BeforeMasking()
    {
        var store = new InMemoryStore<PageView>();
        var enricher = new SpyEnricher();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(store);
            s.AddSingleton<IPageViewEnricher<PageView>>(enricher);
        });

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(enricher.SeenClientIp, Is.EqualTo("203.0.113.45"), "full address at enrich time");
            Assert.That(enricher.IpAddressAtEnrichTime, Is.Null, "not yet masked at enrich time");
            Assert.That(store.Views[0].IpAddress, Is.EqualTo("203.0.113.0"), "masked when stored");
        });
    }

    [Test]
    public async Task ThrowingEnricher_DoesNotCostTheBatch()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(store);
            s.AddSingleton<IPageViewEnricher<PageView>>(new ThrowingEnricher());
        });

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);
        await writer.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task StoreFailure_LosesTheBatch_ButNotTheLoop()
    {
        var store = new InMemoryStore<PageView> { FailWith = () => new IOException("disk full") };
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending());

        await TestHostFactory.WaitUntilAsync(() => store.SaveCalls == 1);
        Assert.Multiple(() =>
        {
            Assert.That(store.Views, Is.Empty);
            Assert.That(writer.ExecuteTask!.IsCompleted, Is.False, "the loop survives, backing off");
        });

        // The loop is inside its 30s backoff (deliberately non-cancellable); don't wait it out.
        using var cts = new CancellationTokenSource(500);
        await writer.StopAsync(cts.Token);
    }

    [Test]
    public async Task Shutdown_FlushesWhatIsStillQueued()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        // BackgroundService.StartAsync schedules ExecuteAsync rather than running it inline; one saved
        // prime item proves the loop is live before StopAsync races it.
        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending("/prime"));
        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);

        queue.Enqueue(Pending("/a"));
        queue.Enqueue(Pending("/b"));
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task Shutdown_FlushesMoreThanOneBatch()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, BatchSize = 2, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending("/prime"));
        await TestHostFactory.WaitUntilAsync(() => store.Views.Count == 1);

        for (var i = 0; i < 5; i++)
            queue.Enqueue(Pending($"/{i}"));
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views, Has.Count.EqualTo(6), "a shutdown backlog larger than one batch must not be dropped");
    }

    [Test]
    public async Task Purge_RunsOnStart_WhenRetentionStoreAndDaysArePresent()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { SiteName = "TestSite", FlushIntervalSeconds = 1, RetentionDays = 30 };
        var (writer, _) = Create(config, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(store);
            s.AddSingleton<IPageViewRetentionStore>(store);
        });

        await writer.StartAsync(CancellationToken.None);
        await TestHostFactory.WaitUntilAsync(() => store.LastPurge != null);
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.LastPurge, Is.Not.Null);
            var lastPurge = store.LastPurge!.Value;
            Assert.That(lastPurge.SiteName, Is.EqualTo("TestSite"));
            Assert.That(lastPurge.CutoffUtc,
                Is.EqualTo(DateTime.UtcNow.AddDays(-30)).Within(TimeSpan.FromMinutes(5)));
        });
    }

    [Test]
    public async Task Purge_IsSkipped_WithoutRetentionStore_OrWithoutRetentionDays()
    {
        var withoutStore = new InMemoryStore<PageView>();
        var configWithDays = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 30 };
        var (writer1, _) = Create(configWithDays, s => s.AddSingleton<IPageViewStore<PageView>>(withoutStore));

        var withoutDays = new InMemoryStore<PageView>();
        var configWithoutDays = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer2, _) = Create(configWithoutDays, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(withoutDays);
            s.AddSingleton<IPageViewRetentionStore>(withoutDays);
        });

        await writer1.StartAsync(CancellationToken.None);
        await writer2.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await writer1.StopAsync(CancellationToken.None);
        await writer2.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(withoutStore.LastPurge, Is.Null);
            Assert.That(withoutDays.LastPurge, Is.Null);
        });
    }

    [Test]
    public async Task NoStoreRegistered_WriterDeactivatesItself()
    {
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1 };
        var (writer, _) = Create(config, _ => { });

        await writer.StartAsync(CancellationToken.None);
        await TestHostFactory.WaitUntilAsync(() => writer.ExecuteTask!.IsCompleted);
        await writer.StopAsync(CancellationToken.None);
    }

    private class SpyEnricher : IPageViewEnricher<PageView>
    {
        public string? SeenClientIp { get; private set; }
        public string? IpAddressAtEnrichTime { get; private set; }

        public ValueTask EnrichAsync(PendingPageView<PageView> pending, CancellationToken cancellationToken = default)
        {
            SeenClientIp = pending.ClientIp?.ToString();
            IpAddressAtEnrichTime = pending.View.IpAddress;
            return ValueTask.CompletedTask;
        }
    }

    private class ThrowingEnricher : IPageViewEnricher<PageView>
    {
        public ValueTask EnrichAsync(PendingPageView<PageView> pending, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("boom");
    }
}