using Hangfire.Server;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.System.Hosting.Scheduling;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Scheduling.Hangfire;

/// <summary>
/// The call Hangfire stores for a job without input. Hangfire creates it in the scope of the run, and it runs <typeparamref name="TJob"/> from that scope.
/// The stored job names this type with its type arguments: renaming or moving a handler breaks the jobs already stored.
/// </summary>
public sealed class HangfireJobRunner<TJob>(IServiceProvider scopedServices)
    where TJob : IJobHandler
{
    public Task Run(string? recurringId, DateTimeOffset? scheduledAt, bool exclusive, PerformContext? context, CancellationToken token)
        => HangfireRun.Execute(scopedServices, context, recurringId, scheduledAt, exclusive,
            jobContext => JobHandlerInvoker.Run<TJob>(scopedServices, jobContext, token));
}

/// <summary>
/// The call Hangfire stores for a job with an input. Hangfire creates it in the scope of the run, and it runs <typeparamref name="TJob"/> from that scope.
/// The stored job names this type with its type arguments: renaming or moving a handler or its input breaks the jobs already stored.
/// </summary>
public sealed class HangfireJobRunner<TJob, TInput>(IServiceProvider scopedServices)
    where TJob : IJobHandler<TInput>
{
    public Task Run(TInput input, string? recurringId, DateTimeOffset? scheduledAt, bool exclusive, PerformContext? context, CancellationToken token)
        => HangfireRun.Execute(scopedServices, context, recurringId, scheduledAt, exclusive,
            jobContext => JobHandlerInvoker.Run<TJob, TInput>(scopedServices, input, jobContext, token));
}

internal static class HangfireRun
{
    public static async Task Execute(IServiceProvider scopedServices, PerformContext? context, string? recurringId, DateTimeOffset? scheduledAt, bool exclusive,
        Func<JobContext, Task> run)
    {
        if (context == null)
        {
            throw new InvalidOperationException("A job runner is called by a Hangfire server, which passes the context of the run.");
        }

        var jobContext = new JobContext(context.BackgroundJob.Id, recurringId, scheduledAt ?? ReadScheduledTime(context));
        if (!exclusive || recurringId == null)
        {
            await run(jobContext);
            return;
        }

        // A lock per recurring job, so two different jobs never wait for each other.
        // It is not waited for: a run that finds it taken is the overlap the job asked not to have.
        IDisposable distributedLock;
        try
        {
            distributedLock = context.Connection.AcquireDistributedLock($"regira:recurring:{recurringId}", TimeSpan.Zero);
        }
        catch (DistributedLockTimeoutException)
        {
            scopedServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(HangfireJobScheduler))
                .LogInformation("Recurring job {RecurringId} is still running and skips run {JobId}", recurringId, jobContext.JobId);
            return;
        }

        using (distributedLock)
        {
            await run(jobContext);
        }
    }

    // Hangfire stores the time a recurring run was due in the "Time" parameter, as a Unix timestamp.
    private static DateTimeOffset? ReadScheduledTime(PerformContext context)
    {
        try
        {
            var timestamp = context.GetJobParameter<long?>("Time");
            return timestamp == null ? null : DateTimeOffset.FromUnixTimeSeconds(timestamp.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
