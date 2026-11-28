using Quartz;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Scheduling.Quartz;

/// <summary>
/// <see cref="IJobScheduler"/> on the Quartz scheduler the application registered with <c>AddQuartz</c>:
/// its job store, its clustering and its thread pool apply as configured.
/// A job is a <see cref="QuartzJobRunner{TJob}"/> or <see cref="QuartzJobRunner{TJob, TInput}"/>; a recurring job is a durable job with one cron trigger.
/// </summary>
public sealed class QuartzJobScheduler(ISchedulerFactory schedulerFactory) : IJobScheduler
{
    /// <summary>The job and trigger group of the recurring jobs: a recurring job's id is its name in this group.</summary>
    public const string RecurringGroup = "regira.recurring";
    /// <summary>The trigger group of the jobs that run once: a job's id is its trigger's name in this group.</summary>
    public const string JobGroup = "regira.jobs";

    public async Task<string> Enqueue<TJob>(CancellationToken token = default)
        where TJob : IJobHandler
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(NewId(), JobGroup)
            .StartNow()
            .Build();
        // the job takes the trigger's key and goes when the trigger has fired
        await scheduler.ScheduleJob<QuartzJobRunner<TJob>>(trigger, cancellationToken: token);
        return trigger.Key.Name;
    }
    public Task<string> Enqueue<TJob, TInput>(TInput input, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
        => Schedule<TJob, TInput>(input, DateTimeOffset.UtcNow, token);
    public async Task<string> Schedule<TJob, TInput>(TInput input, DateTimeOffset at, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        var scheduled = await scheduler.ScheduleJob<QuartzJobRunner<TJob, TInput>, TInput>(input, at, new OneOffJobOptions { Name = NewId(), Group = JobGroup }, token);
        return scheduled.TriggerKey.Name;
    }

    public async Task<bool> Cancel(string jobId, CancellationToken token = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        var triggerKey = new TriggerKey(jobId, JobGroup);

        // A run that already started is not stopped by removing its trigger: signal the token of that run.
        // Its firing is looked up first, since it is listed by its trigger.
        var cancelled = false;
        var firings = await scheduler.QueryFireInstances(new FireInstanceQuery
        {
            TriggerGroup = GroupMatcher<TriggerKey>.GroupEquals(JobGroup),
            TriggerName = NameMatcher<TriggerKey>.NameEquals(jobId)
        }, token);
        foreach (var running in firings.Items)
        {
            cancelled |= await scheduler.InterruptFireInstance(running.FireInstanceId, token);
        }

        // A recurring run is known by the id of its firing (JobContext.JobId). The scheduler also runs the jobs the application
        // gave Quartz itself: only a firing of a recurring job of this scheduler is interrupted.
        var recurringRuns = await scheduler.QueryFireInstances(new FireInstanceQuery { Take = PagedQuery.All }, token);
        if (recurringRuns.Items.Any(f => f.FireInstanceId == jobId && f.JobKey?.Group == RecurringGroup))
        {
            cancelled |= await scheduler.InterruptFireInstance(jobId, token);
        }

        return await scheduler.UnscheduleJob(triggerKey, token) || cancelled;
    }

    public async Task AddOrUpdateRecurring<TJob>(string id, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler
    {
        var job = JobBuilder.Create<QuartzJobRunner<TJob>>()
            .WithIdentity(id, RecurringGroup)
            .StoreDurably()
            .DisallowConcurrentExecution(options?.AllowConcurrent != true)
            .Build();
        await ScheduleRecurring(job, schedule, options, token);
    }
    public async Task AddOrUpdateRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
    {
        var job = JobBuilder.Create<QuartzJobRunner<TJob, TInput>>()
            .WithIdentity(id, RecurringGroup)
            .StoreDurably()
            .DisallowConcurrentExecution(options?.AllowConcurrent != true)
            .UsingInput(input)
            .Build();
        await ScheduleRecurring(job, schedule, options, token);
    }
    public async Task<bool> RemoveRecurring(string id, CancellationToken token = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        return await scheduler.DeleteJob(new JobKey(id, RecurringGroup), token);
    }
    public async Task<bool> Trigger(string id, CancellationToken token = default)
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        var jobKey = new JobKey(id, RecurringGroup);
        if (!await scheduler.Exists(jobKey, token))
        {
            return false;
        }

        await scheduler.TriggerJob(jobKey, token);
        return true;
    }

    private async Task ScheduleRecurring(IJobDetail job, CronSchedule schedule, RecurringJobOptions? options, CancellationToken token)
    {
        var scheduler = await schedulerFactory.GetScheduler(token);
        // the trigger always names its time zone: Quartz would otherwise use the machine's
        var cron = CronScheduleBuilder.Create(QuartzCron.Translate(schedule))
            .InTimeZone(options?.TimeZone ?? TimeZoneInfo.Utc)
            .WithMisfireInstruction(options?.Misfire == MisfirePolicy.Skip ? CronTriggerMisfireInstruction.DoNothing : CronTriggerMisfireInstruction.FireAndProceed);
        var trigger = TriggerBuilder.Create()
            .WithIdentity(job.Key.Name, RecurringGroup)
            .ForJob(job)
            .WithSchedule(cron)
            .Build();
        await scheduler.ScheduleJob(job, trigger, ScheduleJobOptions.Replacing, token);
    }
    private static string NewId() => Guid.NewGuid().ToString("N");
}
