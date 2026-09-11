namespace Regira.Entities.EFcore.Utilities;

/// <summary>
/// Waits on asynchronous work from a synchronous caller — the <c>SavingChanges</c> hook of the primer and normalizer
/// interceptors, whose contracts (<c>PrepareManyAsync</c>, <c>HandleNormalizeMany</c>) are asynchronous. This is the one
/// place a blocking wait is deliberate, so it guards both ways such a wait deadlocks:
/// <list type="bullet">
///   <item>a single-threaded <see cref="SynchronizationContext"/> (a UI thread, a Blazor circuit, a test runner's async
///     context): an <c>await</c> inside the work posts its continuation there, and the thread that would run it is the
///     one blocked here. The work is started with no context, so its continuations run on the thread pool; the context
///     is restored before blocking.</item>
///   <item>a non-default <see cref="TaskScheduler"/> (<see cref="ConcurrentExclusiveSchedulerPair"/>, an actor runtime):
///     without a context an <c>await</c> falls back to <see cref="TaskScheduler.Current"/>, whose only lane is the blocked
///     caller. There the work is started on the default scheduler instead.</item>
/// </list>
/// Work that completes synchronously — every built-in primer and normalizer — runs inline on the caller's thread and
/// costs nothing. Work that awaits I/O holds the calling thread for that I/O, as the synchronous save already does for
/// its own round trip, and an ambient <c>TransactionScope</c> created without <c>TransactionScopeAsyncFlowOption.Enabled</c>
/// does not reach the code after its first real <c>await</c>. <c>GetAwaiter().GetResult()</c> rethrows the original
/// exception rather than an <see cref="AggregateException"/>, so a caller's <c>catch</c> keeps matching.
/// </summary>
internal static class SyncOverAsync
{
    public static void Wait(Func<Task> work)
    {
        if (TaskScheduler.Current != TaskScheduler.Default)
        {
            Task.Run(work).GetAwaiter().GetResult();
            return;
        }

        var previous = SynchronizationContext.Current;
        Task task;
        try
        {
            SynchronizationContext.SetSynchronizationContext(null);
            task = work();
        }
        finally
        {
            // every await inside the work has captured the empty context by now — restore before blocking
            SynchronizationContext.SetSynchronizationContext(previous);
        }
        task.GetAwaiter().GetResult();
    }
}
