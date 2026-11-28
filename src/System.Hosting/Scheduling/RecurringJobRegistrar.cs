using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Hosting.Scheduling;

/// <summary>A recurring job declared at registration, applied to the scheduler when the host starts.</summary>
internal sealed class RecurringJobDeclaration(string id, Func<IJobScheduler, CancellationToken, Task> apply)
{
    public string Id { get; } = id;
    public Func<IJobScheduler, CancellationToken, Task> Apply { get; } = apply;
}

/// <summary>
/// Applies the declared recurring jobs through <c>AddOrUpdateRecurring</c> when the host starts,
/// so a persistent backend holds the declared schedule after every deployment.
/// </summary>
internal sealed class RecurringJobRegistrar(IJobScheduler scheduler, IEnumerable<RecurringJobDeclaration> declarations, ILogger<RecurringJobRegistrar> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var declaration in declarations)
        {
            await declaration.Apply(scheduler, cancellationToken);
            logger.LogDebug("Recurring job {RecurringId} is scheduled", declaration.Id);
        }
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
