using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        var (writer, queue, _) = Build(config, register);
        return (writer, queue);
    }

    /// <summary>
    /// Starts a writer and waits until its loop runs, so that stopping it flushes what is queued: a stop that comes before
    /// the loop starts skips it altogether. The loop's first act is to open a scope.
    /// </summary>
    private static async Task<(PageViewWriter<PageView> Writer, PageViewQueue<PageView> Queue)> StartLoop(
        AnalyticsConfig config, Action<IServiceCollection> register, ILogger<PageViewWriter<PageView>>? logger = null)
    {
        var (writer, queue, scopes) = Build(config, register, logger);
        await writer.StartAsync(CancellationToken.None);
        await scopes.FirstScope.WaitAsync(TimeSpan.FromSeconds(10));
        return (writer, queue);
    }

    private static (PageViewWriter<PageView> Writer, PageViewQueue<PageView> Queue, SignallingScopeFactory Scopes) Build(
        AnalyticsConfig config, Action<IServiceCollection> register, ILogger<PageViewWriter<PageView>>? logger = null)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        var scopes = new SignallingScopeFactory(provider.GetRequiredService<IServiceScopeFactory>());
        var queue = new PageViewQueue<PageView>(config, NullLogger<PageViewQueue<PageView>>.Instance);
        var writer = new PageViewWriter<PageView>(queue, config, scopes, logger ?? NullLogger<PageViewWriter<PageView>>.Instance);
        return (writer, queue, scopes);
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
        var (writer, queue) = await StartLoop(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));
        queue.Enqueue(Pending());
        // the shutdown flush writes what is queued, through the same enrich-and-mask step as the timed flush
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views.Select(v => v.IpAddress), Is.EqualTo(new[] { "203.0.0.0" }));
    }

    [Test]
    public async Task OutOfRangePrefixLength_MasksToTheDefault_AndWarns()
    {
        var store = new InMemoryStore<PageView>();
        // The obvious slip: the IPv6 value in the IPv4 slot. Must not fail open to full addresses.
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, Ipv4PrefixLength = 48, RetentionDays = 0 };
        var logger = new CapturingLogger<PageViewWriter<PageView>>();
        var (writer, queue) = await StartLoop(config, s => s.AddSingleton<IPageViewStore<PageView>>(store), logger);
        queue.Enqueue(Pending());
        // the shutdown flush writes what is queued, through the same enrich-and-mask step as the timed flush
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(store.Views.Select(v => v.IpAddress), Is.EqualTo(new[] { "203.0.113.0" }));
            Assert.That(logger.Entries.Any(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning
                && e.Message.Contains("out of range")), Is.True);
        });
    }

    [Test]
    public async Task MaskingOff_StoresTheFullNormalizedAddress()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, MaskIpAddress = false, RetentionDays = 0 };
        var (writer, queue) = await StartLoop(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));
        queue.Enqueue(Pending());
        // the shutdown flush writes what is queued, through the same enrich-and-mask step as the timed flush
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views.Select(v => v.IpAddress), Is.EqualTo(new[] { "203.0.113.45" }));
    }

    [Test]
    public async Task Enricher_SeesTheUnmaskedIp_BeforeMasking()
    {
        var store = new InMemoryStore<PageView>();
        var enricher = new SpyEnricher();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = await StartLoop(config, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(store);
            s.AddSingleton<IPageViewEnricher<PageView>>(enricher);
        });
        queue.Enqueue(Pending());
        // the shutdown flush writes what is queued, through the same enrich-and-mask step as the timed flush
        await writer.StopAsync(CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(enricher.SeenClientIp, Is.EqualTo("203.0.113.45"), "full address at enrich time");
            Assert.That(enricher.IpAddressAtEnrichTime, Is.Null, "not yet masked at enrich time");
            Assert.That(store.Views.Select(v => v.IpAddress), Is.EqualTo(new[] { "203.0.113.0" }), "masked when stored");
        });
    }

    [Test]
    public async Task ThrowingEnricher_DoesNotCostTheBatch()
    {
        var store = new InMemoryStore<PageView>();
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = await StartLoop(config, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(store);
            s.AddSingleton<IPageViewEnricher<PageView>>(new ThrowingEnricher());
        });
        queue.Enqueue(Pending());
        // the shutdown flush writes what is queued, through the same enrich-and-mask step as the timed flush
        await writer.StopAsync(CancellationToken.None);

        Assert.That(store.Views, Has.Count.EqualTo(1));
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

        await writer.StopAsync(CancellationToken.None);
    }

    [Test]
    public async Task Shutdown_During_The_Backoff_Flushes_Without_Waiting_It_Out()
    {
        var failing = true;
        var store = new InMemoryStore<PageView> { FailWith = () => failing ? new IOException("disk full") : null };
        var config = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer, queue) = Create(config, s => s.AddSingleton<IPageViewStore<PageView>>(store));

        await writer.StartAsync(CancellationToken.None);
        queue.Enqueue(Pending("/lost"));
        await TestHostFactory.WaitUntilAsync(() => store.SaveCalls == 1);
        failing = false;
        queue.Enqueue(Pending("/queued"));

        // the loop is inside its 30s back-off
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await writer.StopAsync(cts.Token);

        Assert.That(writer.ExecuteTask!.IsCompleted, Is.True, "the back-off ends with the host");
        Assert.That(store.Views.Select(v => v.Path), Is.EqualTo(new[] { "/queued" }));
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

        var lastPurge = store.LastPurge;
        Assert.That(lastPurge, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(lastPurge!.Value.SiteName, Is.EqualTo("TestSite"));
            Assert.That(lastPurge.Value.CutoffUtc, Is.EqualTo(DateTime.UtcNow.AddDays(-30)).Within(TimeSpan.FromMinutes(5)));
        });
    }

    [Test]
    public async Task Purge_IsSkipped_WithoutRetentionStore_OrWithoutRetentionDays()
    {
        var withoutStore = new InMemoryStore<PageView>();
        var configWithDays = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 30 };
        var (writer1, _) = await StartLoop(configWithDays, s => s.AddSingleton<IPageViewStore<PageView>>(withoutStore));

        var withoutDays = new InMemoryStore<PageView>();
        var configWithoutDays = new AnalyticsConfig { FlushIntervalSeconds = 1, RetentionDays = 0 };
        var (writer2, _) = await StartLoop(configWithoutDays, s =>
        {
            s.AddSingleton<IPageViewStore<PageView>>(withoutDays);
            s.AddSingleton<IPageViewRetentionStore>(withoutDays);
        });

        // with the loops running, a purge would have run first thing, before either loop waits: stopping waits for it
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

    /// <summary>Opens scopes as the inner factory does, and completes <see cref="FirstScope"/> on the first.</summary>
    private sealed class SignallingScopeFactory(IServiceScopeFactory inner) : IServiceScopeFactory
    {
        private readonly TaskCompletionSource _firstScope = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task FirstScope => _firstScope.Task;

        public IServiceScope CreateScope()
        {
            _firstScope.TrySetResult();
            return inner.CreateScope();
        }
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