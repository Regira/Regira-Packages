using Regira.System.Hosting.Background.Models;

namespace Regira.System.Hosting.Background.Abstractions;

public interface IBackgroundTaskManager : IBackgroundTaskManager<BackgroundTask>;
public interface IBackgroundTaskManager<TTask>
    where TTask : class, IBackgroundTask, new()
{
    IList<TTask> List();
    TTask? Find(string id);
    void Add(TTask task);
    bool Remove(string id);
    void Clear();
}