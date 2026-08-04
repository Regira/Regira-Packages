using Regira.System.Hosting.Background.Models;

namespace Regira.System.Hosting.Background.Abstractions;

public interface IBackgroundQueueManager : IBackgroundQueueManager<BackgroundTask>;
public interface IBackgroundQueueManager<out TTask>
    where TTask : class, IBackgroundTask, new()
{
    event Action<Exception, TTask, IServiceProvider> OnError;

    TTask Execute(Action<IServiceProvider, TTask> action);
    TTask Execute<TResult>(Func<IServiceProvider, TTask, Task<TResult>> func);
}