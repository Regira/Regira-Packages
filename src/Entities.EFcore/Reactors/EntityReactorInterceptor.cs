using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Runs the registered <see cref="IEntityReactor"/>s once the changes of a save are <b>committed</b>.
/// <list type="bullet">
///   <item>While the context queries, it marks the entries a tracking query loads: their original values are the
///     stored row.</item>
///   <item>In <c>SavingChanges</c> — after the primers — it captures every pending row of an entity type a reactor
///     reacts to, with its stored values: the originals of an entry loaded by a query or by the write path, or a read
///     of the row — one query per entity type — for an entry its writer attached (<c>Update(detached)</c>, a stub
///     <c>Attach</c> or <c>Remove</c>).</item>
///   <item>In <c>SavedChanges</c> it builds the changes and runs the reactors — at once when the save committed on its
///     own; when its database transaction commits when the save ran inside one, through whichever context wired with
///     this interceptor commits it; when the ambient <see cref="System.Transactions.Transaction"/> completes when there
///     is one. A failed or canceled save, a rollback and a transaction that ends without committing discard them.</item>
/// </list>
/// Both call shapes are hooked: a synchronous <c>SaveChanges()</c> waits for its reactors as the asynchronous one does.
/// A rollback to a savepoint made by the caller names no savepoint, so which saves it undid is unknown: the reactions of
/// every save made before it in that transaction still run, marked as possibly undone for a reactor that must not act on
/// a change that never happened (<see cref="Attachments.AttachmentFileReactor{TAttachment, TKey}"/>). EF's own rollback
/// to the savepoint it made for a failing save undoes that save alone, and marks nothing. Not seen: a transaction
/// committed outside EF, on the <see cref="DbTransaction"/> itself (its reactions never run). A transaction begun
/// outside EF and handed to <c>UseTransaction</c> is known to have ended only when it commits or rolls back through
/// EF; on a provider that reuses its transaction object (Npgsql), one disposed without either leaves its reactions to
/// the next such transaction on that connection that commits through EF.
/// </summary>
public class EntityReactorInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor, IDbTransactionInterceptor, IDbCommandInterceptor
{
    private sealed class ReactionState
    {
        public List<CapturedChange>? InFlight;
        // the pool lease the change tracker is watched for: a pooled context drops its event handlers when it is returned
        public int? WatchedLease;
        public EventHandler<EntityTrackedEventArgs>? OnTracked;
    }

    private sealed class AwaitingCommit
    {
        public readonly List<(ReactionDispatcher Dispatcher, IReadOnlyList<IEntityChange> Changes)> Batches = [];
    }

    private static readonly ConditionalWeakTable<DbContext, ReactionState> States = new();
    // Keyed on the database transaction, which contexts sharing it (UseTransaction) hold in common: whichever of them
    // commits it runs the reactions of all. A transaction disposed without a commit raises nothing: what awaits it is
    // collected with it, or — on a provider that hands the same object to the next transaction on its connection
    // (Npgsql) — dropped when that one starts.
    private static readonly ConditionalWeakTable<DbTransaction, AwaitingCommit> AwaitingCommits = new();
    // the changes a rollback to a savepoint may have undone
    private static readonly ConditionalWeakTable<IEntityChange, object> MaybeUndone = new();
    private static readonly object Mark = new();
    // a concurrency conflict raises the context's event, never the interceptors' failure hooks
    private static readonly EventHandler<SaveChangesFailedEventArgs> OnSaveChangesFailed = (sender, _) => Discard(sender as DbContext);

    private readonly Lazy<ReactionDispatcher> _dispatcher = new(() => new ReactionDispatcher(serviceProvider));
    private readonly Lazy<ReactorTargets> _unregisteredTargets = new(() => ReactorDiscovery.GetTargets(serviceProvider.GetServices<IEntityReactor>()));
    private readonly Lazy<ReactorRegistration[]> _unregisteredReactors = new(() => ReactorDiscovery.Resolved(serviceProvider.GetServices<IEntityReactor>()));

    /// <summary>Whether a registered reactor of <paramref name="reactorType"/>, or derived from it, reacts to <paramref name="entityType"/>.</summary>
    internal bool HasReactor(Type reactorType, Type entityType)
    {
        var services = serviceProvider.GetService<IServiceCollection>();
        var registrations = services != null ? ReactorDiscovery.GetCatalog(services).Registrations : _unregisteredReactors.Value;
        return registrations.Any(r => r.IsOf(reactorType) && r.MayHandle(entityType));
    }

    /// <summary>Whether a rollback to a savepoint, made after the save of <paramref name="change"/>, may have undone it.</summary>
    internal static bool MayBeUndone(IEntityChange change) => MaybeUndone.TryGetValue(change, out _);

    // Querying: mark what a tracking query loads

    InterceptionResult<DbDataReader> IDbCommandInterceptor.ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Watch(eventData.Context);
        return result;
    }
    ValueTask<InterceptionResult<DbDataReader>> IDbCommandInterceptor.ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken)
    {
        Watch(eventData.Context);
        return ValueTask.FromResult(result);
    }

    // An entry a tracking query loads holds the stored row as its originals. Watched from the first command a context
    // runs — before a row is materialized — and again on every pool lease; a provider without commands is watched from
    // its first save on.
    private void Watch(DbContext? context)
    {
        if (context == null)
        {
            return;
        }
        var state = States.GetOrCreateValue(context);
        var lease = context.ContextId.Lease;
        if (state.WatchedLease == lease)
        {
            return;
        }
        state.WatchedLease = lease;

        var targets = GetTargets();
        if (targets.IsEmpty)
        {
            return;
        }
        var tracker = context.ChangeTracker;
        if (state.OnTracked != null)
        {
            tracker.Tracked -= state.OnTracked;
        }
        state.OnTracked = (_, e) =>
        {
            if (e.FromQuery && targets.Covers(e.Entry.Metadata.ClrType))
            {
                StoredOriginalsExtensions.MarkLoaded(e.Entry);
            }
        };
        tracker.Tracked += state.OnTracked;
    }

    // Saving: capture

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            SyncOverAsync.Wait(() => CaptureOrFail(context, async: false, CancellationToken.None));
        }
        return base.SavingChanges(eventData, result);
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await CaptureOrFail(context, async: true, cancellationToken);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // a capture that throws ends the save before EF's own failure hooks: what the primers left for it is undone here
    private async Task CaptureOrFail(DbContext context, bool async, CancellationToken token)
    {
        try
        {
            await Capture(context, async, token);
        }
        catch
        {
            await SaveOutcomes.FailedBeforeDatabase(context);
            throw;
        }
    }

    private async Task Capture(DbContext context, bool async, CancellationToken token)
    {
        Watch(context);
        // re-attached every save: a pooled context drops its handlers when it is returned
        context.SaveChangesFailed -= OnSaveChangesFailed;
        context.SaveChangesFailed += OnSaveChangesFailed;
        // replaced, never appended: a save that did not reach SavedChanges left nothing to react to
        var state = States.GetOrCreateValue(context);
        state.InFlight = null;

        var targets = GetTargets();
        if (targets.IsEmpty)
        {
            return;
        }
        var entries = context.GetPendingEntries()
            .Where(e => targets.Covers(e.Metadata.ClrType))
            .ToArray();
        if (entries.Length == 0)
        {
            return;
        }
        state.InFlight = await CapturedChange.CaptureAll(context, entries, async, token);
    }

    private ReactorTargets GetTargets()
    {
        var services = serviceProvider.GetService<IServiceCollection>();
        return services != null ? ReactorDiscovery.GetTargets(services) : _unregisteredTargets.Value;
    }

    // Saved: react, or wait for the commit

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context)
        {
            // read on the saving thread: SyncOverAsync may run the work on the thread pool, where a TransactionScope
            // without async flow is not visible
            var ambient = Transaction.Current;
            SyncOverAsync.Wait(() => Complete(context, ambient));
        }
        return base.SavedChanges(eventData, result);
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await Complete(context, Transaction.Current);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task Complete(DbContext context, Transaction? ambient)
    {
        context.ClearStoredOriginals();
        if (!States.TryGetValue(context, out var state) || state.InFlight is not { } captured)
        {
            return;
        }
        state.InFlight = null;

        // built now, while the entries still hold what was written — the caller may reuse or clear them next
        IReadOnlyList<IEntityChange> changes = captured.Select(c => c.ToChange()).ToArray();

        if (context.Database.CurrentTransaction is IInfrastructure<DbTransaction> transaction)
        {
            var awaiting = AwaitingCommits.GetOrCreateValue(transaction.Instance);
            lock (awaiting)
            {
                awaiting.Batches.Add((_dispatcher.Value, changes));
            }
        }
        else if (ambient != null)
        {
            // created now, while the scope that saved is alive — the transaction may complete after it is disposed
            var dispatcher = _dispatcher.Value;
            ambient.TransactionCompleted += (_, e) =>
            {
                if (e.Transaction?.TransactionInformation.Status == TransactionStatus.Committed)
                {
                    DispatchOutsideAmbient(dispatcher, changes);
                }
            };
        }
        else
        {
            // committed — or, on a provider without database transactions, stored as it will stay
            await _dispatcher.Value.Dispatch(changes);
        }
    }

    // Raised inside the ambient transaction's completion, which is inside the caller's TransactionScope.Dispose(): run
    // outside the transaction, so a reactor's own writes do not try to enlist in one that has ended, and never throw.
    private static void DispatchOutsideAmbient(ReactionDispatcher dispatcher, IReadOnlyList<IEntityChange> changes)
    {
        try
        {
            using var suppress = new TransactionScope(TransactionScopeOption.Suppress, TransactionScopeAsyncFlowOption.Enabled);
            SyncOverAsync.Wait(() => dispatcher.Dispatch(changes));
        }
        catch (Exception ex)
        {
            dispatcher.Report(ex, "Could not run the reactors for {Count} change(s) committed by an ambient transaction", changes.Count);
        }
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        Discard(eventData.Context);
        base.SaveChangesFailed(eventData);
    }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        Discard(eventData.Context);
        base.SaveChangesCanceled(eventData);
    }
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        Discard(eventData.Context);
        return base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    // The entries stay tracked for a retry; their marks go, so a later save reads what it cannot be sure of rather than
    // hold on to entries that may never be saved.
    private static void Discard(DbContext? context)
    {
        if (context == null)
        {
            return;
        }
        context.ClearStoredOriginals();
        if (States.TryGetValue(context, out var state))
        {
            state.InFlight = null;
        }
    }

    // Transactions — the one SaveChanges opens itself commits before SavedChanges: nothing awaits it

    void IDbTransactionInterceptor.TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => SyncOverAsync.Wait(() => Committed(transaction));
    Task IDbTransactionInterceptor.TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
        => Committed(transaction);

    private static async Task Committed(DbTransaction transaction)
    {
        if (!AwaitingCommits.TryGetValue(transaction, out var awaiting) || !AwaitingCommits.Remove(transaction))
        {
            return;
        }
        (ReactionDispatcher Dispatcher, IReadOnlyList<IEntityChange> Changes)[] batches;
        lock (awaiting)
        {
            batches = [.. awaiting.Batches];
        }
        // in the order they were saved, each run of saves by the dispatcher of the context that made them
        var run = new List<IEntityChange>();
        ReactionDispatcher? current = null;
        foreach (var (dispatcher, changes) in batches)
        {
            if (current != null && current != dispatcher)
            {
                await current.Dispatch(run.ToArray());
                run.Clear();
            }
            current = dispatcher;
            run.AddRange(changes);
        }
        if (current != null)
        {
            await current.Dispatch(run.ToArray());
        }
    }

    // A transaction starting on an object means whatever waited on its previous use ended without committing: Npgsql
    // hands the NpgsqlTransaction of a pooled connection to every BeginTransaction on it, the next request's included.
    // UseTransaction is not a start — contexts sharing a live transaction each raise it.
    DbTransaction IDbTransactionInterceptor.TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        AwaitingCommits.Remove(result);
        return result;
    }
    ValueTask<DbTransaction> IDbTransactionInterceptor.TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken)
    {
        AwaitingCommits.Remove(result);
        return ValueTask.FromResult(result);
    }

    void IDbTransactionInterceptor.TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => AwaitingCommits.Remove(transaction);
    Task IDbTransactionInterceptor.TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
    {
        AwaitingCommits.Remove(transaction);
        return Task.CompletedTask;
    }
    void IDbTransactionInterceptor.TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => AwaitingCommits.Remove(transaction);
    Task IDbTransactionInterceptor.TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken)
    {
        AwaitingCommits.Remove(transaction);
        return Task.CompletedTask;
    }

    // A rollback to a savepoint names no savepoint: after the caller's, every save waiting on the transaction may have been
    // undone. EF's own, to the savepoint it made for a failing save, comes from inside that save and undoes it alone.
    void IDbTransactionInterceptor.RolledBackToSavepoint(DbTransaction transaction, TransactionEventData eventData)
        => MarkMayBeUndone(transaction, eventData.Context);
    Task IDbTransactionInterceptor.RolledBackToSavepointAsync(DbTransaction transaction, TransactionEventData eventData, CancellationToken cancellationToken)
    {
        MarkMayBeUndone(transaction, eventData.Context);
        return Task.CompletedTask;
    }

    private static void MarkMayBeUndone(DbTransaction transaction, DbContext? context)
    {
        if (IsSaving(context) || !AwaitingCommits.TryGetValue(transaction, out var awaiting))
        {
            return;
        }
        lock (awaiting)
        {
            foreach (var change in awaiting.Batches.SelectMany(batch => batch.Changes))
            {
                MaybeUndone.AddOrUpdate(change, Mark);
            }
        }
    }

    // EF's own flag, set around the statements of a save and reset in a finally: unlike one an interceptor would set in
    // SavingChanges, it cannot outlive a save that a later interceptor ended before the database was reached
#pragma warning disable EF1001
    private static bool IsSaving(DbContext? context)
        => context?.GetService<Microsoft.EntityFrameworkCore.ChangeTracking.Internal.IStateManager>().SavingChanges == true;
#pragma warning restore EF1001
}
