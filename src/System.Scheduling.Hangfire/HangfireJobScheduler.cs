using Hangfire;
using Hangfire.States;
using Regira.System.Scheduling.Abstractions;
using HangfireRecurringJobOptions = Hangfire.RecurringJobOptions;
using RecurringJobOptions = Regira.System.Scheduling.Abstractions.RecurringJobOptions;

namespace Regira.System.Scheduling.Hangfire;

/// <summary>
/// <see cref="IJobScheduler"/> on the Hangfire installation the application registered with <c>AddHangfire</c>:
/// its storage, its servers and its retry filter apply as configured.
/// A job is stored as a call to <see cref="HangfireJobRunner{TJob}"/> or <see cref="HangfireJobRunner{TJob, TInput}"/>.
/// </summary>
public sealed class HangfireJobScheduler(IBackgroundJobClient client, IRecurringJobManager recurringJobs, JobStorage storage) : IJobScheduler
{
    // the states a job can still be cancelled from
    private static readonly string[] CancellableStates = [ScheduledState.StateName, EnqueuedState.StateName, AwaitingState.StateName, ProcessingState.StateName];

    public Task<string> Enqueue<TJob>(CancellationToken token = default)
        where TJob : IJobHandler
        => Task.FromResult(client.Enqueue<HangfireJobRunner<TJob>>(r => r.Run(null, null, false, null, CancellationToken.None)));
    public Task<string> Enqueue<TJob, TInput>(TInput input, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
        => Task.FromResult(client.Enqueue<HangfireJobRunner<TJob, TInput>>(r => r.Run(input, null, null, false, null, CancellationToken.None)));
    public Task<string> Schedule<TJob, TInput>(TInput input, DateTimeOffset at, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
        => Task.FromResult(client.Schedule<HangfireJobRunner<TJob, TInput>>(r => r.Run(input, null, at, false, null, CancellationToken.None), at));

    public Task<bool> Cancel(string jobId, CancellationToken token = default)
    {
        // Deleting a job that is processing signals its token on the server that runs it.
        var cancelled = CancellableStates.Any(state => client.ChangeState(jobId, new DeletedState(), state));
        return Task.FromResult(cancelled);
    }

    public Task AddOrUpdateRecurring<TJob>(string id, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler
    {
        var exclusive = options?.AllowConcurrent != true;
        recurringJobs.AddOrUpdate<HangfireJobRunner<TJob>>(id, r => r.Run(id, null, exclusive, null, CancellationToken.None), schedule.Expression, Map(options));
        return Task.CompletedTask;
    }
    public Task AddOrUpdateRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
    {
        var exclusive = options?.AllowConcurrent != true;
        recurringJobs.AddOrUpdate<HangfireJobRunner<TJob, TInput>>(id, r => r.Run(input, id, null, exclusive, null, CancellationToken.None), schedule.Expression, Map(options));
        return Task.CompletedTask;
    }
    public Task<bool> RemoveRecurring(string id, CancellationToken token = default)
    {
        if (!Exists(id))
        {
            return Task.FromResult(false);
        }

        recurringJobs.RemoveIfExists(id);
        return Task.FromResult(true);
    }
    public Task<bool> Trigger(string id, CancellationToken token = default)
    {
        if (!Exists(id))
        {
            return Task.FromResult(false);
        }

        recurringJobs.Trigger(id);
        return Task.FromResult(true);
    }

    private bool Exists(string recurringId)
    {
        using var connection = storage.GetConnection();
        return connection.GetAllEntriesFromHash($"recurring-job:{recurringId}") is { Count: > 0 };
    }
    private static HangfireRecurringJobOptions Map(RecurringJobOptions? options)
        => new()
        {
            TimeZone = options?.TimeZone ?? TimeZoneInfo.Utc,
            MisfireHandling = options?.Misfire == MisfirePolicy.Skip ? MisfireHandlingMode.Ignorable : MisfireHandlingMode.Relaxed
        };
}
