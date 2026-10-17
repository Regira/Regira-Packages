using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.System.Hosting.Background.Abstractions;

namespace Regira.System.Hosting.Background.Core;

/// <summary>
/// Runs the work items of an <see cref="IBackgroundTaskQueue"/> one after the other.
/// A work item that throws is logged, and the next one runs.
/// </summary>
public class QueuedHostedService : BackgroundService
{
    private readonly ILogger _logger;

    public QueuedHostedService(IBackgroundTaskQueue taskQueue)
        : this(taskQueue, NullLogger<QueuedHostedService>.Instance)
    {
    }
    public QueuedHostedService(IBackgroundTaskQueue taskQueue, ILogger<QueuedHostedService> logger)
    {
        TaskQueue = taskQueue;
        _logger = logger;
    }

    public IBackgroundTaskQueue TaskQueue { get; }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var workItem = await TaskQueue.DequeueAsync(cancellationToken);
            if (workItem == null)
            {
                continue;
            }

            try
            {
                await workItem(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "A background work item failed");
            }
        }
    }
}
