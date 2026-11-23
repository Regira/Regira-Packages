using Regira.System.Hosting.Scheduling;

namespace Regira.System.Scheduling.Hangfire;

public static class SchedulingBuilderExtensions
{
    /// <summary>
    /// Runs the jobs on Hangfire in place of the built-in backend. The application registers Hangfire itself:
    /// <c>AddHangfire</c> with its storage, and <c>AddHangfireServer</c> on the nodes that process jobs.
    /// </summary>
    public static SchedulingBuilder UseHangfire(this SchedulingBuilder builder)
        => builder.UseScheduler<HangfireJobScheduler>();
}
