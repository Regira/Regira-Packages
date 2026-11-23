using Regira.System.Hosting.Scheduling;

namespace Regira.System.Scheduling.Quartz;

public static class SchedulingBuilderExtensions
{
    /// <summary>
    /// Runs the jobs on Quartz in place of the built-in backend. The application registers Quartz itself:
    /// <c>AddQuartz</c> with its job store, and <c>AddQuartzHostedService</c> to start the scheduler with the host.
    /// </summary>
    public static SchedulingBuilder UseQuartz(this SchedulingBuilder builder)
        => builder.UseScheduler<QuartzJobScheduler>();
}
