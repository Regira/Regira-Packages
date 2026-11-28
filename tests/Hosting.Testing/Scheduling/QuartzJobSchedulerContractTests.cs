using Microsoft.Extensions.DependencyInjection;
using Quartz;
using Regira.System.Hosting.Scheduling;
using Regira.System.Hosting.Scheduling.DependencyInjection;
using Regira.System.Scheduling.Quartz;

namespace Hosting.Testing.Scheduling;

/// <summary>The portable contract on Quartz, with its in-memory job store.</summary>
[TestFixture]
public class QuartzJobSchedulerContractTests : JobSchedulerContractTests
{
    protected override void AddScheduling(IServiceCollection services, Action<SchedulingBuilder> declare)
    {
        services.AddQuartz(_ => { });
        services.AddQuartzHostedService();
        services.AddScheduling(declare).UseQuartz();
    }
    private class OwnQuartzJob(JobProbe probe) : IJob
    {
        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            probe.Add(new JobRun(nameof(OwnQuartzJob), new Regira.System.Scheduling.Abstractions.JobContext(context.FireInstanceId, null, null)));
            try
            {
                await probe.Released.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                probe.Cancelled.Enqueue("own");
                throw;
            }
        }
    }

    // The Quartz scheduler is the application's: a job it gave Quartz itself is not this scheduler's to cancel.
    [Test]
    public async Task Cancel_Leaves_The_Application_Its_Own_Jobs()
    {
        var scheduler = await Start();
        var quartz = await Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        await quartz.ScheduleJob(
            JobBuilder.Create<OwnQuartzJob>().WithIdentity("own", "app").Build(),
            TriggerBuilder.Create().WithIdentity("own", "app").StartNow().Build());
        var own = (await Probe.WaitFor(1)).Single();

        Assert.That(await scheduler.Cancel(own.Context.JobId), Is.False);
        Assert.That(await scheduler.Cancel("own"), Is.False);
        await Task.Delay(200);
        Assert.That(Probe.Cancelled, Is.Empty);
        // the same firing is interruptible through Quartz itself: the check above did not pass for want of a running job
        Assert.That(await quartz.InterruptFireInstance(own.Context.JobId), Is.True);
        await JobProbe.Until(() => Probe.Cancelled.Contains("own"));
    }

    protected override async Task WhenSettled(string jobId)
    {
        var scheduler = await Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        var until = DateTime.UtcNow.AddSeconds(20);
        var done = false;
        while (!done)
        {
            if (DateTime.UtcNow > until)
            {
                Assert.Fail($"Job {jobId} was not settled within 20s");
            }
            await Task.Delay(25);
            done = await scheduler.GetTrigger(new TriggerKey(jobId, QuartzJobScheduler.JobGroup)) == null
                   && (await scheduler.QueryFireInstances(new FireInstanceQuery { TriggerName = NameMatcher<TriggerKey>.NameEquals(jobId) })).Items.Count == 0;
        }
    }
}
