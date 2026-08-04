using System.Collections.Concurrent;
using Regira.System.Hosting.Background.Abstractions;
using Regira.System.Hosting.Background.Models;

namespace Regira.System.Hosting.Background.Services;

public class BackgroundTaskManager : BackgroundTaskManager<BackgroundTask>, IBackgroundTaskManager;
public class BackgroundTaskManager<TTask> : IBackgroundTaskManager<TTask>
    where TTask : class, IBackgroundTask, new()
{
    private static readonly IDictionary<string, TTask> Tasks = new ConcurrentDictionary<string, TTask>();

    public IList<TTask> List()
    {
        return Tasks.Values.ToList();
    }
    public TTask? Find(string id)
    {
        if (Tasks.TryGetValue(id, out var task))
        {
            return task;
        }

        return null;
    }

    public void Add(TTask task)
    {
        Tasks[task.Id] = task;
    }
    public bool Remove(string id)
    {
        if (Tasks.TryGetValue(id, out var task))
        {
            return Tasks.Remove(task.Id);
        }

        return false;
    }
    public void Clear()
    {
        Tasks.Clear();
    }
}