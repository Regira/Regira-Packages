using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Regira.System.Hosting.Scheduling;
using Regira.System.Hosting.Scheduling.DependencyInjection;
using Regira.System.Hosting.Scheduling.InMemory;
using Regira.System.Scheduling.Abstractions;

namespace Hosting.Testing.Scheduling;

/// <summary>The built-in backend on a fake clock: a test advances time instead of waiting for it.</summary>
[TestFixture]
public class InMemoryJobSchedulerTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    private JobProbe _probe = null!;
    private FakeTimeProvider _time = null!;
    private ServiceProvider? _services;

    [SetUp]
    public void SetUp()
    {
        _probe = new JobProbe();
        // a Saturday, in winter time
        _time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
    }
    [TearDown]
    public async Task TearDown()
    {
        _probe.Release();
        if (_services != null)
        {
            await _services.GetRequiredService<InMemoryJobScheduler>().Stop(CancellationToken.None);
            await _services.DisposeAsync();
            _services = null;
        }
    }

    private InMemoryJobScheduler Start(Action<SchedulingBuilder>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_probe);
        services.AddSingleton<TimeProvider>(_time);
        services.AddScoped<ScopedDependency>();
        services.AddScheduling(configure);
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var scheduler = _services.GetRequiredService<InMemoryJobScheduler>();
        scheduler.Start();
        return scheduler;
    }
    private async Task Advance(InMemoryJobScheduler scheduler, TimeSpan by)
    {
        _time.Advance(by);
        await scheduler.WhenIdle();
    }

    private static readonly AsyncLocal<string?> Ambient = new();
    private class AmbientJob(JobProbe probe) : IJobHandler
    {
        public Task Execute(JobContext context, CancellationToken token)
        {
            probe.Add(new JobRun(nameof(AmbientJob), context, Ambient.Value));
            return Task.CompletedTask;
        }
    }

    // A job enqueued from a request must not see that request's ambient state.
    [Test]
    public async Task A_Job_Does_Not_Run_In_The_Context_Of_Its_Caller()
    {
        var scheduler = Start();
        Ambient.Value = "the caller";

        await scheduler.Enqueue<AmbientJob>();
        await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(1, "later"), _time.GetUtcNow().AddMinutes(1));
        await scheduler.AddOrUpdateRecurring<AmbientJob>("ambient", "*/5 * * * *");
        await scheduler.WhenIdle();
        await Advance(scheduler, TimeSpan.FromMinutes(5));

        var runs = _probe.Runs.Where(r => r.Handler == nameof(AmbientJob)).ToArray();
        Assert.That(runs, Has.Length.EqualTo(2));
        Assert.That(runs.Select(r => r.Input), Is.All.Null);
        Assert.That(Ambient.Value, Is.EqualTo("the caller"));
    }

    [Test]
    public async Task A_Recurring_Job_Fires_On_Its_Occurrences_In_Its_Time_Zone()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("daily", "0 7 * * *", new RecurringJobOptions { TimeZone = Brussels });

        // 07:00 in Brussels is 06:00 UTC in winter
        await Advance(scheduler, TimeSpan.FromHours(6) - TimeSpan.FromSeconds(1));
        Assert.That(_probe.Runs, Is.Empty);
        await Advance(scheduler, TimeSpan.FromSeconds(1));
        var first = _probe.Runs.Single();
        await Advance(scheduler, TimeSpan.FromHours(24));

        Assert.That(first.Context.RecurringId, Is.EqualTo("daily"));
        Assert.That(first.Context.ScheduledAt, Is.EqualTo(new DateTimeOffset(2026, 1, 10, 6, 0, 0, TimeSpan.Zero)));
        Assert.That(_probe.Runs.Select(r => r.Context.ScheduledAt), Is.EqualTo(new DateTimeOffset?[]
        {
            new DateTimeOffset(2026, 1, 10, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 11, 6, 0, 0, TimeSpan.Zero)
        }));
    }

    [Test]
    public async Task A_Recurring_Job_Follows_A_Daylight_Saving_Change()
    {
        // summer time starts in Brussels on 2026-03-29 at 02:00
        _time.SetUtcNow(new DateTimeOffset(2026, 3, 28, 0, 0, 0, TimeSpan.Zero));
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("daily", "0 7 * * *", new RecurringJobOptions { TimeZone = Brussels });

        await Advance(scheduler, TimeSpan.FromHours(6));
        await Advance(scheduler, TimeSpan.FromHours(23));

        Assert.That(_probe.Runs.Select(r => r.Context.ScheduledAt), Is.EqualTo(new DateTimeOffset?[]
        {
            new DateTimeOffset(2026, 3, 28, 6, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 3, 29, 5, 0, 0, TimeSpan.Zero)
        }));
    }

    [Test]
    public async Task A_Schedule_With_Seconds_Fires_On_The_Second()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("often", "*/20 * * * * *");

        await Advance(scheduler, TimeSpan.FromSeconds(20));
        await Advance(scheduler, TimeSpan.FromSeconds(20));
        await Advance(scheduler, TimeSpan.FromSeconds(19));

        Assert.That(_probe.Runs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task A_Declared_Recurring_Job_Is_Applied_When_The_Host_Starts()
    {
        var scheduler = Start(s => s.AddRecurring<ReportJob, ReportInput>("report", new ReportInput(1, "declared"), "*/5 * * * *"));
        foreach (var hosted in _services!.GetServices<Microsoft.Extensions.Hosting.IHostedService>())
        {
            await hosted.StartAsync(CancellationToken.None);
        }

        await Advance(scheduler, TimeSpan.FromMinutes(5));

        Assert.That(_probe.Runs.Single().Input, Is.EqualTo(new ReportInput(1, "declared")));
    }

    [Test]
    public async Task A_Scheduled_Job_Fires_Once()
    {
        var scheduler = Start();
        var at = _time.GetUtcNow().AddMinutes(10);

        var jobId = await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(4, "once"), at);
        await Advance(scheduler, TimeSpan.FromMinutes(10) - TimeSpan.FromSeconds(1));
        Assert.That(_probe.Runs, Is.Empty);
        await Advance(scheduler, TimeSpan.FromSeconds(1));
        await Advance(scheduler, TimeSpan.FromDays(2));

        var run = _probe.Runs.Single();
        Assert.That(run.Context, Is.EqualTo(new JobContext(jobId, null, at)));
        Assert.That(run.Input, Is.EqualTo(new ReportInput(4, "once")));
    }

    [Test]
    public async Task A_Job_Accepted_Before_The_Start_Runs_After_It()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_probe);
        services.AddSingleton<TimeProvider>(_time);
        services.AddScoped<ScopedDependency>();
        services.AddScheduling();
        _services = services.BuildServiceProvider();
        var scheduler = _services.GetRequiredService<InMemoryJobScheduler>();

        await scheduler.Enqueue<RecordJob>();
        await Task.Delay(100);
        Assert.That(_probe.Runs, Is.Empty);
        scheduler.Start();
        await scheduler.WhenIdle();

        Assert.That(_probe.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Cancel_Removes_A_Job_That_Waits()
    {
        var scheduler = Start();
        var jobId = await scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(4, "never"), _time.GetUtcNow().AddMinutes(10));

        var cancelled = await scheduler.Cancel(jobId);
        await Advance(scheduler, TimeSpan.FromMinutes(11));

        Assert.That(cancelled, Is.True);
        Assert.That(_probe.Runs, Is.Empty);
        Assert.That(await scheduler.Cancel(jobId), Is.False);
    }

    [Test]
    public async Task Cancel_Signals_The_Token_Of_A_Running_Job()
    {
        var scheduler = Start();
        var jobId = await scheduler.Enqueue<SlowJob, string>("running");
        await _probe.WaitFor(1);

        var cancelled = await scheduler.Cancel(jobId);
        await scheduler.WhenIdle();

        Assert.That(cancelled, Is.True);
        Assert.That(_probe.Cancelled, Is.EqualTo(new[] { "running" }));
    }

    [Test]
    public async Task RemoveRecurring_Stops_The_Occurrences()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("minutely", "* * * * *");
        await Advance(scheduler, TimeSpan.FromMinutes(1));

        var removed = await scheduler.RemoveRecurring("minutely");
        await Advance(scheduler, TimeSpan.FromMinutes(5));

        Assert.That(removed, Is.True);
        Assert.That(_probe.Runs, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Trigger_Runs_A_Recurring_Job_Outside_Its_Schedule()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("yearly", "0 0 1 1 *");

        await scheduler.Trigger("yearly");
        await scheduler.WhenIdle();

        var run = _probe.Runs.Single();
        Assert.That(run.Context.RecurringId, Is.EqualTo("yearly"));
        Assert.That(run.Context.ScheduledAt, Is.Null);
    }

    [Test]
    public async Task An_Occurrence_Is_Skipped_While_The_Previous_Run_Is_Still_Going()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<SlowJob, string>("slow", "exclusive", "* * * * *");

        _time.Advance(TimeSpan.FromMinutes(1));
        await _probe.WaitFor(1);
        _time.Advance(TimeSpan.FromMinutes(1));
        _time.Advance(TimeSpan.FromMinutes(1));
        _probe.Release();
        await scheduler.WhenIdle();
        Assert.That(_probe.Runs, Has.Count.EqualTo(1));

        await Advance(scheduler, TimeSpan.FromMinutes(1));
        Assert.That(_probe.Runs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Occurrences_Overlap_With_AllowConcurrent()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<SlowJob, string>("slow", "concurrent", "* * * * *", new RecurringJobOptions { AllowConcurrent = true });

        _time.Advance(TimeSpan.FromMinutes(1));
        await _probe.WaitFor(1);
        _time.Advance(TimeSpan.FromMinutes(1));

        await _probe.WaitFor(2);
    }

    [Test]
    public async Task A_Handler_That_Throws_Does_Not_Stop_The_Scheduler()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<FailingJob>("failing", "* * * * *");

        await Advance(scheduler, TimeSpan.FromMinutes(1));
        await Advance(scheduler, TimeSpan.FromMinutes(1));

        Assert.That(_probe.Runs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Stop_Signals_The_Token_Of_A_Running_Job()
    {
        var scheduler = Start();
        await scheduler.Enqueue<SlowJob, string>("stopping");
        await _probe.WaitFor(1);

        await scheduler.Stop(CancellationToken.None);

        Assert.That(_probe.Cancelled, Is.EqualTo(new[] { "stopping" }));
    }

    private class StubbornJob(JobProbe probe) : IJobHandler
    {
        public async Task Execute(JobContext context, CancellationToken token)
        {
            probe.Add(new JobRun(nameof(StubbornJob), context));
            // ignores its token
            await probe.Released;
        }
    }

    [Test]
    public async Task Stop_Returns_When_Its_Token_Expires_Before_A_Job_Ends()
    {
        var scheduler = Start();
        await scheduler.Enqueue<StubbornJob>();
        await _probe.WaitFor(1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await scheduler.Stop(timeout.Token);

        Assert.That(timeout.IsCancellationRequested, Is.True);
        _probe.Release();
    }

    private class BlockingCallbackJob(JobProbe probe) : IJobHandler
    {
        public async Task Execute(JobContext context, CancellationToken token)
        {
            // a callback on the token that does not return
            await using var registration = token.Register(() => probe.Released.Wait());
            probe.Add(new JobRun(nameof(BlockingCallbackJob), context));
            await probe.Released;
        }
    }

    [Test]
    public async Task Stop_Returns_When_Its_Token_Expires_While_A_Cancellation_Callback_Blocks()
    {
        var scheduler = Start();
        await scheduler.Enqueue<BlockingCallbackJob>();
        await _probe.WaitFor(1);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await scheduler.Stop(timeout.Token).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(timeout.IsCancellationRequested, Is.True);
        _probe.Release();
    }

    // The run of a removed job still counts as that job's run when the id is registered again.
    [Test]
    public async Task A_Job_Registered_Again_While_Its_Run_Is_Going_Does_Not_Overlap_It()
    {
        var scheduler = Start(s => s.InMemory.MaxConcurrency = 4);
        await scheduler.AddOrUpdateRecurring<SlowJob, string>("job", "first", "0 0 1 1 *");
        await scheduler.Trigger("job");
        await _probe.WaitFor(1);

        await scheduler.RemoveRecurring("job");
        await scheduler.AddOrUpdateRecurring<SlowJob, string>("job", "second", "0 0 1 1 *");
        await scheduler.Trigger("job");
        await Task.Delay(100);
        Assert.That(_probe.Runs, Has.Count.EqualTo(1));
        _probe.Release();
        await scheduler.WhenIdle();

        Assert.That(_probe.Runs.Select(r => r.Input), Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public async Task Dispose_Signals_The_Token_Of_A_Running_Job()
    {
        var scheduler = Start();
        await scheduler.Enqueue<SlowJob, string>("disposing");
        await _probe.WaitFor(1);

        scheduler.Dispose();

        await JobProbe.Until(() => _probe.Cancelled.Contains("disposing"));
    }

    // Nothing would run it any more: it is refused instead of lost.
    [Test]
    public async Task A_Job_Is_Refused_After_The_Stop()
    {
        var scheduler = Start();
        await scheduler.AddOrUpdateRecurring<RecordJob>("yearly", "0 0 1 1 *");
        await scheduler.Stop(CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.Enqueue<RecordJob>());
        await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.Schedule<ReportJob, ReportInput>(new ReportInput(1, "late"), _time.GetUtcNow()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => scheduler.Trigger("yearly"));
        Assert.That(_probe.Runs, Is.Empty);
    }

    // The scheduler finds an occurrence far in the past when it starts late, or when the clock jumps.
    [TestCase(MisfirePolicy.RunOnce, 1)]
    [TestCase(MisfirePolicy.Skip, 0)]
    public async Task An_Occurrence_Found_Late_Runs_Once_Or_Is_Skipped(MisfirePolicy policy, int expectedRuns)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_probe);
        services.AddSingleton<TimeProvider>(_time);
        services.AddScoped<ScopedDependency>();
        services.AddScheduling();
        _services = services.BuildServiceProvider();
        var scheduler = _services.GetRequiredService<InMemoryJobScheduler>();
        await scheduler.AddOrUpdateRecurring<RecordJob>("minutely", "* * * * *", new RecurringJobOptions { Misfire = policy });
        var firstOccurrence = _time.GetUtcNow().AddMinutes(1);

        // ten occurrences pass before the scheduler looks
        _time.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(30));
        scheduler.Start();
        await scheduler.WhenIdle();

        Assert.That(_probe.Runs, Has.Count.EqualTo(expectedRuns));
        if (expectedRuns == 1)
        {
            Assert.That(_probe.Runs.Single().Context.ScheduledAt, Is.EqualTo(firstOccurrence));
        }
        // the next occurrence is on time
        await Advance(scheduler, TimeSpan.FromSeconds(30));
        Assert.That(_probe.Runs, Has.Count.EqualTo(expectedRuns + 1));
    }

    [Test]
    public async Task MaxConcurrency_Holds_Back_What_Is_Due()
    {
        var scheduler = Start(s => s.InMemory.MaxConcurrency = 1);

        await scheduler.Enqueue<SlowJob, string>("first");
        await scheduler.Enqueue<SlowJob, string>("second");
        await _probe.WaitFor(1);
        await Task.Delay(100);
        Assert.That(_probe.Runs, Has.Count.EqualTo(1));
        _probe.Release();
        await scheduler.WhenIdle();

        Assert.That(_probe.Runs.Select(r => r.Input), Is.EqualTo(new[] { "first", "second" }));
    }

    // An occurrence only goes missing here when it was held back: several of them become one run, or none.
    [TestCase(MisfirePolicy.RunOnce, 1)]
    [TestCase(MisfirePolicy.Skip, 0)]
    public async Task A_Missed_Occurrence_Runs_Once_Or_Is_Skipped(MisfirePolicy policy, int expectedRuns)
    {
        var scheduler = Start(s => s.InMemory.MaxConcurrency = 1);
        await scheduler.Enqueue<SlowJob, string>("in-the-way");
        await _probe.WaitFor(1);
        await scheduler.AddOrUpdateRecurring<RecordJob>("minutely", "* * * * *", new RecurringJobOptions { Misfire = policy });
        var firstOccurrence = _time.GetUtcNow().AddMinutes(1);

        // five occurrences pass while the only place is taken
        for (var i = 0; i < 5; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
        }
        _time.Advance(TimeSpan.FromSeconds(30));
        _probe.Release();
        await scheduler.WhenIdle();

        var runs = _probe.Runs.Where(r => r.Handler == nameof(RecordJob)).ToArray();
        Assert.That(runs, Has.Length.EqualTo(expectedRuns));
        if (expectedRuns == 1)
        {
            Assert.That(runs[0].Context.ScheduledAt, Is.EqualTo(firstOccurrence));
        }
    }

    [Test]
    public async Task An_Input_That_Cannot_Be_Serialized_Is_Refused_When_It_Is_Accepted()
    {
        var scheduler = Start();
        var node = new Node();
        node.Next = node;

        await Assert.ThrowsAsync<global::System.Text.Json.JsonException>(() => scheduler.Enqueue<NodeJob, Node>(node));
    }

    [Test]
    public void A_Recurring_Id_Is_Declared_Once()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddScheduling(s =>
        {
            s.AddRecurring<RecordJob>("same", "* * * * *");
            s.AddRecurring<RecordJob>("same", "*/5 * * * *");
        }));
        Assert.Throws<FormatException>(() => services.AddScheduling(s => s.AddRecurring<RecordJob>("macro", "@daily")));
    }

    public class Node
    {
        public Node? Next { get; set; }
    }
    public class NodeJob : IJobHandler<Node>
    {
        public Task Execute(Node input, JobContext context, CancellationToken token) => Task.CompletedTask;
    }
}
