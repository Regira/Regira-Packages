# Reactor batches: one batch per commit

As of 2026-11-14. Sources: the `Regira-Packages` repository, branch `wip` at `9b87c15` (`src/Entities.EFcore/Reactors/`, `src/Common.Entities/Reactors/`, `src/Entities.DependencyInjection/`, the reactor sections of the Entities guides, `tests/Entities.Testing/ReactorTests.cs`). The timings and probes come from a throwaway console harness built outside the repository against `Entities.DependencyInjection` on `net10.0`, EF Core 10.0.15 and in-memory SQLite. Nothing in the repository has been built.

**Status: built and reviewed twice on 2026-11-14, committed on `wip` with this file; see *Outcome*. Bram took decision 3 (one batch per commit) and accepted the recommendations for decisions 1, 2, 4, 5 and 6 on 2026-11-14. Decision 7, the version, is 6.6.3 (Bram, 2026-11-14).**

## Recommendation

Give reactors a batch form, and make a batch everything one commit made durable.

1. **A batch reactor** (`IEntityBatchReactor<TEntity>`, `EntityBatchReactorBase<TEntity>`, `e.ReactMany(...)`) receives all the changes it covers in one call. It can then do one query, one save, one message or one enqueue, where a per-row reactor does one per row. It is opt-in: `IEntityReactor` and every existing reactor stay as they are.
2. **One batch per commit.** A save that commits on its own is one batch, as today. The saves inside an explicit `BeginTransaction()` or a `TransactionScope` become one batch at the commit, where today each save is dispatched on its own.
3. **Fix the dispose defect.** A scoped service whose disposal throws at the end of a reaction makes the committed save throw. A reactor failure must never fail the save.
4. **Move `AttachmentFileReactor` onto the batch form**, so deleting many attachments reads their rows in one query per 500.
5. **Document** bulk messaging, the rule of thumb for writes inside a reactor, and the save-per-row trap.

The framework's own cost on a large save is small and needs no work. The cost is in the contract: a reactor sees one row at a time and never learns which row was the last.

## What happens today

There is no bulk path of its own: bulk means many tracked entries in one `SaveChanges()`. `ExecuteUpdate` and `ExecuteDelete` bypass reactors, as documented.

- **Capture** (`EntityReactorInterceptor.Capture`, before the save): an insert needs no read. Updates and deletes read their stored rows in one keyed `IN` query per entity type, 500 keys per query (`StoredRowReader.cs:20`). An entity with a composite, converted or owned key falls back to one `GetDatabaseValues` per row.
- **Dispatch** (`ReactionDispatcher.Dispatch`, after the commit): one DI scope, then every change × every reactor, each `React` awaited in turn, all inside the call that commits.
- **Transactions:** each save inside a transaction is kept as its own batch and dispatched on its own at the commit (`EntityReactorInterceptor.Committed`, and one `TransactionCompleted` handler per save for an ambient transaction).
- **No batch hook:** primers have `PrepareManyAsync` and normalizers have `HandleNormalizeMany`. `IEntityReactor` has `React(change)` only.

### Measured

Best of three, timing the `SaveChangesAsync` only. In-memory SQLite has no network round trip, so the per-row rows understate a real server. Differences of a few tens of milliseconds are noise (the runs are short and the JIT is still tiering).

| Scenario | 1000 rows | 5000 rows |
|---|---:|---:|
| Insert, no reactor | ~65 ms | ~130 ms |
| Insert, no-op reactor | ~100 ms | ~150 ms |
| Insert, reactor reads one row per change | 188 ms | 293 ms |
| Insert, reactor adds a row and saves per change | 504–580 ms | 9.5–10.8 s |
| The same, with `ChangeTracker.Clear()` after each save | 82 ms | 280 ms |
| By hand: one read for all keys | 13 ms | 39 ms |
| By hand: all rows in one save | 17 ms | 110 ms |
| Update, no reactor → no-op reactor | 23 → ~30 ms | 111 → ~145 ms |
| Stub delete, no reactor → no-op reactor | 17 → ~23 ms | 79 → 97–140 ms |

- **The framework is not the problem.** Capture, snapshots and dispatch add tens of milliseconds per thousand rows.
- **Per-row reads are N+1.** At about 1 ms per round trip on a real server, 1000 reads take over a second. A batch needs two queries.
- **Save-per-row is quadratic.** The reaction scope's `DbContext` keeps every row it saved, and each further save rescans the whole tracker: EF's own change detection, plus the normalizer, primer and reactor interceptors, which each enumerate the pending entries. Clearing the tracker brings 5000 rows from about 10 s to 280 ms, which confirms the cause. The split between EF and the interceptors was not measured.

### Probed

| Probe | Result |
|---|---|
| A scoped buffer that flushes in `DisposeAsync` and throws | `SaveChangesAsync` throws `InvalidOperationException`; the three rows are stored |
| Three saves in one `BeginTransaction()` | Three reaction scopes after the commit, none before |
| Three saves in one `TransactionScope` (one of them in a nested scope) | Three reaction scopes after completion, none before |
| A reactor that rewrites its own row, no `CanReact` guard, 100 inserts | 800 reactions (8 nested levels × 100), 1.4 s, then one logged error per row |
| `IServiceScopeFactory` resolved from two scopes and from the root | The same instance |
| `Transaction.Current` across saves, after an await and in a nested scope | The same reference each time |
| The API shape below | Compiles; a reactor on an interface receives two entity types in one list |

## Design

### The batch reactor

In `Common.Entities/Reactors/Abstractions`:

```csharp
public interface IEntityBatchReactor : IEntityReactor
{
    /// <summary>Reacts to the committed changes this reactor selected, in the order they were saved.</summary>
    Task ReactMany(IReadOnlyList<IEntityChange> changes, CancellationToken token = default);
}

public interface IEntityBatchReactor<in TEntity> : IEntityReactor<TEntity>, IEntityBatchReactor
{
    Task ReactMany(IReadOnlyList<IEntityChange<TEntity>> changes, CancellationToken token = default);
}

public abstract class EntityBatchReactorBase<TEntity> : EntityReactorBase<TEntity>, IEntityBatchReactor<TEntity>
    where TEntity : class
{
    public abstract Task ReactMany(IReadOnlyList<IEntityChange<TEntity>> changes, CancellationToken token = default);

    // a caller holding the per-row contract still works: a batch of one
    public override Task React(IEntityChange<TEntity> change, CancellationToken token = default) => ReactMany([change], token);

    Task IEntityBatchReactor.ReactMany(IReadOnlyList<IEntityChange> changes, CancellationToken token)
        => ReactMany(changes.Cast<IEntityChange<TEntity>>().ToArray(), token);
}
```

- **`CanReact` selects, `ReactMany` acts.** The dispatcher passes the changes the reactor's registration covers and its `CanReact` accepts. An empty selection makes no call.
- **Registration is unchanged.** A batch reactor is an `IEntityReactor<TEntity>`, so `e.AddReactor<T>()`, `options.AddReactor<T>()` and `ReactorDiscovery` need nothing new.
- **Delegates:** `EntityBatchReactor<TEntity>` beside `EntityReactor<TEntity>`, and on the entity builder:

```csharp
e.ReactMany(async (changes, services, token) => { ... });
e.ReactMany(change => change.ChangedTo(x => x.Status, OrderStatus.Shipped), async (changes, services, token) => { ... });
```

  Two overloads on `EntityServiceBuilder`, each shadowed with `new` on the four derived builders, as `React` is: ten methods.

### Dispatch

`ReactionDispatcher.Dispatch` becomes reactor by reactor (decision 2):

- For each reactor, in registration order: the changes it covers, in save order.
- **A batch reactor** gets one call. A failure is logged with the reactor and the number of changes, the save stands, and the reactors after it start from a fresh scope.
- **A per-row reactor** gets them row by row, each in its own try/catch, as today.
- Reactors are still instantiated lazily, only when a change of a type they cover is in the batch.
- The dispatcher does not cap or chunk a batch (decision 5).

### One batch per commit (decided)

A batch is everything one commit made durable.

| The save | Today | Proposed |
|---|---|---|
| Commits on its own | One dispatch, one scope | Unchanged |
| Inside `BeginTransaction()` | One dispatch and scope per save, at `Commit()` | One dispatch and scope for all its saves, in save order, at `Commit()` |
| Contexts sharing a transaction through `UseTransaction` | One dispatch per save, each by its own context's dispatcher | One dispatch for all of them |
| Inside a `TransactionScope` | One dispatch per save, when the scope completes | One dispatch for all its saves, when the scope completes |

- **Database transactions:** `AwaitingCommit` already collects the saves per `DbTransaction`. `Committed` concatenates them and dispatches once. Saves are merged when their dispatchers share a scope factory, which every context of one application does (probed). Dispatchers of different applications on one transaction stay apart.
- **Ambient transactions:** the saves are collected per `Transaction` in a `ConditionalWeakTable`, as the `DbTransaction` ones are, with one `TransactionCompleted` subscription per transaction. Keying on the reference was probed for the plain and the nested case. A dependent clone is another object and would get its own batch, which is a harmless fallback to today's behaviour.
- **Per-row reactors follow the same rule.** Their reactions of one commit share one scope, where today they share one per save. A scoped de-duplication set, as the guide's `SettledInvoices` pattern uses, then spans the transaction.
- **The same row can appear more than once.** A row inserted by one save and updated by the next is two changes in the list, in order (decision 4).
- **Unchanged:** a rollback, a failed save and a transaction disposed without a commit react to nothing. A rollback to a savepoint still names no saves, so the reactions of the saves before it still run. The nesting limit of 8 still applies to a batch reactor's own saves.

### The dispose fix

`ReactionDispatcher` disposes the reaction scope in its `finally` block and after a failed reactor. An exception from that disposal escapes `Dispatch` and surfaces from `SaveChanges()` although the rows are committed. Wrap both disposals: log through `Report`, never throw. This is a defect on its own and can ship ahead of the rest.

### AttachmentFileReactor

Today each replaced or deleted attachment costs one `AnyAsync` and one `RemoveFile`, in sequence. As a batch reactor it reads which `(Id, Path)` pairs are still named in one query per 500 keys, then removes the files of the others. File removal stays sequential, since `IAttachmentFileService` has no batch removal.

- `AttachmentFileReactor<TAttachment, TKey>` derives from `EntityBatchReactorBase<TAttachment>`, which derives from `EntityReactorBase<TAttachment>`. `React` and `CanReact` stay public.
- **Risk:** a consumer subclass that overrides `React` would no longer be called, because the dispatcher calls `ReactMany`. `WithAttachments` registers the class through a factory, so such a subclass has to be registered by hand and is unlikely. It goes in the CHANGELOG.
- The obsolete constructor without a `DbContext` keeps removing without the read.

### Guidance for the guides

- **Bulk messaging.** A reactor that publishes or enqueues is a batch reactor: one message carrying the ids of the commit, or the bus's batch send. A per-row reactor that enqueues is 1000 enqueues for a 1000-row save, inside the call that commits. Debouncing across requests belongs to the bus or the job system.
- **Writes inside a reactor** are supported and bounded, and cannot be prevented: a reactor can resolve any service. The rule of thumb:

| Need | Use |
|---|---|
| Derived data that must be consistent with the row | A prepper or primer: same save, same transaction |
| A side effect outside the database (mail, bus, job) | A reactor that only enqueues, in batch form for bulk |
| Derived data that may lag | A reactor that writes: guard `CanReact` with `ChangedTo` / `HasChanged`, and save once for the batch |

  A reactor's write is a second commit. It is not atomic with the first, and a failure is only logged.
- **The save-per-row trap.** Saving once per change in the shared reaction scope is quadratic. Save once for the batch, or clear the tracker.
- **No flush on dispose.** A scoped buffer flushed in `Dispose` has no error handling of its own and runs after the last reactor. Use a batch reactor.
- **The `SettledInvoices` pattern** is rewritten on `ReactMany`: the distinct invoice ids of the batch, one save. The scoped `HashSet` goes.

## Decisions

| # | Decision | Recommendation |
|---|---|---|
| 1 | Shape of the batch hook | **Opt-in interface** (above). The alternative is a default interface method `ReactMany` on `IEntityReactor` that loops `React`. It needs no new type, but the dispatcher can then no longer tell a batch reactor from a per-row one, so a per-row reactor that throws on row 3 loses its rows after it, and the per-row log line loses the row's id |
| 2 | Dispatch order | **Reactor by reactor for every reactor.** With per-row reactors A and B and rows 1 and 2, today's order is A1 B1 A2 B2 and the proposed order is A1 A2 B1 B2. Registration order holds in both, and no test or guide pattern depends on the interleaving (`Reactors_Run_In_Registration_Order` saves one row). The alternative keeps today's loop for per-row reactors and runs batch reactors after it: nothing moves for existing apps, but there are two rules, and registration order no longer holds between the two kinds |
| 3 | What a batch is | **Taken: one batch per commit**, for per-row reactors too |
| 4 | Merge the changes of one row across the saves of a transaction into one net change | **No.** The batch lists every change in order, and a reactor that wants one message per row groups by key. A net change needs the key and the comparers of a context that may be gone by the commit, and an insert followed by a delete would have to disappear |
| 5 | Cap or chunk the batch in the dispatcher | **No.** The reactor chunks (`changes.Chunk(500)`), since the right size is the receiver's. The changes of a long transaction are held until its commit, as today |
| 6 | Move `AttachmentFileReactor` to the batch form | **Yes**, with the subclass risk in the CHANGELOG |
| 7 | Version | Bram's call. By the `AGENTS.md` rule a new extension point is a minor for `Regira.Entities`, `Regira.Entities.EFcore` and `Regira.Entities.DependencyInjection`; the dispose fix alone is a patch |

## Not proposed

- **A warning when a reactor reacts inside its own reaction.** The 8-level limit already bounds a loop and logs it. One guarded re-entry is legitimate, so a warning would have to guess.
- **Blocking writes inside a reactor**, or suppressing reactors for a reactor's own saves. The second would also skip the attachment file cleanup.
- **Micro-optimizations** in the dispatcher (caching `Handles` per type, lazy snapshots). They do not show in the measurements.
- **The stored-row fallback** for composite, converted and owned keys: one `GetDatabaseValues` per row during capture, for updates and deletes of such entities only. Not measured. A follow-up of its own if an application hits it.
- **An outbox, or a background channel.** Unchanged from the reactor design: reactions are in-process and at most once.

## Steps

1. **Dispose fix** in `ReactionDispatcher`, with a test: a scoped service that throws on disposal, the save returns, the error is logged.
2. **Batch contract** in `Common.Entities`: the two interfaces, `EntityBatchReactorBase<TEntity>`, `EntityBatchReactor<TEntity>`.
3. **Dispatcher:** reactor by reactor, the batch call, the failure log for a batch.
4. **Interceptor:** one dispatch per committed `DbTransaction`; the ambient table and its single subscription.
5. **Builders:** `ReactMany` on `EntityServiceBuilder` and the four derived builders.
6. **`AttachmentFileReactor`** on the batch form.
7. **Tests** (`ReactorTests`, `AttachmentPrimerTests`, `ProviderReactorTests` for the transaction rows on SQL Server and PostgreSQL):
   - a batch reactor receives the changes of one save in one call, in save order
   - `CanReact` filters the batch; an empty selection makes no call
   - a batch reactor on an interface receives several entity types in one list
   - a batch reactor that throws is logged, the save stands, the next reactor runs in a fresh scope
   - three saves in a transaction are one batch and one scope at the commit; a rollback reacts to nothing
   - two contexts sharing a transaction are one batch
   - three saves in a `TransactionScope`, one in a nested scope, are one batch
   - two per-row reactors and two rows run A1 A2 B1 B2
   - a batch reactor's own save triggers reactors, and the nesting limit holds
   - twenty deleted attachments read their rows in one query; a row that still names its file keeps it
8. **Docs:** the reactor step and patterns in `entities.instructions.md`, `entities.patterns.md` (`SettledInvoices`), `entities.examples.md`, `entities.signatures.md`, `entities.namespaces.md`, `docs/services.md`, `docs/attachments.md`; the wording "the reactions of one committed save share one DI scope" becomes "of one commit"; `CHANGELOG.md`; a row in `ai/learnings.md`.

Steps 1 and 8's guidance on writes and bulk messaging stand on their own and can ship first.

## Outcome

Built on 2026-11-14 as designed, in one pass, with the recommended answers to decisions 1, 2, 4, 5 and 6.

- **Contract** (`Common.Entities/Reactors`): `IEntityBatchReactor`, `IEntityBatchReactor<TEntity>`, `EntityBatchReactorBase<TEntity>`, `EntityBatchReactor<TEntity>`.
- **Dispatcher** (`ReactionDispatcher`): reactor by reactor; one `ReactMany` call for a batch reactor; scope disposal is logged, never thrown.
- **Interceptor** (`EntityReactorInterceptor`): one dispatch per committed `DbTransaction` and per completed ambient `Transaction`, for the dispatchers that share a scope factory.
- **Builders:** `ReactMany` in two overloads on `EntityServiceBuilder` and the four derived builders.
- **`AttachmentFileReactor`:** a batch reactor, with two points beyond the design:
  - Every file is tried when one cannot be removed, and the failures are thrown together as one `AggregateException`, which the dispatcher logs. As a reactor per row, each removal failed on its own, so this keeps that isolation.
  - The stored path is compared in memory, without case. The per-row form compared it in the database, under the column's collation. Without case is the safe direction: a path the row names in another casing keeps its file.
- **Docs:** the reactor step of `entities.instructions.md`, *Settle it from the child with a reactor* in `entities.patterns.md` (on `ReactMany`, the scoped set gone), `entities.examples.md`, `entities.signatures.md`, `entities.namespaces.md`, `entities.card.md`, `docs/services.md`, `docs/attachments.md`, the `IEntityReactor` doc comment, `CHANGELOG.md` and two rows in `ai/learnings.md`. Every snippet touched sits in a `no-compile` block, so the guide verifier was not run.

### Tests

| Run | Result |
|---|---|
| `Entities.Testing`, net10.0 (EF Core 10) | 640 passed |
| `Entities.Testing`, net8.0 (EF Core 9) | 637 passed |
| `Entities.Web.Testing` | 318 passed |
| `Entities.Providers.Testing`, `ProviderReactorTests` | 3 passed on the provider that needs no container; the 6 container runs (two providers) were skipped, Docker was not running |

Eighteen tests were added to `ReactorTests`, four to `AttachmentPrimerTests` and one to `ProviderReactorTests`. They cover the list in step 7. No existing test needed a change.

### Review

A fresh session reviewed the change set on 2026-11-14 from the code and the diff alone: ready to commit, no blocker, two test gaps and nine nits. It ran thirteen mutants against the tests and probed the dispatcher, real `TransactionScope` enlistment on SQL Server LocalDB, and the attachment reactor with `Guid`, `string` and converted keys. What followed from it:

- **Tests added** for the two gaps: the path compared without case, and the fresh scope after a failed reaction. Also the nesting limit for a batch reactor.
- **The dispatcher loop** became one path per kind of reactor. A reactor that cannot be created is logged once for the changes it covers, where it was logged per row, and a failing batch reactor is logged with the number of changes it was handed.
- **Small tidying:** `AttachmentFileReactor` shares `StoredRowReader`'s chunk size and counts only the files it tried in its failure message; `AwaitingCommit.Add` says whether a save is the transaction's first, in place of a separate flag.
- **Docs:** the inline `ReactMany` snippet de-duplicates the ids, and the guide says a dependent clone of the ambient transaction is a batch of its own.
- **Left as it is:** dispatchers are merged when they share a scope factory. Comparing the service collection instead would also merge two providers built from one collection, which do not share their singletons. A container that hands out a scope factory per scope falls back to a batch per context.
- **Left as it is:** a reactor registered under `IEntityBatchReactor<T>` as its service type is not found, as one registered under any type but `IEntityReactor` is not; the interface's doc comment names `AddReactor`.
- **Sealed (Bram, 2026-11-14):** `EntityBatchReactorBase<TEntity>.React` is `sealed`, since the dispatcher calls `ReactMany` and an override of `React` would never run. A subclass of `AttachmentFileReactor` that overrides `React` no longer compiles, which the CHANGELOG states.

### Second review

A second fresh session, which did not read the first report, reviewed the tree after those fixes: ready to commit, no blocker, one should-fix and seven nits. It ran twenty mutants (seventeen killed) and probed real `TransactionScope` enlistment on SQL Server LocalDB. What followed from it:

- **A regression fixed:** a save made under an ambient transaction that had already completed was never reacted to, since only a transaction's first save subscribes to its completion. The completion handler now drops the transaction's entry, so such a save starts a new one and its subscription runs at once. It needs a provider that does not enlist, and a transaction used past its commit.
- **A provider disposed before a commit:** creating the reaction's scope then threw out of `Commit()`. It is logged. This predates the change set.
- **Tests added** for the three survivors: the fresh scope after a reactor that cannot be created, a scoped service that fails to dispose between two reactors, and a file left behind twice in one transaction (which pins the `DistinctBy`).
- **The attachment failure names its files:** the `AggregateException` lists the id and path of up to ten files that could not be removed, since one error per commit replaced one per attachment.
- **CHANGELOG:** the attachment bullet describes the log shape, the sealed `React` bullet says an assembly compiled against an earlier version has to be recompiled, and the batch bullet names the dependent clone.

### Left open

- **The version (decision 7)** is taken: Bram set the cycle's 6.6.3 (2026-11-14). `Regira.Entities`, `Regira.Entities.EFcore` and `Regira.Entities.DependencyInjection` read 6.6.3, as the CHANGELOG bullets do.
- **The container providers.** The transaction batch has not run against SQL Server and PostgreSQL.
