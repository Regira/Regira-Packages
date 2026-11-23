using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Regira.System.Hosting.Scheduling.InMemory;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Hosting.Scheduling.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IJobScheduler"/> with the built-in backend: in process and in memory, for development, tests and
    /// a single-node application. Nothing is stored, so a job that was accepted is lost on a restart, and a recurring job
    /// runs on every node that hosts the application. Register an adapter on the returned builder for a persistent backend.
    /// </summary>
    public static SchedulingBuilder AddScheduling(this IServiceCollection services, Action<SchedulingBuilder>? configure = null)
    {
        var inMemory = services.Select(d => d.ImplementationInstance).OfType<InMemorySchedulerOptions>().FirstOrDefault();
        if (inMemory == null)
        {
            inMemory = new InMemorySchedulerOptions();
            services.AddSingleton(inMemory);
        }

        if (services.All(d => d.ServiceType != typeof(IJobScheduler)))
        {
            services.AddSingleton<InMemoryJobScheduler>();
            services.AddSingleton<IJobScheduler>(p => p.GetRequiredService<InMemoryJobScheduler>());
            services.AddHostedService<InMemoryJobWorker>();
        }
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, RecurringJobRegistrar>());

        var builder = new SchedulingBuilder(services, inMemory);
        configure?.Invoke(builder);
        return builder;
    }
}
