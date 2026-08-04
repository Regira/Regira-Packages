using Microsoft.Extensions.DependencyInjection;
using Regira.System.Hosting.Background.Abstractions;
using Regira.System.Hosting.Background.Core;
using Regira.System.Hosting.Background.Services;

namespace Regira.System.Hosting.Background.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseBackgroundQueue(this IServiceCollection services, Action<BackgroundTaskQueue.Options>? configure = null)
    {
        var options = new BackgroundTaskQueue.Options();
        configure?.Invoke(options);
        return services
            .AddHostedService<QueuedHostedService>()
            .AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(options))
            .AddTransient<IBackgroundTaskManager, BackgroundTaskManager>()
            .AddTransient<IBackgroundQueueManager, BackgroundQueueManager>();
    }
    public static IServiceCollection UseBackgroundQueue<TTask>(this IServiceCollection services, Action<BackgroundTaskQueue.Options>? configure = null)
        where TTask : class, IBackgroundTask, new()
    {
        var options = new BackgroundTaskQueue.Options();
        configure?.Invoke(options);
        return services
            .AddHostedService<QueuedHostedService>()
            .AddSingleton<IBackgroundTaskQueue>(_ => new BackgroundTaskQueue(options))
            .AddTransient<IBackgroundTaskManager<TTask>, BackgroundTaskManager<TTask>>()
            .AddTransient<IBackgroundQueueManager<TTask>, BackgroundQueueManager<TTask>>();
    }
}