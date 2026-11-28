using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Regira.System.Hosting.Scheduling.InMemory;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Hosting.Scheduling;

/// <summary>Declares the recurring jobs of an application and chooses its scheduling backend.</summary>
public sealed class SchedulingBuilder
{
    internal SchedulingBuilder(IServiceCollection services, InMemorySchedulerOptions inMemory)
    {
        Services = services;
        InMemory = inMemory;
    }

    public IServiceCollection Services { get; }
    /// <summary>Settings of the built-in backend. They have no effect once another backend is registered.</summary>
    public InMemorySchedulerOptions InMemory { get; }

    /// <summary>
    /// Declares a recurring job. It is added to the scheduler, or replaces the one with the same id, when the host starts.
    /// </summary>
    /// <exception cref="FormatException">The cron expression is outside the portable subset.</exception>
    public SchedulingBuilder AddRecurring<TJob>(string id, CronSchedule schedule, Action<RecurringJobOptions>? configure = null)
        where TJob : class, IJobHandler
    {
        var options = CreateOptions(configure);
        return Declare<TJob>(id, schedule, (scheduler, token) => scheduler.AddOrUpdateRecurring<TJob>(id, schedule, options, token));
    }
    /// <inheritdoc cref="AddRecurring{TJob}(string, CronSchedule, Action{RecurringJobOptions}?)"/>
    public SchedulingBuilder AddRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, Action<RecurringJobOptions>? configure = null)
        where TJob : class, IJobHandler<TInput>
    {
        var options = CreateOptions(configure);
        return Declare<TJob>(id, schedule, (scheduler, token) => scheduler.AddOrUpdateRecurring<TJob, TInput>(id, input, schedule, options, token));
    }

    /// <summary>Replaces the scheduling backend. The declared recurring jobs are applied to it when the host starts.</summary>
    public SchedulingBuilder UseScheduler<TScheduler>()
        where TScheduler : class, IJobScheduler
    {
        Services.RemoveAll<IJobScheduler>();
        Services.RemoveAll<InMemoryJobScheduler>();
        var worker = Services.FirstOrDefault(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(InMemoryJobWorker));
        if (worker != null)
        {
            Services.Remove(worker);
        }

        Services.AddSingleton<IJobScheduler, TScheduler>();
        return this;
    }

    private SchedulingBuilder Declare<TJob>(string id, CronSchedule schedule, Func<IJobScheduler, CancellationToken, Task> apply)
        where TJob : class
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A recurring job needs an id.", nameof(id));
        }
        // reading the expression refuses an empty schedule here, at registration
        _ = schedule.Expression;
        if (Services.Any(d => d.ImplementationInstance is RecurringJobDeclaration declared && declared.Id == id))
        {
            throw new ArgumentException($"A recurring job with id '{id}' is already declared.", nameof(id));
        }

        Services.TryAddScoped<TJob>();
        Services.AddSingleton(new RecurringJobDeclaration(id, apply));
        return this;
    }
    private static RecurringJobOptions CreateOptions(Action<RecurringJobOptions>? configure)
    {
        var options = new RecurringJobOptions();
        configure?.Invoke(options);
        return options;
    }
}
