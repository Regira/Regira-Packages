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
}
