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
}
