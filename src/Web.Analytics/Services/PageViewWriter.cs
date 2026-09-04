using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Models;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Drains the queue, runs the enrichers, masks the IP and hands batches to the registered store — all
/// off the request thread. Store and enrichers are resolved from a fresh scope per batch.
/// </summary>
public class PageViewWriter<TPageView>(PageViewQueue<TPageView> queue, AnalyticsConfig config,
    IServiceScopeFactory scopeFactory, ILogger<PageViewWriter<TPageView>> logger) : BackgroundService
    where TPageView : IPageView
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (config.Ipv4PrefixLength is < 0 or > 32)
            logger.LogWarning("Analytics: Ipv4PrefixLength {Value} is out of range (0-32) — masking IPv4 to the default /{Default}",
                config.Ipv4PrefixLength, IpMasker.DefaultIpv4PrefixLength);
        if (config.Ipv6PrefixLength is < 0 or > 128)
            logger.LogWarning("Analytics: Ipv6PrefixLength {Value} is out of range (0-128) — masking IPv6 to the default /{Default}",
                config.Ipv6PrefixLength, IpMasker.DefaultIpv6PrefixLength);

        bool hasRetentionStore;
        try
        {
            // Registration check only — instantiating here would turn a store fault into a host that
            // fails to start, and analytics is not a service the app should die for.
            using var scope = scopeFactory.CreateScope();
            if (!AnalyticsExtensions.IsRegistered(scope.ServiceProvider, typeof(IPageViewStore<TPageView>)))
            {
                logger.LogWarning("Analytics: no IPageViewStore<{ViewType}> registered — visit tracking is inactive",
                    typeof(TPageView).Name);
                return;
            }

            hasRetentionStore = AnalyticsExtensions.IsRegistered(scope.ServiceProvider, typeof(IPageViewRetentionStore));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Analytics: startup check failed — visit tracking is inactive");
            return;
        }

        await Task.WhenAll(
            DrainAsync(stoppingToken),
            PurgeLoopAsync(hasRetentionStore, stoppingToken));
    }

    private async Task DrainAsync(CancellationToken stoppingToken)
    {
        var flushInterval = TimeSpan.FromSeconds(Math.Max(1, config.FlushIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await queue.Reader.WaitToReadAsync(stoppingToken))
                    return;

                // Let a few more visits arrive so they go to the store as one batch.
                await Task.Delay(flushInterval, stoppingToken);

                // Drain the whole backlog — waiting one interval per batch would cap throughput
                // at BatchSize / FlushIntervalSeconds and overflow the queue under a burst.
                while (ReadBatch() is { Count: > 0 } batch)
                    await SaveAsync(batch, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                await FlushRemainingAsync();
                return;
            }
            catch (Exception ex)
            {
                // A write failure must never kill the loop.
                logger.LogError(ex, "Analytics: failed to write page views");
                await Task.Delay(TimeSpan.FromSeconds(30), CancellationToken.None);
            }
        }

        await FlushRemainingAsync();
    }

    private async Task FlushRemainingAsync()
    {
        try
        {
            while (ReadBatch() is { Count: > 0 } batch)
                await SaveAsync(batch, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Analytics: could not flush remaining page views on shutdown");
        }
    }

    private List<PendingPageView<TPageView>> ReadBatch()
    {
        var batch = new List<PendingPageView<TPageView>>();
        while (batch.Count < config.BatchSize && queue.Reader.TryRead(out var pending))
            batch.Add(pending);
        return batch;
    }

    private async Task SaveAsync(List<PendingPageView<TPageView>> batch, CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();

        var enrichers = scope.ServiceProvider.GetServices<IPageViewEnricher<TPageView>>().ToArray();
        foreach (var pending in batch)
        {
            foreach (var enricher in enrichers)
            {
                // A throwing enricher costs its own data, not the batch.
                try
                {
                    await enricher.EnrichAsync(pending, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Analytics: enricher {Enricher} failed", enricher.GetType().Name);
                }
            }

            // After enrichment: enrichers get the full address, the store never does (unless masking is off).
            pending.View.IpAddress = config.MaskIpAddress
                ? IpMasker.Mask(pending.ClientIp, config.Ipv4PrefixLength, config.Ipv6PrefixLength)
                : IpMasker.Normalize(pending.ClientIp)?.ToString();
        }

        var store = scope.ServiceProvider.GetRequiredService<IPageViewStore<TPageView>>();
        await store.SaveAsync(batch.Select(b => b.View).ToList(), stoppingToken);

        logger.LogDebug("Analytics: stored {Count} page views", batch.Count);
    }

    private async Task PurgeLoopAsync(bool hasRetentionStore, CancellationToken stoppingToken)
    {
        if (!hasRetentionStore || config.RetentionDays <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
        try
        {
            do
            {
                try
                {
                    var cutoff = DateTime.UtcNow.AddDays(-config.RetentionDays);
                    using var scope = scopeFactory.CreateScope();
                    var store = scope.ServiceProvider.GetRequiredService<IPageViewRetentionStore>();
                    var deleted = await store.PurgeAsync(config.SiteName, cutoff, stoppingToken);

                    if (deleted > 0)
                        logger.LogInformation("Analytics: purged {Count} {Site} page views older than {Days} days",
                            deleted, config.SiteName, config.RetentionDays);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Analytics: retention purge failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }
}