namespace Regira.System.Scheduling.Abstractions;

/// <summary>
/// Runs jobs in the background: now, once at a time, or on a cron schedule.
/// Whether a failed run is retried, whether a job survives a restart and how many nodes run it depend on the backend.
/// </summary>
public interface IJobScheduler
{
    /// <summary>Runs the job as soon as possible. Returns the id <see cref="Cancel"/> takes.</summary>
    Task<string> Enqueue<TJob>(CancellationToken token = default) where TJob : IJobHandler;
    /// <inheritdoc cref="Enqueue{TJob}(CancellationToken)"/>
    Task<string> Enqueue<TJob, TInput>(TInput input, CancellationToken token = default) where TJob : IJobHandler<TInput>;

    /// <summary>Runs the job once, at the given time. Returns the id <see cref="Cancel"/> takes.</summary>
    Task<string> Schedule<TJob, TInput>(TInput input, DateTimeOffset at, CancellationToken token = default) where TJob : IJobHandler<TInput>;
    /// <summary>
    /// Cancels a job that was enqueued or scheduled. A run that already started gets its token signalled where the backend supports that:
    /// also a run of a recurring job, by the <see cref="JobContext.JobId"/> of that run.
    /// Returns <c>false</c> for a job that already ran or does not exist.
    /// </summary>
    Task<bool> Cancel(string jobId, CancellationToken token = default);

    /// <summary>Adds a recurring job, or replaces the one with the same id.</summary>
    Task AddOrUpdateRecurring<TJob>(string id, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default) where TJob : IJobHandler;
    /// <inheritdoc cref="AddOrUpdateRecurring{TJob}(string, CronSchedule, RecurringJobOptions?, CancellationToken)"/>
    Task AddOrUpdateRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default) where TJob : IJobHandler<TInput>;
    /// <summary>Removes a recurring job. Returns <c>false</c> when no recurring job has the id.</summary>
    Task<bool> RemoveRecurring(string id, CancellationToken token = default);
    /// <summary>Runs a recurring job now, outside its schedule. Returns <c>false</c> when no recurring job has the id.</summary>
    Task<bool> Trigger(string id, CancellationToken token = default);
}
