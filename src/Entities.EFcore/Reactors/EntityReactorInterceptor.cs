using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Runs the registered <see cref="IEntityReactor"/>s once the changes of a save are <b>committed</b>.
/// <list type="bullet">
///   <item>In <c>SavingChanges</c> — after the primers — it captures every pending row of an entity type a reactor
///     reacts to, with its stored values: the originals the write path loaded, or a read of the row when its writer
///     attached it without them (<c>Update(detached)</c>, a stub <c>Remove</c>).</item>
///   <item>In <c>SavedChanges</c> it builds the changes and runs the reactors — at once when the save committed on its
///     own; at <c>TransactionCommitted</c> when the save ran inside an explicit transaction; when the ambient
///     <see cref="System.Transactions.Transaction"/> completes when there is one. A failed or canceled save, a rollback
///     and a transaction that ends without committing discard them.</item>
/// </list>
/// Both call shapes are hooked: a synchronous <c>SaveChanges()</c> waits for its reactors as the asynchronous one does.
/// Rolling back to a savepoint does not withdraw the reactions of the saves made after it.
/// </summary>
public class EntityReactorInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private sealed class ReactionState
    {
        public List<CapturedChange>? InFlight;
        public readonly List<(Guid TransactionId, IReadOnlyList<IEntityChange> Changes)> AwaitingCommit = [];
    }

    private static readonly ConditionalWeakTable<DbContext, ReactionState> States = new();

    private readonly Lazy<ReactionDispatcher> _dispatcher = new(() => new ReactionDispatcher(serviceProvider));
    private readonly Lazy<ReactorTargets> _unregisteredTargets = new(() => ReactorDiscovery.GetTargets(serviceProvider.GetServices<IEntityReactor>()));

    // Saving: capture

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            SyncOverAsync.Wait(() => Capture(context, async: false, CancellationToken.None));
        }
        return base.SavingChanges(eventData, result);
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await Capture(context, async: true, cancellationToken);
        }
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task Capture(DbContext context, bool async, CancellationToken token)
    {
        // replaced, never appended: a save that did not reach SavedChanges left nothing to react to
        if (States.TryGetValue(context, out var existing))
        {
            existing.InFlight = null;
        }

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

        var captured = new List<CapturedChange>(entries.Length);
        foreach (var entry in entries)
        {
            captured.Add(await CapturedChange.Capture(context, entry, async, token));
        }
        States.GetOrCreateValue(context).InFlight = captured;
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
            SyncOverAsync.Wait(() => Complete(context));
        }
        return base.SavedChanges(eventData, result);
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await Complete(context);
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task Complete(DbContext context)
    {
        context.ClearStoredOriginals();
        if (!States.TryGetValue(context, out var state) || state.InFlight is not { } captured)
        {
            return;
        }
        state.InFlight = null;

        // built now, while the entries still hold what was written — the caller may reuse or clear them next
        IReadOnlyList<IEntityChange> changes = captured.Select(c => c.ToChange()).ToArray();

        if (context.Database.CurrentTransaction is { } transaction)
        {
            state.AwaitingCommit.Add((transaction.TransactionId, changes));
        }
        else if (Transaction.Current is { } ambient)
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

    // Transactions

    void IDbTransactionInterceptor.TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData)
        => SyncOverAsync.Wait(() => Committed(eventData));
    Task IDbTransactionInterceptor.TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
        => Committed(eventData);

    private Task Committed(TransactionEndEventData eventData)
    {
        if (eventData.Context is not { } context || !States.TryGetValue(context, out var state) || state.AwaitingCommit.Count == 0)
        {
            return Task.CompletedTask;
        }
        // A context runs one transaction at a time, so what another transaction left behind ended without committing.
        // The commit of the transaction SaveChanges opens itself comes before SavedChanges: nothing awaits it.
        IReadOnlyList<IEntityChange> changes = state.AwaitingCommit
            .Where(b => b.TransactionId == eventData.TransactionId)
            .SelectMany(b => b.Changes)
            .ToArray();
        state.AwaitingCommit.Clear();
        return _dispatcher.Value.Dispatch(changes);
    }

    void IDbTransactionInterceptor.TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData)
        => DiscardAwaitingCommit(eventData.Context);
    Task IDbTransactionInterceptor.TransactionRolledBackAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken)
    {
        DiscardAwaitingCommit(eventData.Context);
        return Task.CompletedTask;
    }
    void IDbTransactionInterceptor.TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => DiscardAwaitingCommit(eventData.Context);
    Task IDbTransactionInterceptor.TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken)
    {
        DiscardAwaitingCommit(eventData.Context);
        return Task.CompletedTask;
    }

    // A transaction that is disposed without a commit or rollback call raises neither — the next one to start is where
    // what it left behind is known to be dead.
    DbTransaction IDbTransactionInterceptor.TransactionStarted(DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        DiscardAwaitingCommit(eventData.Context);
        return result;
    }
    ValueTask<DbTransaction> IDbTransactionInterceptor.TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken)
    {
        DiscardAwaitingCommit(eventData.Context);
        return ValueTask.FromResult(result);
    }
    DbTransaction IDbTransactionInterceptor.TransactionUsed(DbConnection connection, TransactionEventData eventData, DbTransaction result)
    {
        DiscardAwaitingCommit(eventData.Context);
        return result;
    }
    ValueTask<DbTransaction> IDbTransactionInterceptor.TransactionUsedAsync(DbConnection connection, TransactionEventData eventData, DbTransaction result,
        CancellationToken cancellationToken)
    {
        DiscardAwaitingCommit(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void DiscardAwaitingCommit(DbContext? context)
    {
        if (context != null && States.TryGetValue(context, out var state))
        {
            state.AwaitingCommit.Clear();
        }
    }
}
