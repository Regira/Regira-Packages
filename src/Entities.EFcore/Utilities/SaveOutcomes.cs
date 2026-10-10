using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Regira.Entities.EFcore.Utilities;

/// <summary>
/// Work a primer leaves to the end of the save it primes: undone when the save fails and finished when it succeeds. A
/// save that failed in the database tells the work whether its statements may stand: EF undoes a failed save in the
/// transaction it begins itself, and in a caller's transaction where it made a savepoint first; under an ambient or
/// enlisted transaction, in a caller's transaction without a savepoint, or with <c>AutoTransactionBehavior.Never</c>,
/// the statements before the failing one stand, for the caller to commit. EF reports a failure only from inside its own
/// save, so the failures before it are reported here too:
/// <list type="bullet">
///   <item>the primer interceptor and the reactor interceptor report a failure of their own <c>SavingChanges</c> pass,
///     and the end of every save, asynchronously on an asynchronous save;</item>
///   <item>work left by a save that failed in another interceptor's <c>SavingChanges</c>, which nothing reports, is undone
///     when the next primer pass on that context begins;</item>
///   <item>on a context primed by an explicit <c>ApplyPrimers()</c>, without the primer interceptor, the context's own
///     save events report the end of its save.</item>
/// </list>
/// The work never throws to the save: the save's own outcome is what the caller has to see.
/// </summary>
internal static class SaveOutcomes
{
    private sealed record Work(Func<bool, Task>? OnFailed, Func<Task>? OnSaved, bool InSave);

    private sealed class State
    {
        public readonly List<Work> Pending = [];
        public bool InSavePass;
        public EventHandler<SaveChangesFailedEventArgs>? FailedHandler;
        public EventHandler<SavedChangesEventArgs>? SavedHandler;
    }

    private static readonly ConditionalWeakTable<DbContext, State> States = new();

    /// <summary>
    /// Leaves <paramref name="onFailed"/> and <paramref name="onSaved"/> to the end of the save being primed.
    /// <paramref name="onFailed"/> is told whether the statements of the failed save may stand.
    /// </summary>
    public static void Register(DbContext context, Func<bool, Task>? onFailed = null, Func<Task>? onSaved = null)
    {
        var state = States.GetOrCreateValue(context);
        state.Pending.Add(new Work(onFailed, onSaved, state.InSavePass));

        // the fallback for a context without the primer interceptor; EF raises its events after the interceptors'
        // hooks, so where the interceptor is wired they find nothing left to do. Re-attached every time: a pooled
        // context drops its handlers when it is returned.
        state.FailedHandler ??= (_, _) => FailedSync(context);
        state.SavedHandler ??= (_, _) => SyncOverAsync.Wait(() => Saved(context));
        context.SaveChangesFailed -= state.FailedHandler;
        context.SaveChangesFailed += state.FailedHandler;
        context.SavedChanges -= state.SavedHandler;
        context.SavedChanges += state.SavedHandler;
    }

    /// <summary>
    /// A primer pass inside a save begins: what an earlier save left, failed where no hook saw it, is undone first. What
    /// an explicit <c>ApplyPrimers()</c> left waits for the save that follows it.
    /// </summary>
    public static async Task BeginSavePass(DbContext context)
    {
        var state = States.GetOrCreateValue(context);
        var abandoned = state.Pending.Where(work => work.InSave).ToArray();
        state.Pending.RemoveAll(work => work.InSave);
        state.InSavePass = true;
        foreach (var work in abandoned)
        {
            await Undo(work, mayStand: false);
        }
    }

    public static void EndSavePass(DbContext context)
    {
        if (States.TryGetValue(context, out var state))
        {
            state.InSavePass = false;
        }
    }

    /// <summary>The save failed before it reached the database: undoes the work registered for it.</summary>
    public static Task FailedBeforeDatabase(DbContext? context) => UndoAll(context, mayStand: false);

    /// <summary>The save failed in the database: undoes the work registered for it, as far as no statement of it may stand.</summary>
    public static Task Failed(DbContext? context) => UndoAll(context, StatementsMayStand(context));

    /// <summary>
    /// <see cref="Failed"/> for a synchronous hook. The transaction is read here, on the saving thread: the work may run
    /// on the thread pool (<see cref="SyncOverAsync"/>), where a <c>TransactionScope</c> without async flow is not visible.
    /// </summary>
    public static void FailedSync(DbContext? context)
    {
        var mayStand = StatementsMayStand(context);
        SyncOverAsync.Wait(() => UndoAll(context, mayStand));
    }

    private static async Task UndoAll(DbContext? context, bool mayStand)
    {
        foreach (var work in Take(context))
        {
            await Undo(work, mayStand);
        }
    }

    // EF's own rule (BatchExecutor): it begins a transaction of its own only where there is none, ambient or enlisted
    // included, and unless AutoTransactionBehavior.Never; in a caller's transaction it makes a savepoint where that
    // transaction supports one and AutoSavepointsEnabled holds
    private static bool StatementsMayStand(DbContext? context)
    {
        if (context == null)
        {
            return false;
        }
        var database = context.Database;
        if (database.CurrentTransaction is { } transaction)
        {
            return !(transaction.SupportsSavepoints && database.AutoSavepointsEnabled);
        }
        return Transaction.Current != null
            || (database.IsRelational() && database.GetEnlistedTransaction() != null)
            || database.AutoTransactionBehavior == AutoTransactionBehavior.Never;
    }

    private static Task Undo(Work work, bool mayStand)
        => work.OnFailed is { } onFailed ? Run(() => onFailed(mayStand)) : Task.CompletedTask;

    /// <summary>The save succeeded: finishes the work registered for it.</summary>
    public static async Task Saved(DbContext? context)
    {
        foreach (var work in Take(context))
        {
            if (work.OnSaved != null)
            {
                await Run(work.OnSaved);
            }
        }
    }

    private static Work[] Take(DbContext? context)
    {
        if (context == null || !States.TryGetValue(context, out var state))
        {
            return [];
        }
        state.InSavePass = false;
        if (state.FailedHandler != null)
        {
            context.SaveChangesFailed -= state.FailedHandler;
        }
        if (state.SavedHandler != null)
        {
            context.SavedChanges -= state.SavedHandler;
        }
        var pending = state.Pending.ToArray();
        state.Pending.Clear();
        return pending;
    }

    private static async Task Run(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception)
        {
            // the save's own outcome is what the caller has to see
        }
    }
}
