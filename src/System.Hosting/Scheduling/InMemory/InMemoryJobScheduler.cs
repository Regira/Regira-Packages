using System.Text.Json;
using Cronos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Hosting.Scheduling.InMemory;

/// <summary>
/// The built-in scheduling backend: in process and in memory. Nothing is stored, a failed run is logged and not retried,
/// and a recurring job runs on every node that hosts the application.
/// </summary>
internal sealed class InMemoryJobScheduler(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    InMemorySchedulerOptions options,
    ILogger<InMemoryJobScheduler> logger)
    : IJobScheduler, IDisposable
{
    private delegate Task JobInvoker(IServiceProvider scopedServices, JobContext context, CancellationToken token);

    private sealed class Recurring(string id)
    {
        public string Id { get; } = id;
        public CronExpression Cron = null!;
        public RecurringJobOptions Options = null!;
        public JobInvoker Invoke = null!;
        public DateTimeOffset? Next;
        public int Running;
        // a run of this job is waiting to start
        public bool Waiting;
    }
    private sealed class Run(string id, DateTimeOffset dueAt, JobInvoker? invoke, Recurring? recurring, DateTimeOffset? scheduledAt)
    {
        public string Id { get; } = id;
        public DateTimeOffset DueAt { get; } = dueAt;
        public JobInvoker? Invoke { get; } = invoke;
        public Recurring? Recurring { get; } = recurring;
        public DateTimeOffset? ScheduledAt { get; } = scheduledAt;
        // a scheduled occurrence, which a misfire policy can skip; a triggered run is not
        public bool IsOccurrence { get; init; }
        public CancellationTokenSource? Cancellation;
        public Task? Task;
    }

    // a timer cannot be armed further ahead than this
    private static readonly TimeSpan MaxTimerDelay = TimeSpan.FromDays(1);

    private readonly object _gate = new();
    private readonly Dictionary<string, Recurring> _recurring = new(StringComparer.Ordinal);
    private readonly List<Run> _waiting = [];
    private readonly Dictionary<string, Run> _running = new(StringComparer.Ordinal);
    private readonly CancellationTokenSource _stopping = new();
    private ITimer? _timer;
    private bool _started;
    private bool _stopped;
    private bool _disposed;
    private TaskCompletionSource? _idle;


    public Task<string> Enqueue<TJob>(CancellationToken token = default)
        where TJob : IJobHandler
        => Task.FromResult(Accept(JobHandlerInvoker.Run<TJob>, time.GetUtcNow(), null));
    public Task<string> Enqueue<TJob, TInput>(TInput input, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
        => Task.FromResult(Accept(WithInput<TJob, TInput>(input), time.GetUtcNow(), null));
    public Task<string> Schedule<TJob, TInput>(TInput input, DateTimeOffset at, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
        => Task.FromResult(Accept(WithInput<TJob, TInput>(input), at, at));

    public Task<bool> Cancel(string jobId, CancellationToken token = default)
    {
        CancellationTokenSource? running = null;
        lock (_gate)
        {
            var waiting = _waiting.FindIndex(r => r.Id == jobId);
            if (waiting >= 0)
            {
                if (_waiting[waiting].Recurring is { } recurring)
                {
                    recurring.Waiting = false;
                }
                _waiting.RemoveAt(waiting);
                Pump();
                return Task.FromResult(true);
            }
            if (_running.TryGetValue(jobId, out var run))
            {
                running = run.Cancellation;
            }
        }

        if (running == null)
        {
            return Task.FromResult(false);
        }
        try
        {
            running.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // the run ended in between
            return Task.FromResult(false);
        }
        return Task.FromResult(true);
    }

    public Task AddOrUpdateRecurring<TJob>(string id, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler
    {
        SetRecurring(id, schedule, options, JobHandlerInvoker.Run<TJob>);
        return Task.CompletedTask;
    }
    public Task AddOrUpdateRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default)
        where TJob : IJobHandler<TInput>
    {
        SetRecurring(id, schedule, options, WithInput<TJob, TInput>(input));
        return Task.CompletedTask;
    }
    public Task<bool> RemoveRecurring(string id, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_recurring.Remove(id, out var recurring))
            {
                return Task.FromResult(false);
            }

            _waiting.RemoveAll(r => r.Recurring == recurring);
            recurring.Waiting = false;
            Pump();
            return Task.FromResult(true);
        }
    }
    public Task<bool> Trigger(string id, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (!_recurring.TryGetValue(id, out var recurring))
            {
                return Task.FromResult(false);
            }

            RefuseWhenStopped();
            if (!recurring.Waiting)
            {
                recurring.Waiting = true;
                _waiting.Add(new Run(NewId(), time.GetUtcNow(), null, recurring, null));
                Pump();
            }
            return Task.FromResult(true);
        }
    }


    internal void Start()
    {
        lock (_gate)
        {
            _started = true;
            Pump();
        }
    }
    /// <summary>Signals the token of every running job and waits for them to end, or for <paramref name="token"/>.</summary>
    internal async Task Stop(CancellationToken token)
    {
        Task[] running;
        lock (_gate)
        {
            if (_disposed)
            {
                // disposing already signalled every run
                return;
            }

            _started = false;
            _stopped = true;
            _timer?.Dispose();
            _timer = null;
            running = _running.Values.Select(r => r.Task).OfType<Task>().ToArray();
        }

        // signalling the runs is bounded by the token as well: a handler can block in a callback on its own token
        var signalled = _stopping.CancelAsync();
        try
        {
            await Task.WhenAll([signalled, .. running]).WaitAsync(token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            logger.LogWarning("{Count} jobs were still running when the scheduler stopped", running.Count(t => !t.IsCompleted));
        }
    }
    /// <summary>Completes when nothing is running and nothing that is due waits to start.</summary>
    internal Task WhenIdle()
    {
        lock (_gate)
        {
            if (IsIdle())
            {
                return Task.CompletedTask;
            }

            _idle ??= new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _idle.Task;
        }
    }

    public void Dispose()
    {
        // the container disposes it once per registration that returns it
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _started = false;
            _stopped = true;
            _timer?.Dispose();
            _timer = null;
        }
        _stopping.Cancel();
        _stopping.Dispose();
    }


    // The input is serialized when the job is accepted, as a persistent backend would:
    // an input that cannot be stored is refused here, not on the first deployment with another backend.
    private static JobInvoker WithInput<TJob, TInput>(TInput input)
        where TJob : IJobHandler<TInput>
    {
        var json = JsonSerializer.Serialize(input);
        return (scopedServices, context, token) => JobHandlerInvoker.Run<TJob, TInput>(scopedServices, JsonSerializer.Deserialize<TInput>(json)!, context, token);
    }
    private static string NewId() => Guid.NewGuid().ToString("N");
    // Before the start a job waits for it. After the stop nothing would ever run it: refusing it beats losing it in silence.
    private void RefuseWhenStopped()
    {
        if (_stopped)
        {
            throw new InvalidOperationException("The scheduler has stopped: the job is not accepted.");
        }
    }

    private string Accept(JobInvoker invoke, DateTimeOffset dueAt, DateTimeOffset? scheduledAt)
    {
        var run = new Run(NewId(), dueAt, invoke, null, scheduledAt);
        lock (_gate)
        {
            RefuseWhenStopped();
            _waiting.Add(run);
            Pump();
        }
        return run.Id;
    }
    private void SetRecurring(string id, CronSchedule schedule, RecurringJobOptions? recurringOptions, JobInvoker invoke)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("A recurring job needs an id.", nameof(id));
        }

        var cron = CronExpression.Parse(schedule.Expression, schedule.HasSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        recurringOptions ??= new RecurringJobOptions();
        lock (_gate)
        {
            // the same object is kept when a job is replaced, so a run of the previous definition still counts as this job's run:
            // also when the job was removed while that run was going
            if (!_recurring.TryGetValue(id, out var recurring))
            {
                _recurring[id] = recurring = _running.Values.Select(r => r.Recurring).FirstOrDefault(r => r?.Id == id) ?? new Recurring(id);
            }
            recurring.Cron = cron;
            recurring.Options = recurringOptions;
            recurring.Invoke = invoke;
            recurring.Next = cron.GetNextOccurrence(time.GetUtcNow(), recurringOptions.TimeZone);
            Pump();
        }
    }

    // Starts what is due and arms the timer for what comes next. Called under the lock, on every change and on every timer tick.
    private void Pump()
    {
        if (!_started)
        {
            return;
        }

        var now = time.GetUtcNow();
        foreach (var recurring in _recurring.Values)
        {
            if (recurring.Next is not { } occurrence || occurrence > now)
            {
                continue;
            }

            // from now on: the occurrences that were missed are one run or none, never one each
            recurring.Next = recurring.Cron.GetNextOccurrence(now, recurring.Options.TimeZone);
            if (IsMisfire(occurrence, now) && recurring.Options.Misfire == MisfirePolicy.Skip)
            {
                logger.LogInformation("Recurring job {RecurringId} missed its occurrence of {ScheduledAt} and skips it", recurring.Id, occurrence);
                continue;
            }
            if (recurring.Waiting)
            {
                continue;
            }
            if (recurring.Running > 0 && !recurring.Options.AllowConcurrent)
            {
                logger.LogInformation("Recurring job {RecurringId} is still running and skips its occurrence of {ScheduledAt}", recurring.Id, occurrence);
                continue;
            }

            recurring.Waiting = true;
            _waiting.Add(new Run(NewId(), occurrence, null, recurring, occurrence) { IsOccurrence = true });
        }

        foreach (var run in _waiting.Where(r => r.DueAt <= now).OrderBy(r => r.DueAt).ToArray())
        {
            if (_running.Count >= Math.Max(1, options.MaxConcurrency))
            {
                break;
            }
            if (run.Recurring is { Running: > 0, Options.AllowConcurrent: false })
            {
                // a triggered run waits for the previous one to end
                continue;
            }

            _waiting.Remove(run);
            if (run.Recurring is { } recurring)
            {
                recurring.Waiting = false;
                if (run.IsOccurrence && IsMisfire(run.DueAt, now) && recurring.Options.Misfire == MisfirePolicy.Skip)
                {
                    logger.LogInformation("Recurring job {RecurringId} could not start its occurrence of {ScheduledAt} in time and skips it", recurring.Id, run.DueAt);
                    continue;
                }
                recurring.Running++;
            }

            run.Cancellation = CancellationTokenSource.CreateLinkedTokenSource(_stopping.Token);
            _running[run.Id] = run;
            run.Task = WithoutCallerContext(() => Task.Run(() => Execute(run)));
        }

        var next = _recurring.Values.Select(r => r.Next)
            .Concat(_waiting.Where(r => r.DueAt > now).Select(r => (DateTimeOffset?)r.DueAt))
            .Min();
        if (next is { } due)
        {
            var delay = due - now < MaxTimerDelay ? due - now : MaxTimerDelay;
            if (_timer == null)
            {
                _timer = WithoutCallerContext(() => time.CreateTimer(_ => OnTimer(), null, delay, Timeout.InfiniteTimeSpan));
            }
            else
            {
                _timer.Change(delay, Timeout.InfiniteTimeSpan);
            }
        }

        if (_idle != null && IsIdle())
        {
            _idle.TrySetResult();
            _idle = null;
        }
    }
    // A run and the timer start without the ExecutionContext of whoever pumped: a job enqueued from a request does not see
    // that request's AsyncLocal values (the current activity, an ambient transaction, the culture), and neither does a later tick.
    private static T WithoutCallerContext<T>(Func<T> start)
    {
        var flow = ExecutionContext.IsFlowSuppressed() ? (AsyncFlowControl?)null : ExecutionContext.SuppressFlow();
        try
        {
            return start();
        }
        finally
        {
            flow?.Undo();
        }
    }
    private void OnTimer()
    {
        lock (_gate)
        {
            Pump();
        }
    }
    private bool IsMisfire(DateTimeOffset occurrence, DateTimeOffset now) => now - occurrence > options.MisfireThreshold;
    private bool IsIdle()
    {
        var now = time.GetUtcNow();
        return _running.Count == 0 && !_waiting.Any(r => r.DueAt <= now);
    }

    private async Task Execute(Run run)
    {
        var context = new JobContext(run.Id, run.Recurring?.Id, run.ScheduledAt);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var invoke = run.Invoke ?? run.Recurring!.Invoke;
            await invoke(scope.ServiceProvider, context, run.Cancellation!.Token);
        }
        catch (OperationCanceledException) when (run.Cancellation!.IsCancellationRequested)
        {
            logger.LogInformation("Job {JobId} was cancelled", run.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Job {JobId} failed (recurring job: {RecurringId})", run.Id, run.Recurring?.Id);
        }
        finally
        {
            lock (_gate)
            {
                _running.Remove(run.Id);
                if (run.Recurring != null)
                {
                    run.Recurring.Running--;
                }
                run.Cancellation!.Dispose();
                Pump();
            }
        }
    }
}
