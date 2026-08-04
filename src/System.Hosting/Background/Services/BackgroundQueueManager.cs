using Microsoft.Extensions.DependencyInjection;
using Regira.System.Hosting.Background.Abstractions;
using Regira.System.Hosting.Background.Models;

namespace Regira.System.Hosting.Background.Services;

public class BackgroundQueueManager(
    IBackgroundTaskQueue queue,
    IBackgroundTaskManager backgroundTaskManager,
    IServiceProvider serviceProvider)
    : BackgroundQueueManager<BackgroundTask>(queue, backgroundTaskManager, serviceProvider), IBackgroundQueueManager;
public class BackgroundQueueManager<TTask>(
    IBackgroundTaskQueue queue,
    IBackgroundTaskManager<TTask> backgroundTaskManager,
    IServiceProvider serviceProvider)
    : IBackgroundQueueManager<TTask>
    where TTask : class, IBackgroundTask, new()
{
    public event Action<Exception, TTask, IServiceProvider>? OnError;


    public TTask Execute(Action<IServiceProvider, TTask> action)
    {
        return Execute<object?>((sp, bgTask) =>
        {
            action(sp, bgTask);
            return null!;
        });
    }
    public TTask Execute<TResult>(Func<IServiceProvider, TTask, Task<TResult>> func)
    {
        var bgTask = new TTask
        {
            Id = Guid.NewGuid().ToString()
        };
        queue.QueueBackgroundWorkItem(async token =>
        {
            bgTask.Start(token);

            using var scope = serviceProvider.CreateScope();
            try
            {
                var result = await func(scope.ServiceProvider, bgTask);
                bgTask.Finish(result);
            }
            catch (Exception ex)
            {
                bgTask.SetError(ex);
                OnError?.Invoke(ex, bgTask, scope.ServiceProvider);
            }
        });
        backgroundTaskManager.Add(bgTask);

        return bgTask;
    }
}