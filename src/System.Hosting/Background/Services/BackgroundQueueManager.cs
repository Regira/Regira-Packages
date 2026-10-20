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
    // The root's factory, whatever scope built this manager: one injected into a controller holds the request's scope,
    // which is disposed by the time the work item runs.
    private readonly IServiceScopeFactory _scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

    public event Action<Exception, TTask, IServiceProvider>? OnError;


    public TTask Execute(Action<IServiceProvider, TTask> action)
    {
        return Execute<object?>((sp, bgTask) =>
        {
            action(sp, bgTask);
            return Task.FromResult<object?>(null);
        });
    }
    public TTask Execute<TResult>(Func<IServiceProvider, TTask, Task<TResult>> func)
    {
        var bgTask = new TTask
        {
            Id = Guid.NewGuid().ToString()
        };
        // known before it can run, so a poll by its id finds it from the start
        backgroundTaskManager.Add(bgTask);
        queue.QueueBackgroundWorkItem(async token =>
        {
            bgTask.Start(token);

            IServiceScope? scope = null;
            try
            {
                scope = _scopeFactory.CreateScope();
                var result = await func(scope.ServiceProvider, bgTask);
                bgTask.Finish(result);
            }
            catch (Exception ex)
            {
                bgTask.SetError(ex);
                OnError?.Invoke(ex, bgTask, scope?.ServiceProvider ?? serviceProvider);
            }
            finally
            {
                scope?.Dispose();
            }
        });

        return bgTask;
    }
}
