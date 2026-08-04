using Microsoft.Extensions.Hosting;
using Regira.System.Hosting.Background.Abstractions;

namespace Regira.System.Hosting.Background.Core;

public class QueuedHostedService(IBackgroundTaskQueue taskQueue) : BackgroundService
{
    public IBackgroundTaskQueue TaskQueue { get; } = taskQueue;

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var workItem = await TaskQueue.DequeueAsync(cancellationToken);
            if (workItem != null)
            {
                await workItem(cancellationToken);
            }
        }
    }
}