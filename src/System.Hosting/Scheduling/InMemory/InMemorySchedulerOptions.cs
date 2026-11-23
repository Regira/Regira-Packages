using Microsoft.Extensions.Hosting;

namespace Regira.System.Hosting.Scheduling.InMemory;

public sealed class InMemorySchedulerOptions
{
    /// <summary>How many runs execute at the same time. A run that is due beyond that waits for a free place.</summary>
    public int MaxConcurrency { get; set; } = Environment.ProcessorCount;
    /// <summary>
    /// How late a recurring occurrence may start before it counts as missed
    /// and <see cref="Regira.System.Scheduling.Abstractions.RecurringJobOptions.Misfire"/> decides what happens to it.
    /// </summary>
    public TimeSpan MisfireThreshold { get; set; } = TimeSpan.FromMinutes(1);
}

/// <summary>Starts the built-in scheduler with the host and stops it on shutdown, signalling the token of every running job.</summary>
internal sealed class InMemoryJobWorker(InMemoryJobScheduler scheduler) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        scheduler.Start();
        return Task.CompletedTask;
    }
    public Task StopAsync(CancellationToken cancellationToken) => scheduler.Stop(cancellationToken);
}
