# Transactions and attachment file consistency in Regira Entities

As of 2026-10-01. Sources: the `Regira-Packages` repository, branch `wip` at `5219481`. EF Core hook behaviour comes from the `dotnet/efcore` sources on `main`: `StateManager`, `DbContext`, `RelationalTransaction` and `BatchExecutor`.

**Status: proposal, not built. Five questions are open (see *Open questions*). No target version is set.**

## Recommendation

Decision: add no transaction API to the entity services. Make the attachment pipeline aware of the commit instead, so the database stays the source of truth. After a failure, storage may hold a file too many, but a row never points at a missing file.

1. Document that a unit of work spanning several saves belongs to the consumer, and that the library joins a transaction the caller owns.
2. Extend the commit tracking that `EntityReactorInterceptor` already does with actions bound to a commit. Code running inside a save registers work to run after the commit, or after a rollback.
3. Rebuild `AttachmentPrimer` on that mechanism with three rules:
   - A content write never overwrites a file.
   - A failed save removes the files it wrote.
   - A file is deleted only after the delete of its row has committed.

No outcome the mechanism cannot observe ever deletes a file. Commit actions only remove files whose rows are confirmed gone. Rollback actions only remove files whose rows were confirmed never written. If the outcome stays unknown, the cost is an orphaned file, never a dangling row.

## Part 1: database transactions need no new API

Every write path in the library flushes exactly once, and EF wraps that flush in its own transaction.

| Write path | Flushes | Transaction |
| --- | --- | --- |
| Generated endpoints (`ControllerExtensions` Save and Delete, both attachment controllers) | one `SaveChanges` | EF's implicit one |
| `EntityWriteService.SaveChanges` | one `SaveChangesAsync`, then `ChangeTracker.Clear()` on success. On failure the tracker is kept, so the caller can retry | EF's implicit one |
| A prepper that loads and changes related rows (`entities.patterns.md`) | the owner's single save | EF's implicit one |
| `SaveChangesBreakingDeleteCycles` | two statements when it breaks a cycle, one save otherwise | opens its own when it breaks a cycle, or joins a caller-owned or ambient one |

A unit of work that spans several saves or several services is the consumer's. The tools already exist: `BeginTransaction()` inside `CreateExecutionStrategy().Execute(...)`, or a `TransactionScope` with `TransactionScopeAsyncFlowOption.Enabled`. The library joins both. A transaction abstraction of our own would duplicate EF's. Every wrapper around a save would also have to detect the caller's transaction itself, and `Database.CurrentTransaction` never sees an ambient `TransactionScope` (`ai/learnings.md`, 2026-08-31).

**Work:** one paragraph in each documentation layer. It says what the library guarantees and where the consumer takes over, and it joins the transaction text the reactors already carry. See step 4.

## Part 2: what the attachment pipeline does today

1. **Preppers run inside `Add`, `Modify` and `Save`, and touch no storage.**
   - `EntityAttachmentPrepper` keeps the stored `Attachment` on a link that arrives without one. When a new `Attachment` arrives, it marks the stored one `Deleted`.
   - `RelatedAttachmentsPrepper` syncs an owner's `Attachments`. It builds a new `Attachment` from `NewBytes` and `NewFileName` where a new link carries them, and marks it `Added`. Under `IsStrictRelation` it marks as `Deleted` the `Attachment` of a link that is replaced, and of a link that is dropped.
   - The validators run after the preppers, before the entity is tracked. A refused write takes back the rows its preppers marked, so it never reaches storage.
2. **`SaveChanges` calls the primer interceptor first.** `EntityPrimerContainerInterceptor` runs the primers in `SavingChanges` and `SavingChangesAsync`, before EF opens its transaction.
   - A plain `SaveChanges` runs them outside EF's retry loop, because the execution strategy wraps only the database save inside `StateManager`. The primers therefore run once per call, even under `EnableRetryOnFailure()`.
   - A save inside a strategy delegate reruns `SavingChanges`, and so the primers, on every retry. That covers the caller's `CreateExecutionStrategy().Execute(...)` recipe and `SaveChangesBreakingDeleteCycles`.
3. **`EntityAttachmentPrimer` runs next.** It fills a link's `Attachment.Identifier` from `IFileIdentifierGenerator` when it is empty. On a modified link it copies `NewFileName` and `NewBytes` onto the `Attachment`.
4. **`AttachmentPrimer` then does the storage work.**
   - For `Added` and `Modified` entries, it sets `ContentType` from `FileName` and writes the bytes whenever `HasContent()` holds.
   - For `Deleted` entries, it deletes the file, loading `Path` first when it is missing.
5. **Finally EF opens its transaction, writes the rows and commits.**

Storage is touched first and the database second, with nothing to undo either side.

| Operation | Storage step, before the database | The database then rejects the save |
| --- | --- | --- |
| Upload (`POST {objectId}/files`, or a new link in the owner's `Attachments`) | writes a new file | orphaned new file |
| Replace (`PUT {objectId}/files/{id}`) | writes a new file, deletes the old one | orphaned new file. The surviving row points at the deleted old file |
| Delete (`DELETE attachments/{id}`, or a link dropped from the owner's collection) | deletes the file | the row survives, the file is gone, and the client got a 409 saying nothing changed |
| Content change on an existing link (`NewBytes`) | depends on the route, see below | see below |
| Shared attachment replace (`PUT attachments/{id}` on `AttachmentControllerBase`) | writes under a fresh name derived from `FileName` | orphaned new file |

**The content change depends on the route.** `Identifier` is `[NotMapped]`. Only `AttachmentProcessor` fills it on a read.

- **Through the owner's save.** The owner's `Details` runs only the owner's processors, so the nested `Attachment` arrives without an `Identifier`. `EntityAttachmentPrimer` generates a fresh one, and the bytes land under a new name. A failed save orphans the new file. A successful one updates `Path` and leaks the old file.
- **Through the link's own `PUT {objectId}/attachments/{id}`.** The original is read with `Details`, whose full include set makes `AttachmentProcessor` fill `Identifier` and load `Bytes`. `EntityAttachmentPrepper` keeps that `Attachment`. A write therefore lands on the stored file before the commit, and a failed save leaves the old metadata describing the new bytes.
  - The loaded `Bytes` satisfy `HasContent()`, so a rename on this route also rewrites the stored file with its own bytes.
  - How the stored `Attachment` is tracked on this route, and so whether a primer sees `NewBytes` or a rename at all, is untested. Step 0 pins it.

Two paths also lose a file **when the save succeeds**: the shared attachment replace, and a content change through the owner's save. Both update `Path` to the new file, and nothing deletes the previous one.

**A storage failure partway through one save** is not consistent either. The primer loop may already have deleted file A when writing file B throws. EF never reaches the database, so A's row survives without its file.

## Part 3: design

### Actions bound to a commit

The surface. Its names are provisional (open question 4), and whether it is public is open question 3.

<!-- no-compile -->
```csharp
namespace Regira.Entities.EFcore.Extensions;

public static class CommitActionExtensions
{
    // Runs once the rows of the current save are committed. Never throws into the caller; failures are logged.
    public static void AfterCommit(this DbContext dbContext, Func<CancellationToken, Task> action);
    // Runs once the rows of the current save are known not to have been written.
    public static void AfterRollback(this DbContext dbContext, Func<CancellationToken, Task> action);
    // Whether the commit tracking that runs them is wired (see "No commit tracking wired").
    public static bool SupportsCommitActions(this DbContext dbContext);
}
```

**Commit tracking is shared with the reactors.** `EntityReactorInterceptor` in `Regira.Entities.EFcore` already decides when a save is committed:

- With no transaction open, it acts at `SavedChanges`.
- Inside a caller's transaction, it waits for that transaction's `TransactionCommitted`. The wait is keyed on the `DbTransaction` itself, so contexts sharing it through `UseTransaction` are covered.
- Inside an ambient scope, it acts at `TransactionCompleted` with status `Committed`.
- A rollback, a failure and a transaction disposed without committing discard what waits.
- Work waiting on a transaction object is dropped when a new transaction starts on it, because Npgsql reuses one `NpgsqlTransaction` per pooled connection.

Its state lives in two `ConditionalWeakTable`s: the save in progress, keyed on the `DbContext`, and the work waiting for a commit, keyed on the `DbTransaction`. An undecided transaction's work is therefore collected with it.

Extract that tracking into an internal component with a payload of its own, and enlist both the reactor batches and the commit actions in it. Do not write a second interceptor with its own lists. The extraction adds what the reactors never needed:

- **Rollback dispatch.** Where the reactors only discard, rollback actions run on a failed or canceled save, on `TransactionRolledBack` and `TransactionFailed`, and on status `Aborted`.
- **The concurrency hook** (see *Which failure hook fires* below).
- **The savepoint rules** (see *Savepoints* below).
- **A primer that throws.** EF calls `SavingChanges` outside the `try` that raises its failure hooks. The primer interceptor therefore runs the save's rollback actions itself before it rethrows.
- **The reset point.** The reactor interceptor resets the save in progress in its own `SavingChanges`, which runs after the primer interceptor. Reset there, the commit actions' list would lose what the primers just registered. It is reset when the primer pass starts instead (`ai/learnings.md`: reset per-pass state when a pass starts).

| Moment | Save outside any caller transaction | Save inside a caller's `BeginTransaction()` | Save inside an ambient `TransactionScope` |
| --- | --- | --- | --- |
| A primer throws in `SavingChanges` | run this save's rollback actions, then rethrow | same | same |
| Save succeeds (`SavedChanges`) | run commit actions, drop rollback actions | move both lists to the transaction bucket | move both lists to the scope bucket, and subscribe to `TransactionCompleted` |
| Save fails (`SaveChangesFailed`, the concurrency hook, `SaveChangesCanceled`) | run rollback actions, drop commit actions | if EF rolled back to its own savepoint during this save, run rollback actions. Otherwise move them to the transaction bucket. Drop commit actions either way | move rollback actions to the scope bucket, drop commit actions |
| Caller rolls back to a savepoint of its own | not applicable | drop every commit action in the bucket, keep its rollback actions | not applicable |
| Caller commits | not applicable | `TransactionCommitted`: run commit actions, drop rollback actions | status `Committed`: run commit actions, drop rollback actions |
| Caller rolls back | not applicable | `TransactionRolledBack` or `TransactionFailed`: run rollback actions, drop commit actions | status `Aborted`: run rollback actions, drop commit actions |
| Caller disposes without committing | not applicable | EF raises no interceptor hook, only a `TransactionDisposed` log event. The bucket stays undecided, runs nothing, and is collected with the `DbTransaction` | status `InDoubt`: run nothing |

**Savepoints.** EF makes a savepoint for a save only inside a transaction it did not begin, and only when the provider supports savepoints and `Database.AutoSavepointsEnabled` holds. It never makes one under an ambient transaction (`BatchExecutor`). When the save fails, EF rolls back to that savepoint, and only logs a failure of that rollback.

- **When a failed save wrote nothing.** Inside a transaction, a failed save has written nothing only when `RolledBackToSavepoint` fired between its `SavingChanges` and its failure hook. Otherwise some of its statements may stand, and the caller may still commit them. Its rollback actions then wait for the transaction's outcome.
- **A rollback to a savepoint outside any save is the caller's own.** EF's savepoint events name no savepoint, so which saves it undid is unknown. Dropping the bucket's commit actions turns any delete they would have made into an orphan, never a dangling row.
- **The reactors keep running** for the saves made after such a savepoint, as their docs state.

**Which failure hook fires, per EF's `DbContext`:**

- A general failure calls `SaveChangesFailed`.
- Cancellation calls `SaveChangesCanceled` instead.
- A `DbUpdateConcurrencyException` reaches neither of those. It raises only `ThrowingConcurrencyException` and the context's `SaveChangesFailed` event.

The rollback path has to listen to all three, or a 409 from a concurrency conflict leaks files. The reactor interceptor implements the first two only. A failed save reacts to nothing either way, but a concurrency conflict also skips the `ClearStoredOriginals()` its failure path runs, so that save's stored-original marks outlive it. The shared component's concurrency hook covers both.

**Retries.**

- **A retry inside EF's strategy.** A transient failure followed by a successful retry reaches `SavedChanges` once.
- **A retry driven by the caller.** When the delegate runs `BeginTransaction()` and `SaveChanges()` again, the primers run again. The failed attempt's transaction reports its failure, and that removes the files the attempt wrote.
- **A retry on the same tracker.** `EntityWriteService` keeps the tracker after a failed save, so the caller can retry. At the save's failure, the entity's `Path`, `Prefix` and `Identifier` are therefore restored to their values before the write. This happens whether the file removal runs then or waits for the transaction's outcome. The retry then starts from the stored state, not from a file that is removed or about to be.

**Rules for actions:**

- **Actions must not use the `DbContext`.** The ambient `TransactionCompleted` event can fire on another thread, after the request scope has finished. The attachment actions capture only the file service and a path, and run under `TransactionScopeOption.Suppress`, as the reactors' ambient work does (`ai/learnings.md`, 2026-09-23).
- **A failing commit action is logged as a warning and never thrown.** The data is already committed.
- **A failing rollback action is logged and never masks the original exception.**
- **The synchronous `SaveChanges` gets the same hooks,** awaited through `SyncOverAsync` like the primers.

**Pooled contexts.** The shared tracking already follows pool leases, and `ReactorTests` pins a pooled context across leases. Every hook that resolves a list clears it, and each primer pass starts from an empty list for the save in progress.

**No commit tracking wired.** A context without it would queue actions that never run. `AttachmentPrimer` checks `SupportsCommitActions()` and falls back to today's immediate storage calls. That keeps the manual `EntityPrimerContainer` and `ApplyPrimers()` path working exactly as it does now. That path is documented as such.

**Wiring.** The commit tracking lives in `EntityReactorInterceptor`, which `DbContextWiring.Reactors` wires. `Reactors` is part of `All`, so `UseDefaults()` picks it up.

- `WireDbContext(DbContextWiring.PrimerInterceptors)` alone is the documented setup for an app that registers only primers. It wires no reactor interceptor, so that app's attachments would keep today's behaviour without a word. Open question 2 settles this.
- Wherever the guides show a context built outside DI adding the primer interceptor by hand, they add the reactor interceptor beside it.

### Attachment rules

`AttachmentPrimer`, by entry state:

| Entry | Before the commit | After commit | After rollback |
| --- | --- | --- | --- |
| `Added`, with content | write under a fresh identifier | nothing | remove the new file |
| `Modified`, with new content | read the stored path, then write under a fresh identifier | remove the previously stored file if its path differs | remove the new file |
| `Modified`, no new content | nothing | nothing | nothing |
| `Deleted` | resolve `Path` with the existing lookup | remove the file | nothing |

**The stored path of a `Modified` entry**, in order:

1. The entry's original `Path` value, when the entry was tracked against the stored row.
2. Otherwise, the entity's own `Path` before the write.
3. When both are empty, the path loaded the way the `Deleted` branch loads it today.

**New content is not `HasContent()`.** `AttachmentProcessor` loads `Bytes` on a read with the full include set, which `Details` always uses. The link's own `PUT {objectId}/attachments/{id}` reads its original that way, so the `Attachment` it writes back already has content. Through the owner's save it does not. The primer needs a signal for content that came with the write (open question 5).

**Never overwrite.** `DefaultFileIdentifierGenerator` builds `{Owner}/Attachments/{ObjectId}/{kebab-name}-{guid}{ext}`, and `SaveFile` derives `{kebab-name}-{guid}{ext}` when `Identifier` is empty. A fresh identifier per content write is therefore a free name by construction. It needs no `Exists` round trip, and two writers cannot race for the same next number.

- `EntityAttachmentPrimer` generates a fresh `Identifier` for a `Modified` link that carries `NewBytes`, instead of keeping an existing one. That preserves the generator's owner-folder layout.
- `AttachmentPrimer` gives a `Modified` attachment with new content a fresh identifier when its `Identifier` still names the stored file.
- A custom `IFileIdentifierGenerator` may build the same name twice. When the fresh identifier equals the stored path, the primer takes the next free name, as `SaveFile` does for a new item.

`AttachmentFileService.SaveFile` keeps its contract: a new item gets the next free name, and an existing item is written at its `Identifier`.

**What each operation does afterwards:**

| Operation | Database rejects the save | Save succeeds |
| --- | --- | --- |
| Upload | new file removed | unchanged |
| Replace (either endpoint) | new file removed, old file intact | old file removed after commit |
| Delete | file intact | file removed after commit |
| Content change | new file removed, old file intact | old file removed after commit |
| Storage fails partway through a save | files already written in this save are removed. No deletes had run yet | not applicable |
| A delete after the commit fails | not applicable | orphaned file, logged |

Two more outcomes leave an orphan:

- A caller's transaction disposed without committing keeps the files its saves wrote.
- A caller's rollback to a savepoint, followed by a commit, keeps the files its transaction would have deleted, and the files the undone saves wrote.

## Implementation steps

### Step 0: tests first, failing today

Add `tests/Entities.Testing/AttachmentFileLifecycleTests.cs`. It runs on SQLite with a `BinaryFileService` rooted in a temporary folder, and asserts on the folder's contents. The fixture reuses the attachments guide's example owner. The tests cover:

- An upload rejected by a unique constraint leaves no file.
- A delete blocked by a `Restrict` foreign key keeps the file, and the file stays downloadable.
- A replace that fails at the database keeps the old file readable, with its original bytes.
- A content change through the owner's save leaves exactly one file afterwards, holding the new bytes. By the code it leaves two today; no test pins that yet.
- A concurrency conflict (`IHasConcurrencyToken` on the owner) leaves no new file.
- A canceled save leaves no new file.
- A storage failure on the second of two uploads in one save leaves neither file.
- A delete of an attachment known only by its key removes its file. `AttachmentPrimer` looks `Path` up through a tracking query, which may return the tracked stub with `Path` still empty.

In `tests/Entities.Web.Testing/AttachmentTests.cs`:

- A shared `PUT attachments/{id}` leaves exactly one file.
- A content change through the link's own `PUT {objectId}/attachments/{id}` stores the new bytes.
- A rename on that route leaves the stored file untouched.

### Step 1: commit tracking shared with the reactors, in `Entities.EFcore`

Extract the commit tracking from `EntityReactorInterceptor` into the internal component described under *Actions bound to a commit*, and enlist the reactors in it. Then add the commit actions and the extension methods. The whole `ReactorTests` suite stays green; it guards the extraction. The new tests cover:

- Success, general failure, concurrency conflict, cancellation, and a primer that throws.
- A caller transaction that commits, one that rolls back, and one disposed without a commit, which must run nothing.
- A failed save inside a caller transaction with `AutoSavepointsEnabled = false`, which keeps its files until the transaction rolls back.
- A caller's rollback to a savepoint followed by a commit, which deletes no file.
- An ambient scope that completes and one that is disposed. `ReactorTests.An_Ambient_Transaction_Reacts_When_It_Completes` shows how, on SQLite with `AmbientTransactionWarning` ignored. SQLite cannot enlist, so the rows are written at once; what is pinned is when the actions run.
- A retrying strategy, using the retrying `ExecutionStrategy` subclass that `DeleteCycleTests` already has as a fixture.
- The synchronous `SaveChanges`.
- A pooled context reused after a failed save.

### Step 2: wiring, in `Entities.DependencyInjection`

Wire the commit tracking as open question 2 decides, in `EntityDbContextOptionsConfiguration` with the existing `HasInterceptor` guard, and extend `UseDefaultsAutoWiringTests`.

- **With the recommended option,** `PrimerInterceptors` wires the reactor interceptor too, and `UseDefaultsAutoWiringTests` pins it for a primer-only setup.
- **With the other,** `InterceptorWiringValidator` warns when attachments are registered and the context lacks the reactor interceptor. Today it reports a missing primer interceptor as a Warning, or as Info when an `EntityPrimerContainer` is registered, and a missing reactor interceptor only when reactors are registered.

### Step 3: attachment rework, in `Entities.EFcore`

Change `AttachmentPrimer` and `EntityAttachmentPrimer` per the rules above, add the new-content signal (open question 5), and keep the fallback when the commit tracking is not wired. `AttachmentFileService.SaveFile` does not change. Step 0's tests turn green.

### Step 4: documentation

Each explanation gets one home per layer. Everything else links to it.

- **Developer docs.**
  - `src/Common.Entities/docs/attachments.md` gains a section on storage and the database: the three rules and what a failed save leaves behind.
  - `src/Common.Entities/docs/services.md` already states when reactors count a save as committed. The Part 1 paragraph joins that text rather than sitting beside it.
- **AI guides.**
  - The attachments section of `entities.instructions.md` gets the same rules in short form.
  - The transaction paragraph joins *How reactors run* in `entities.instructions.md`.
  - If open question 3 makes the extension methods public:
    - `entities.signatures.md` and `entities.namespaces.md` list them.
    - `entities.patterns.md` gets one worked example of `AfterCommit` in a custom primer, picked so it drags in no unrelated subsystem, and says when a reactor fits better.
  - Every new snippet compiles under `tools/GuideVerifier`.
- **`ai/learnings.md`** already records that `SavedChanges` is not the commit and that a savepoint rollback cannot be followed (2026-09-23). It gains what this work adds:
  - A concurrency conflict skips `SaveChangesFailed`.
  - EF makes no savepoint under an ambient transaction, so a failed save there may leave statements standing.
  - A primer that throws reaches no failure hook.

### Step 5: changelog and versions

Add one `CHANGELOG.md` bullet per changed package under `## Unreleased`. `Regira.Entities.EFcore` and `Regira.Entities.DependencyInjection` are at an unpublished 6.5.0. Whether this work joins that number or takes the next one is the maintainer's call.

Under AGENTS.md, public extension methods (open question 3) make the `Entities.EFcore` change a minor. Kept internal, the change is a fix, which is a patch.

## Open questions

1. **The concurrency hook.** `ThrowingConcurrencyException` fires before the throw, and another interceptor can suppress the exception, in which case the save carries on. The alternative is the context's `SaveChangesFailed` event, which fires only for a real failure but is synchronous. The reactor interceptor implements neither. Step 1 settles this with a test in which a consumer interceptor suppresses the exception.
2. **Wiring a primer-only setup.** `WireDbContext(PrimerInterceptors)` wires no reactor interceptor, so attachments there keep today's behaviour.
   - One option wires the reactor interceptor under `PrimerInterceptors` as well. The `HasInterceptor` guard keeps it single, and a context with no reactors captures nothing for them.
   - The other keeps the flags apart and has `InterceptorWiringValidator` warn when attachments are registered without it.

   Recommendation: wire it under both flags, so the attachment guarantee needs no extra step.
3. **Public or internal.** Consumer primers could use `AfterCommit` to clean up external resources only after a commit. Reactors, though, are already the public extension point for work after a commit (mail, jobs, workflows), with the change and its stored original in hand. A second public hook would leave consumers choosing between two. Recommendation: internal. `AttachmentPrimer`, its only user, sits in the same assembly. Make it public when a use appears that a reactor cannot serve.
4. **Names.** `AfterCommit`, `AfterRollback` and `SupportsCommitActions` are working names.
5. **The new-content signal.** `HasContent()` holds for bytes that `AttachmentProcessor` loaded, so the primer needs another way to tell which content came with the write.
   - One option keeps a weak reference to the byte array the processor loaded, per `Attachment`, and treats that same array on save as stored content.
   - Another compares a hash with the stored file, at the cost of a read.

   Recommendation: the weak reference, which costs no IO and adds no public surface.

## Out of scope

- **Staging files and moving them on commit.** On Azure and SSH, `IFileService.Move` is a copy plus a delete, which doubles the storage IO without adding safety over the rules above.
- **An outbox table, or a job that reconciles storage against the database.** These only address the rare orphans described above. A consumer who needs that can sweep storage against the stored `Path` values. A helper for it could come later.
- **Saves with `acceptAllChangesOnSuccess: false`.** Those entries stay `Added`, so the next save reruns the primers and writes the files a second time. This is an existing, separate issue.

## Blast radius

| Package | Change | Consumer impact |
| --- | --- | --- |
| `Regira.Entities.EFcore` | The commit tracking moves out of `EntityReactorInterceptor` into a shared internal component, and commit actions are added. `AttachmentPrimer` and `EntityAttachmentPrimer` change behaviour, and `AttachmentProcessor` does too if open question 5 picks the weak reference | None at the source level. Files are deleted after the commit instead of before. A content change writes under a new name and removes the old file after the commit |
| `Regira.Entities.DependencyInjection` | Wiring, per open question 2 | None for `UseDefaults()`. A context built outside DI must add the reactor interceptor to get the new guarantees, and keeps today's behaviour until it does |
| `Regira.Entities.Web` | No code change | The shared `PUT attachments/{id}` stops leaking the previous file |
| `Regira.Entities` (guides) | Documentation only | Guide-only patch |
