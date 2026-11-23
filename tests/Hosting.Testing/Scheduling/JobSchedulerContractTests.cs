using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.System.Hosting.Scheduling;
using Regira.System.Scheduling.Abstractions;

namespace Hosting.Testing.Scheduling;

/// <summary>
/// The portable contract, stated as tests against an <see cref="IJobScheduler"/>: what a job can rely on whichever backend runs it.
/// Each backend is a subclass.
/// </summary>
public abstract class JobSchedulerContractTests
{
    // far away: a test triggers the job instead of waiting for it
    protected const string Yearly = "0 0 1 1 *";

    protected JobProbe Probe = null!;
    private IHost? _host;

    /// <summary>Registers scheduling with the backend under test, passing <paramref name="declare"/> to <c>AddScheduling</c>.</summary>
    protected abstract void AddScheduling(IServiceCollection services, Action<SchedulingBuilder> declare);

    [SetUp]
    public void SetUp() => Probe = new JobProbe();
    [TearDown]
    public async Task TearDown()
    {
        Probe.Release();
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
            _host = null;
        }
    }

    /// <summary>The services of the started host.</summary>
    protected IServiceProvider Services => _host!.Services;

    protected async Task<IJobScheduler> Start(Action<SchedulingBuilder>? declare = null)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(Probe);
        builder.Services.AddScoped<ScopedDependency>();
        AddScheduling(builder.Services, declare ?? (_ => { }));
        _host = builder.Build();
        await _host.StartAsync();
        return _host.Services.GetRequiredService<IJobScheduler>();
    }

    [Test]
    public async Task Enqueue_Runs_The_Handler()
    {
        var scheduler = await Start();

        var jobId = await scheduler.Enqueue<RecordJob>();
        var run = (await Probe.WaitFor(1)).Single();

        Assert.That(jobId, Is.Not.Empty);
        Assert.That(run.Context.JobId, Is.EqualTo(jobId));
        Assert.That(run.Context.RecurringId, Is.Null);
    }

    [Test]
    public async Task The_Input_Round_Trips()
    {
        var scheduler = await Start();

        var jobId = await scheduler.Enqueue<ReportJob, ReportInput>(new ReportInput(7, "weekly"));
        var run = (await Probe.WaitFor(1)).Single();

        Assert.That(run.Input, Is.EqualTo(new ReportInput(7, "weekly")));
        Assert.That(run.Context.JobId, Is.EqualTo(jobId));
    }

    [Test]
    public async Task Every_Run_Has_A_Scope_Of_Its_Own()
    {
        var scheduler = await Start();

        await scheduler.Enqueue<RecordJob>();
        await scheduler.Enqueue<RecordJob>();
        var runs = await Probe.WaitFor(2);

        Assert.That(runs[0].Scope, Is.Not.EqualTo(runs[1].Scope));
        await JobProbe.Until(() => Probe.DisposedScopes == 2);
    }

    [Test]
    public async Task A_Scheduled_Job_Runs_Once_At_Its_Time()
    {
        var scheduler = await Start();
        var at = DateTimeOffset.UtcNow.AddSeconds(3);

        var jobId = await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(1, "later"), at);
        var run = (await Probe.WaitFor(1)).Single();

        // a backend may keep the time to the second
        Assert.That(DateTimeOffset.UtcNow, Is.GreaterThanOrEqualTo(at.AddSeconds(-1.1)));
        Assert.That(run.Context.JobId, Is.EqualTo(jobId));
        Assert.That(run.Context.RecurringId, Is.Null);
        Assert.That(run.Input, Is.EqualTo(new ReportInput(1, "later")));
        Assert.That(run.Context.ScheduledAt, Is.EqualTo(at).Within(TimeSpan.FromSeconds(1)));
        await Task.Delay(500);
        Assert.That(Probe.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_Cancelled_Job_Does_Not_Run()
    {
        var scheduler = await Start();
        var jobId = await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(1, "never"), DateTimeOffset.UtcNow.AddSeconds(2));

        var cancelled = await scheduler.Cancel(jobId);
        var again = await scheduler.Cancel(jobId);
        var unknown = await scheduler.Cancel("no-such-job");

        Assert.That(cancelled, Is.True);
        Assert.That(again, Is.False);
        Assert.That(unknown, Is.False);
        await Task.Delay(3000);
        Assert.That(Probe.Runs, Is.Empty);
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task A_Recurring_Job_Needs_An_Id(string id)
    {
        var scheduler = await Start();

        await Assert.ThrowsAsync<ArgumentException>(() => scheduler.AddOrUpdateRecurring<RecordJob>(id, Yearly));
        await Assert.ThrowsAsync<ArgumentException>(() => scheduler.AddOrUpdateRecurring<ReportJob, ReportInput>(id, new ReportInput(1, "a"), Yearly));
    }

    [Test]
    public async Task Cancel_Is_False_For_A_Job_That_Ran()
    {
        var scheduler = await Start();
        var jobId = await scheduler.Enqueue<RecordJob>();
        await Probe.WaitFor(1);
        await JobProbe.Until(() => Probe.DisposedScopes == 1);
        await Task.Delay(250);

        Assert.That(await scheduler.Cancel(jobId), Is.False);
    }

    [Test]
    public async Task Cancel_Signals_The_Token_Of_A_Running_Job()
    {
        var scheduler = await Start();
        var jobId = await scheduler.Enqueue<SlowJob, string>("cancelled");
        await Probe.WaitFor(1);

        var cancelled = await scheduler.Cancel(jobId);

        Assert.That(cancelled, Is.True);
        await JobProbe.Until(() => Probe.Cancelled.Contains("cancelled"));
    }

    [Test]
    public async Task A_Declared_Recurring_Job_Is_Triggered_By_Its_Id()
    {
        var scheduler = await Start(s => s.AddRecurring<RecordJob>("yearly-record", Yearly));

        var triggered = await scheduler.Trigger("yearly-record");
        var unknown = await scheduler.Trigger("no-such-job");
        var run = (await Probe.WaitFor(1)).Single();

        Assert.That(triggered, Is.True);
        Assert.That(unknown, Is.False);
        Assert.That(run.Context.RecurringId, Is.EqualTo("yearly-record"));
        Assert.That(run.Context.JobId, Is.Not.Empty);
    }

    [Test]
    public async Task A_Recurring_Job_Fires_On_Its_Schedule()
    {
        await Start(s => s.AddRecurring<ReportJob, ReportInput>("every-second", new ReportInput(3, "tick"), "* * * * * *"));

        var runs = await Probe.WaitFor(2);

        Assert.That(runs.Select(r => r.Context.RecurringId), Is.All.EqualTo("every-second"));
        Assert.That(runs.Select(r => r.Input), Is.All.EqualTo(new ReportInput(3, "tick")));
        Assert.That(runs.Select(r => r.Context.JobId).Distinct().Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task A_Recurring_Job_Is_Replaced_Under_The_Same_Id()
    {
        var scheduler = await Start();
        await scheduler.AddOrUpdateRecurring<ReportJob, ReportInput>("report", new ReportInput(1, "first"), Yearly);
        await scheduler.AddOrUpdateRecurring<ReportJob, ReportInput>("report", new ReportInput(2, "second"), Yearly,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"), Misfire = MisfirePolicy.Skip });

        await scheduler.Trigger("report");
        var run = (await Probe.WaitFor(1)).Single();

        Assert.That(run.Input, Is.EqualTo(new ReportInput(2, "second")));
        await Task.Delay(500);
        Assert.That(Probe.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task A_Removed_Recurring_Job_Is_Gone()
    {
        var scheduler = await Start(s => s.AddRecurring<RecordJob>("yearly-record", Yearly));

        var removed = await scheduler.RemoveRecurring("yearly-record");
        var again = await scheduler.RemoveRecurring("yearly-record");
        var triggered = await scheduler.Trigger("yearly-record");

        Assert.That(removed, Is.True);
        Assert.That(again, Is.False);
        Assert.That(triggered, Is.False);
    }

    [Test]
    public async Task A_Handler_That_Throws_Does_Not_Stop_The_Scheduler()
    {
        var scheduler = await Start();

        await scheduler.Enqueue<FailingJob>();
        await Probe.WaitFor(1);
        await scheduler.Enqueue<RecordJob>();

        await JobProbe.Until(() => Probe.Runs.Any(r => r.Handler == nameof(RecordJob)));
    }

    [Test]
    public async Task A_Recurring_Job_Does_Not_Overlap_Itself()
    {
        var scheduler = await Start(s => s.AddRecurring<SlowJob, string>("slow", "exclusive", Yearly));

        await scheduler.Trigger("slow");
        await Probe.WaitFor(1);
        await scheduler.Trigger("slow");
        await Task.Delay(1500);

        Assert.That(Probe.Runs, Has.Count.EqualTo(1), "a second run started while the first was still going");
    }

    [Test]
    public async Task A_Recurring_Job_Overlaps_When_Asked_To()
    {
        var scheduler = await Start(s => s.AddRecurring<SlowJob, string>("slow", "concurrent", Yearly, o => o.AllowConcurrent = true));

        await scheduler.Trigger("slow");
        await Probe.WaitFor(1);
        await scheduler.Trigger("slow");

        await Probe.WaitFor(2);
    }

    [Test]
    public async Task Shutdown_Signals_The_Token()
    {
        var scheduler = await Start();
        await scheduler.Enqueue<SlowJob, string>("shutdown");
        await Probe.WaitFor(1);

        await _host!.StopAsync();

        await JobProbe.Until(() => Probe.Cancelled.Contains("shutdown"));
    }
}

[TestFixture]
public class InMemoryJobSchedulerContractTests : JobSchedulerContractTests
{
    protected override void AddScheduling(IServiceCollection services, Action<SchedulingBuilder> declare)
        => Regira.System.Hosting.Scheduling.DependencyInjection.ServiceCollectionExtensions.AddScheduling(services, declare);
}
