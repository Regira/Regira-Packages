using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Regira.System.Hosting.Scheduling;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Scheduling.Quartz;

/// <summary>
/// The Quartz job of a handler without input. Quartz creates it in the scope of the run, and it runs <typeparamref name="TJob"/> from that scope.
/// The stored job names this type with its type argument: renaming or moving a handler breaks the jobs already stored.
/// </summary>
public sealed class QuartzJobRunner<TJob>(IServiceProvider scopedServices) : IJob
    where TJob : IJobHandler
{
    public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
    {
        using var stopping = QuartzRun.LinkToShutdown(scopedServices, cancellationToken);
        await JobHandlerInvoker.Run<TJob>(scopedServices, QuartzRun.ToJobContext(context), stopping.Token);
    }
}

/// <summary>
/// The Quartz job of a handler with an input. Quartz creates it in the scope of the run, and it runs <typeparamref name="TJob"/> from that scope.
/// The stored job names this type with its type arguments: renaming or moving a handler or its input breaks the jobs already stored.
/// </summary>
public sealed class QuartzJobRunner<TJob, TInput>(IServiceProvider scopedServices) : IJob<TInput>
    where TJob : IJobHandler<TInput>
{
    public async ValueTask Execute(IJobExecutionContext context, TInput input, CancellationToken cancellationToken = default)
    {
        using var stopping = QuartzRun.LinkToShutdown(scopedServices, cancellationToken);
        await JobHandlerInvoker.Run<TJob, TInput>(scopedServices, input, QuartzRun.ToJobContext(context), stopping.Token);
    }
}

internal static class QuartzRun
{
    // A one-off job is known by its trigger's name, which is the id the scheduler returned. A recurring job is known by
    // its job's name, and each of its runs by the id of the firing.
    public static JobContext ToJobContext(IJobExecutionContext context)
    {
        var recurring = context.JobDetail.Key.Group == QuartzJobScheduler.RecurringGroup;
        return new JobContext(
            recurring ? context.FireInstanceId : context.Trigger.Key.Name,
            recurring ? context.JobDetail.Key.Name : null,
            context.ScheduledFireTimeUtc
        );
    }
    // Quartz signals a run's token when the run is interrupted, not when the host stops.
    public static CancellationTokenSource LinkToShutdown(IServiceProvider services, CancellationToken token)
    {
        var lifetime = services.GetService<IHostApplicationLifetime>();
        return lifetime == null
            ? CancellationTokenSource.CreateLinkedTokenSource(token)
            : CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.ApplicationStopping);
    }
}
