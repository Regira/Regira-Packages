using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;

namespace Regira.Entities.EFcore.Utilities;

/// <summary>
/// Work a primer leaves to the end of the save it primes: undone when the save fails and finished when it succeeds. EF
/// reports a failure only from inside its own save, so the failures before it are reported here too:
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
    private sealed record Work(Func<Task> OnFailed, Func<Task>? OnSaved, bool InSave);

    private sealed class State
    {
        public readonly List<Work> Pending = [];
        public bool InSavePass;
        public EventHandler<SaveChangesFailedEventArgs>? FailedHandler;
        public EventHandler<SavedChangesEventArgs>? SavedHandler;
    }

    private static readonly ConditionalWeakTable<DbContext, State> States = new();

    /// <summary>Leaves <paramref name="onFailed"/> and <paramref name="onSaved"/> to the end of the save being primed.</summary>
    public static void Register(DbContext context, Func<Task> onFailed, Func<Task>? onSaved = null)
    {
        var state = States.GetOrCreateValue(context);
        state.Pending.Add(new Work(onFailed, onSaved, state.InSavePass));

        // the fallback for a context without the primer interceptor; EF raises its events after the interceptors'
        // hooks, so where the interceptor is wired they find nothing left to do. Re-attached every time: a pooled
        // context drops its handlers when it is returned.
        state.FailedHandler ??= (_, _) => SyncOverAsync.Wait(() => Failed(context));
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
            await Run(work.OnFailed);
        }
    }

    public static void EndSavePass(DbContext context)
    {
        if (States.TryGetValue(context, out var state))
        {
            state.InSavePass = false;
        }
    }

    /// <summary>The save failed: undoes the work registered for it.</summary>
    public static async Task Failed(DbContext? context)
    {
        foreach (var work in Take(context))
        {
            await Run(work.OnFailed);
        }
    }

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
