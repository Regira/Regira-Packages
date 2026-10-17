using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Runs the registered <see cref="IEntityReactor"/>s once the changes of a save are <b>committed</b>.
/// <list type="bullet">
///   <item>In <c>SavingChanges</c> — after the primers — it captures every pending row of an entity type a reactor
///     reacts to, and reads the stored row of each update and delete: one query per entity type.</item>
///   <item>In <c>SavedChanges</c> it builds the changes and runs the reactors — at once when the save committed on its
///     own; when its database transaction commits when the save ran inside one, through whichever context wired with
///     this interceptor commits it; when the ambient <see cref="System.Transactions.Transaction"/> completes when there
///     is one. A failed or canceled save, a rollback and a transaction that ends without committing run none.</item>
/// </list>
/// Both call shapes are hooked: a synchronous <c>SaveChanges()</c> waits for its reactors as the asynchronous one does.
/// A rollback to a savepoint names no savepoint, so the reactions of every save made before it in that transaction
/// still run at the commit: a reactor that must not act on a change that never happened reads the stored row first, as
/// <see cref="Attachments.AttachmentFileReactor{TAttachment, TKey}"/> does. Not seen: a transaction committed outside
/// EF, on the <see cref="DbTransaction"/> itself (its reactions never run). A transaction begun outside EF and handed to
/// <c>UseTransaction</c> is known to have ended only when it commits or rolls back through EF; on a provider that reuses
/// its transaction object (Npgsql), one disposed without either leaves its reactions to the next such transaction on
/// that connection that commits through EF.
/// </summary>
public class EntityReactorInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor, IDbTransactionInterceptor
{
    private sealed class AwaitingCommit
    {
        public readonly List<(ReactionDispatcher Dispatcher, IReadOnlyList<IEntityChange> Changes)> Batches = [];
    }

    // what the save in progress captured: replaced by every save, so one that failed leaves nothing to react to
    private static readonly ConditionalWeakTable<DbContext, List<CapturedChange>> InFlight = new();
    // Keyed on the database transaction, which contexts sharing it (UseTransaction) hold in common: whichever of them
    // commits it runs the reactions of all. A transaction disposed without a commit raises nothing: what awaits it is
    // collected with it, or — on a provider that hands the same object to the next transaction on its connection
    // (Npgsql) — dropped when that one starts.
    private static readonly ConditionalWeakTable<DbTransaction, AwaitingCommit> AwaitingCommits = new();

    private readonly Lazy<ReactionDispatcher> _dispatcher = new(() => new ReactionDispatcher(serviceProvider));

    // Saving: capture

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            // inline, on the saving thread: the synchronous capture awaits nothing that is not already complete, and its
            // read must run inside a TransactionScope without async flow, which the thread pool does not see
            Capture(context, async: false, CancellationToken.None).GetAwaiter().GetResult();
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
        InFlight.Remove(context);
        var targets = ReactorDiscovery.GetCatalog(serviceProvider).Targets;
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
        InFlight.AddOrUpdate(context, await CapturedChange.CaptureAll(context, entries, async, token));
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
        if (!InFlight.TryGetValue(context, out var captured))
        {
            return;
        }
        InFlight.Remove(context);

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
        // in the order they were saved, each by the dispatcher of the context that saved it
        foreach (var (dispatcher, changes) in batches)
        {
            await dispatcher.Dispatch(changes);
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
}
