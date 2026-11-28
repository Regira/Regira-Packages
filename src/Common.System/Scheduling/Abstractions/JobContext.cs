namespace Regira.System.Scheduling.Abstractions;

/// <summary>What a run knows about itself.</summary>
/// <param name="JobId">The id of this run. For a job that was enqueued or scheduled, the id <see cref="IJobScheduler"/> returned.</param>
/// <param name="RecurringId">The id of the recurring job this run belongs to, or <c>null</c> for a one-off job.</param>
/// <param name="ScheduledAt">
/// The time this run was due, when the backend knows it. For a recurring run on Hangfire it is the time Hangfire created the run,
/// which is up to its polling interval after the occurrence.
/// </param>
public sealed record JobContext(string JobId, string? RecurringId, DateTimeOffset? ScheduledAt);
