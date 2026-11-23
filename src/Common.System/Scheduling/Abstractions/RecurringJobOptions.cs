namespace Regira.System.Scheduling.Abstractions;

public sealed class RecurringJobOptions
{
    /// <summary>The time zone the cron expression is read in. Defaults to UTC.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    /// <summary>What happens to an occurrence that was missed. It is never replayed occurrence by occurrence.</summary>
    public MisfirePolicy Misfire { get; set; } = MisfirePolicy.RunOnce;
    /// <summary>When <c>false</c> (the default), a run of this job never overlaps its previous run.</summary>
    public bool AllowConcurrent { get; set; }
}

public enum MisfirePolicy
{
    /// <summary>The missed occurrences run as one run, when the scheduler is back.</summary>
    RunOnce,
    /// <summary>The missed occurrences are skipped; the job waits for its next one.</summary>
    Skip
}
