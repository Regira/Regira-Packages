using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Regira.System.Hosting.Scheduling;
using Regira.System.Hosting.Scheduling.DependencyInjection;
using Regira.System.Scheduling.Hangfire;

namespace Hosting.Testing.Scheduling;

/// <summary>The portable contract on Hangfire, with its in-memory storage.</summary>
[TestFixture]
public class HangfireJobSchedulerContractTests : JobSchedulerContractTests
{
    protected override void AddScheduling(IServiceCollection services, Action<SchedulingBuilder> declare)
    {
        services.AddHangfire(c => c
            .UseInMemoryStorage()
            // Every test starts a storage of its own. From the second one in a process on, Hangfire keeps re-applying the retry of
            // a failed job and its server takes its whole shutdown timeout to stop, with or without this adapter. No retry, no loop.
            .UseFilter(new AutomaticRetryAttribute { Attempts = 0 }));
        services.AddHangfireServer(o =>
        {
            // the defaults poll every 15 and every 5 seconds
            o.SchedulePollingInterval = TimeSpan.FromMilliseconds(500);
            o.CancellationCheckInterval = TimeSpan.FromMilliseconds(500);
        });
        services.AddScheduling(declare).UseHangfire();
    }
    protected override Task WhenSettled(string jobId)
        => JobProbe.Until(() =>
        {
            using var connection = Services.GetRequiredService<JobStorage>().GetConnection();
            return connection.GetStateData(jobId)?.Name == "Succeeded";
        });

    // The storage is the application's: a job it gave Hangfire itself is not the scheduler's to cancel.
    [Test]
    public async Task Cancel_Leaves_The_Application_Its_Own_Jobs()
    {
        var scheduler = await Start();
        var ownId = Services.GetRequiredService<IBackgroundJobClient>().Schedule(() => Console.WriteLine("the application's own job"), TimeSpan.FromHours(1));
        var jobId = await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(1, "later"), DateTimeOffset.UtcNow.AddHours(1));

        Assert.That(await scheduler.Cancel(ownId), Is.False);
        Assert.That(await scheduler.Cancel("no-such-job"), Is.False);
        Assert.That(await scheduler.Cancel(jobId), Is.True);
        using var connection = Services.GetRequiredService<JobStorage>().GetConnection();
        Assert.That(connection.GetStateData(ownId).Name, Is.EqualTo("Scheduled"));
        Assert.That(connection.GetStateData(jobId).Name, Is.EqualTo("Deleted"));
    }
}
