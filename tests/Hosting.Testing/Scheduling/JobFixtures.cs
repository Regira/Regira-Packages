using System.Collections.Concurrent;
using Regira.System.Scheduling.Abstractions;

namespace Hosting.Testing.Scheduling;

public record ReportInput(int Id, string Name);

/// <summary>What a run left behind, for a test to wait on.</summary>
public record JobRun(string Handler, JobContext Context, object? Input = null, Guid Scope = default);

/// <summary>Collects the runs of the test handlers. One instance per test, registered as a singleton.</summary>
public class JobProbe
{
    private readonly ConcurrentQueue<JobRun> _runs = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposedScopes;

    public IReadOnlyCollection<JobRun> Runs => _runs.ToArray();
    public int DisposedScopes => _disposedScopes;
    /// <summary>The tokens the slow handler was cancelled through.</summary>
    public ConcurrentQueue<string> Cancelled { get; } = new();
    /// <summary>What a slow handler waits for.</summary>
    public Task Released => _release.Task;

    public void Add(JobRun run)
    {
        _runs.Enqueue(run);
        _signal.Release();
    }
    public void Release() => _release.TrySetResult();
    public void ScopeDisposed() => Interlocked.Increment(ref _disposedScopes);

    /// <summary>Waits until <paramref name="count"/> runs were recorded in total.</summary>
    public async Task<JobRun[]> WaitFor(int count, int seconds = 20)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
        while (_runs.Count < count)
        {
            try
            {
                await _signal.WaitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                Assert.Fail($"Expected {count} runs within {seconds}s, found {_runs.Count}");
            }
        }
        return _runs.ToArray();
    }
    public static async Task Until(Func<bool> condition, int seconds = 20)
    {
        var until = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                Assert.Fail($"The condition was not met within {seconds}s");
            }
            await Task.Delay(25);
        }
    }
}

public sealed class ScopedDependency(JobProbe probe) : IDisposable
{
    public Guid Id { get; } = Guid.NewGuid();
    public void Dispose() => probe.ScopeDisposed();
}

public class RecordJob(JobProbe probe, ScopedDependency scoped) : IJobHandler
{
    public Task Execute(JobContext context, CancellationToken token)
    {
        probe.Add(new JobRun(nameof(RecordJob), context, Scope: scoped.Id));
        return Task.CompletedTask;
    }
}
public class ReportJob(JobProbe probe) : IJobHandler<ReportInput>
{
    public Task Execute(ReportInput input, JobContext context, CancellationToken token)
    {
        probe.Add(new JobRun(nameof(ReportJob), context, input));
        return Task.CompletedTask;
    }
}
public class FailingJob(JobProbe probe) : IJobHandler
{
    public Task Execute(JobContext context, CancellationToken token)
    {
        probe.Add(new JobRun(nameof(FailingJob), context));
        throw new InvalidOperationException("failed on purpose");
    }
}
/// <summary>Records that it started, then runs until the probe releases it or its token is signalled.</summary>
public class SlowJob(JobProbe probe) : IJobHandler<string>
{
    public async Task Execute(string input, JobContext context, CancellationToken token)
    {
        probe.Add(new JobRun(nameof(SlowJob), context, input));
        try
        {
            await probe.Released.WaitAsync(token);
        }
        catch (OperationCanceledException)
        {
            probe.Cancelled.Enqueue(input);
            throw;
        }
    }
}
