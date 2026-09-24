# Transactions and attachment file consistency in Regira Entities

As of 2026-09-22. Sources: the `Regira-Packages` repository, branch `wip`, as read on 2026-09-22. EF Core hook behaviour comes from the `dotnet/efcore` sources on `main`: `StateManager`, `DbContext` and `RelationalTransaction`.

## Recommendation

Decision: add no transaction API to the entity services. Make the attachment pipeline aware of the commit instead, so the database stays the source of truth. After a failure, storage may hold a file too many, but a row never points at a missing file.

1. Document that a unit of work spanning several saves belongs to the consumer, and that the library joins a transaction the caller owns.
2. Add a small mechanism to `Regira.Entities.EFcore` for actions bound to a commit. Code running inside a save registers work to run after the commit, or after a rollback.
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
| `EntityWriteService.SaveChanges` | one `SaveChangesAsync`, then `ChangeTracker.Clear()` on success | EF's implicit one |
| A prepper that loads and changes related rows (`entities.patterns.md`) | the owner's single save | EF's implicit one |
| `SaveChangesBreakingDeleteCycles` | two statements | opens its own, or joins a caller-owned or ambient one |

A unit of work that spans several saves or several services is the consumer's. The tools already exist: `BeginTransaction()` inside `CreateExecutionStrategy().Execute(...)`, or a `TransactionScope` with `TransactionScopeAsyncFlowOption.Enabled`. The library joins both. A transaction abstraction of our own would duplicate EF's. It would also collide with retrying execution strategies in the ways `ai/learnings.md` records for the delete-cycle helper (2026-08-31).

**Work:** one paragraph in each documentation layer. It says what the library guarantees and where the consumer takes over. See step 4.

## Part 2: what the attachment pipeline does today

1. **Preppers run inside `Add`, `Modify` and `Save`.** `EntityAttachmentPrepper` and `RelatedAttachmentsPrepper` only change entry states. They mark a replaced `Attachment` as `Deleted`, and a new one as `Added`.
2. **`SaveChanges` calls the primer interceptor first.** `EntityPrimerContainerInterceptor.SavingChangesAsync` runs before EF opens its transaction. It also runs outside EF's retry loop, because the execution strategy wraps only the database save inside `StateManager`. The primers therefore run once per `SaveChanges` call, even under `EnableRetryOnFailure()`.
3. **`EntityAttachmentPrimer` runs next.** It fills `Attachment.Identifier` from `IFileIdentifierGenerator`, and copies `NewFileName`, `NewContentType` and `NewBytes` onto a modified link's `Attachment`.
4. **`AttachmentPrimer` then does the storage work.** It writes the bytes for `Added` and `Modified` entries that carry content. It deletes the file for `Deleted` entries, loading `Path` first when it is missing.
5. **Finally EF opens its transaction, writes the rows and commits.**

Storage is touched first and the database second, with nothing to undo either side.

| Operation | Storage step, before the database | The database then rejects the save |
| --- | --- | --- |
| Upload (`POST {objectId}/files`, a new link in the owner's `Attachments`) | writes a new file | orphaned new file |
| Replace (`PUT {objectId}/files/{id}`) | writes a new file, deletes the old one | orphaned new file. The surviving row points at the deleted old file |
| Delete (`DELETE attachments/{id}`, a link dropped from the owner's collection) | deletes the file | the row survives, the file is gone, and the client got a 409 saying nothing changed |
| Content change on an existing link (`NewBytes` through the owner's save) | writes under the attachment's `Identifier` | see the note below |
| Shared attachment replace (`PUT attachments/{id}` on `AttachmentControllerBase`) | writes under a fresh name derived from `FileName` | orphaned new file |

Two paths also lose a file **when the save succeeds**. The shared attachment replace, and a content change that lands under a new name, both update `Path` to the new file. Nothing deletes the previous one, so every successful replace leaves a file behind.

**Note on the content-change row.** The outcome depends on whether the nested `Attachment` reaches the primer with its `Identifier` filled. The owner's `Details` runs only the owner's processors, not `AttachmentProcessor`. If the identifier carries over, the stored file is overwritten before the commit, so a failed save leaves the old metadata describing the new bytes. If it does not carry over, the file lands under a new name and the old one leaks on success. Step 0 pins down which of the two happens.

**A storage failure partway through one save** is not consistent either. The primer loop may already have deleted file A when writing file B throws. EF never reaches the database, so A's row survives without its file.

## Part 3: design

### Actions bound to a commit

Public surface. The names are provisional; see the open questions.

<!-- no-compile -->
```csharp
namespace Regira.Entities.EFcore.Extensions;

public static class CommitActionExtensions
{
    // Runs once the rows of the current save are committed. Never throws into the caller; failures are logged.
    public static void AfterCommit(this DbContext dbContext, Func<CancellationToken, Task> action);
    // Runs once the rows of the current save are known not to have been written.
    public static void AfterRollback(this DbContext dbContext, Func<CancellationToken, Task> action);
    // Whether an interceptor is wired that will ever run them (see "No interceptor wired").
    public static bool SupportsCommitActions(this DbContext dbContext);
}
```

A public interceptor, `CommitActionInterceptor`, handles both the save hooks and the transaction hooks. It keeps two lists per context: the actions of the save in progress, and the actions waiting on a transaction the caller owns.

> **Build on the reactor interceptor (added 2026-09-24).** `EntityReactorInterceptor` in `Regira.Entities.EFcore` 6.4.0 already decides when a save is committed, for all three columns of the table below. With no transaction open it acts at `SavedChanges`. Inside a caller's transaction it waits for that transaction's commit, keyed on the `DbTransaction` itself, so contexts sharing it through `UseTransaction` are covered. Inside an ambient scope it acts at `TransactionCompleted`. A rollback, a failure and a transaction disposed without committing discard what waits. Extract that tracking into an internal component that both use, and add the rollback actions and the failure hooks this proposal needs on top of it. Do not write a second interceptor with its own lists. The reactor interceptor has also settled open questions 2 and 3. The lists live in a `ConditionalWeakTable`, and what waits on a transaction is keyed on the `DbTransaction`, so an undecided transaction's work is collected with it. It is wired under `DbContextWiring.Reactors`, not `PrimerInterceptors`. Revisit the wiring paragraph below with that in mind.

| Moment | Save outside any caller transaction | Save inside a caller's `BeginTransaction()` | Save inside an ambient `TransactionScope` |
| --- | --- | --- | --- |
| A primer throws in `SavingChanges` | run this save's rollback actions, then rethrow | same | same |
| Save succeeds (`SavedChanges`) | run commit actions, drop rollback actions | move both lists to the transaction bucket | move both lists to the scope bucket, and subscribe to `TransactionCompleted` once per transaction |
| Save fails (`SaveChangesFailed`, the concurrency hook, `SaveChangesCanceled`) | run rollback actions, drop commit actions | same, because EF rolled back to its savepoint and this save's rows were never written | same |
| Caller commits | not applicable | `TransactionCommitted`: run commit actions | status `Committed`: run commit actions |
| Caller rolls back | not applicable | `TransactionRolledBack` or `TransactionFailed`: run rollback actions | status `Aborted`: run rollback actions |
| Caller disposes without committing | not applicable | EF raises no interceptor hook, only a `TransactionDisposed` log event. The bucket stays undecided and runs nothing | status `InDoubt`: run nothing |

**Which failure hook fires, per EF's `DbContext`:**

- A general failure calls `SaveChangesFailed`.
- Cancellation calls `SaveChangesCanceled` instead.
- A `DbUpdateConcurrencyException` reaches neither of those. It raises only `ThrowingConcurrencyException` and the context's `SaveChangesFailed` event.

The rollback path has to listen to all three, or a 409 from a concurrency conflict leaks files.

**Retries.** A transient failure followed by a successful retry inside EF's strategy reaches `SavedChanges` once. A retry driven by the caller, where the delegate runs `BeginTransaction()` and `SaveChanges()` again, reruns the primers. The failed attempt's transaction reports its failure, which removes the files that attempt wrote.

**Rules for actions:**

- Actions must not use the `DbContext`. The ambient `TransactionCompleted` event can fire on another thread, after the request scope has finished. The attachment actions capture only the file service and a path.
- A failing commit action is logged as a warning and never thrown. The data is already committed.
- A failing rollback action is logged and never masks the original exception.
- The synchronous `SaveChanges` gets the same hooks, awaited through `SyncOverAsync` like the primers.

**Pooled contexts.** A pooled context instance is reused across leases, so no list may outlive its save or its transaction. Every hook that resolves a list clears it, and each `SavingChanges` starts from an empty list for the save in progress. Step 1 decides where the lists live; see open question 2.

**No interceptor wired.** A context without the interceptor would queue actions that never run. `AttachmentPrimer` checks `SupportsCommitActions()` and falls back to today's immediate storage calls. That keeps the manual `EntityPrimerContainer` and `ApplyPrimers()` path working exactly as it does now. That path is documented as such.

**Wiring.** `DbContextWiring.PrimerInterceptors` adds this interceptor beside the primer interceptor, with the same `HasInterceptor` guard in `EntityDbContextOptionsConfiguration`. There is no new flag: the primers are what enlist actions, so the two interceptors travel together, and `UseDefaults()` picks both up. Wherever the guides show a context built outside DI adding the primer interceptor by hand, they add this one beside it.

### Attachment rules

`AttachmentPrimer`, by entry state:

| Entry | Before the commit | After commit | After rollback |
| --- | --- | --- | --- |
| `Added`, with content | write to a free name | nothing | remove the new file |
| `Modified`, with content | read the stored path, then write to a free name | remove the previously stored file if its path differs | remove the new file |
| `Modified`, metadata only | nothing | nothing | nothing |
| `Deleted` | resolve `Path` with the existing lookup | remove the file | nothing |

For a `Modified` entry, the stored path is the entry's original `Path` value when it was tracked against the stored row. Otherwise it is the entity's own `Path` before the write. When both are empty, it is loaded the way the `Deleted` branch loads it today.

**Never overwrite.** Two changes make every content write land on a free name:

- `AttachmentFileService.SaveFile` picks the next available name for every item, not only for new ones.
- `EntityAttachmentPrimer` generates a fresh `Identifier` for a `Modified` link that carries `NewBytes`, instead of keeping an existing one. That preserves the generator's owner-folder layout.

As a result, `SaveFile` no longer overwrites deliberately when a direct caller hands it an existing item. The changelog and the attachments doc say so.

**What each operation does afterwards:**

| Operation | Database rejects the save | Save succeeds |
| --- | --- | --- |
| Upload | new file removed | unchanged |
| Replace (either endpoint) | new file removed, old file intact | old file removed after commit |
| Delete | file intact | file removed after commit |
| Content change | new file removed, old file intact | old file removed after commit |
| Storage fails partway through a save | files already written in this save are removed. No deletes had run yet | not applicable |
| A delete after the commit fails | not applicable | orphaned file, logged |

## Implementation steps

### Step 0: tests first, failing today

Add `tests/Entities.Testing/AttachmentFileLifecycleTests.cs`. It runs on SQLite with a `BinaryFileService` rooted in a temporary folder, and asserts on the folder's contents. The fixture reuses the attachments guide's example owner. The tests cover:

- An upload rejected by a unique constraint leaves no file.
- A delete blocked by a `Restrict` foreign key keeps the file, and the file stays downloadable.
- A replace that fails at the database keeps the old file readable, with its original bytes.
- A content change through the owner's save leaves exactly one file afterwards, holding the new bytes. This test also pins down which of the two current behaviours applies.
- A concurrency conflict (`IHasConcurrencyToken` on the owner) leaves no new file.
- A canceled save leaves no new file.
- A storage failure on the second of two uploads in one save leaves neither file.

In `tests/Entities.Web.Testing/AttachmentTests.cs`, a shared `PUT attachments/{id}` leaves exactly one file.

### Step 1: actions bound to a commit, in `Entities.EFcore`

Add the extension methods and the commit actions, built on the commit tracking of `EntityReactorInterceptor` (see the note under *Actions bound to a commit*). The tests cover:

- Success, general failure, concurrency conflict, cancellation, and a primer that throws.
- A caller transaction that commits, one that rolls back, and one disposed without a commit, which must run nothing.
- A retrying strategy. As `ai/learnings.md` notes, this needs a retrying `ExecutionStrategy` subclass as a fixture.
- The synchronous `SaveChanges`.
- A pooled context reused after a failed save.

The ambient `TransactionScope` path cannot be tested on SQLite, because SQLite rejects the ambient transaction first. That path rests on the documented `TransactionCompleted` behaviour, as the delete-cycle helper's does.

### Step 2: wiring, in `Entities.DependencyInjection`

Update `EntityDbContextOptionsConfiguration` under `PrimerInterceptors`, and extend `UseDefaultsAutoWiringTests`. `InterceptorWiringValidator` already warns when primers are registered without their interceptor. Extend its message only if the two interceptors can end up wired separately.

### Step 3: attachment rework, in `Entities.EFcore`

Change `AttachmentPrimer`, `EntityAttachmentPrimer` and `AttachmentFileService.SaveFile` per the rules above, including the fallback when no interceptor is wired. Step 0's tests turn green.

### Step 4: documentation

Each explanation gets one home per layer. Everything else links to it.

- **Developer docs.** `src/Common.Entities/docs/attachments.md` gains a section on storage and the database: the three rules and what a failed save leaves behind. `docs/services.md` gains the transaction paragraph from Part 1.
- **AI guides.**
  - The attachments section of `entities.instructions.md` gets the same rules in short form.
  - The transaction paragraph goes next to the write-service guidance.
  - `entities.signatures.md` and `entities.namespaces.md` list the new extension methods and interceptor.
  - `entities.patterns.md` gets one worked example of `AfterCommit` in a custom primer, picked so it drags in no unrelated subsystem.
  - Every new snippet compiles under `tools/GuideVerifier`.
- **`ai/learnings.md`** records the EF hook asymmetries this depends on. A concurrency conflict skips `SaveChangesFailed`. Disposing an uncommitted transaction raises no interceptor hook. Primers run outside the execution strategy.

### Step 5: changelog and versions

Add one `CHANGELOG.md` bullet per changed package under `## Unreleased`. The new public types in `Entities.EFcore` count as a minor under AGENTS.md. The wiring change in `Entities.DependencyInjection` counts as a patch. The final numbers are the maintainer's call at implementation time.

## Open questions

1. **The concurrency hook.** `ThrowingConcurrencyException` fires before the throw, and another interceptor can suppress the exception, in which case the save carries on. The alternative is the context's `SaveChangesFailed` event, which fires only for a real failure but is synchronous. Step 1 settles this with a test in which a consumer interceptor suppresses the exception.
2. **Where the lists live.** One option is a `ConditionalWeakTable<DbContext, …>` in `Entities.EFcore`, which is simple and is cleared by the hooks. The other is an internal EF service added through an options extension, which is scoped per context and reset by pooling through `IResettableService`, but needs more plumbing. Recommendation: the table, unless the pooling test in step 1 shows a leak.
3. **Undecided transactions.** A caller that disposes an uncommitted transaction gets orphans for the rows written inside it. One option treats a later `TransactionStarting` or `ConnectionClosed` on the same context as a rollback. Recommendation: accept the orphan, since it is safe and rare.
4. **Public or internal.** Consumer primers could use `AfterCommit` to send mail or clean up external resources only after a commit. Shipping it internal first keeps the public surface smaller until a second use appears. Recommendation: public, because the attachment case already proves the shape is general.
5. **Names.** `AfterCommit`, `AfterRollback`, `SupportsCommitActions` and `CommitActionInterceptor` are working names.

## Out of scope

- **Staging files and moving them on commit.** On Azure and SSH, `IFileService.Move` is a copy plus a delete, which doubles the storage IO without adding safety over the rules above.
- **An outbox table, or a job that reconciles storage against the database.** These only address the rare orphan left when a delete after the commit fails. A consumer who needs that can sweep storage against the stored `Path` values. A helper for it could come later.
- **Saves with `acceptAllChangesOnSuccess: false`.** Those entries stay `Added`, so the next save reruns the primers and writes the files a second time. This is an existing, separate issue.

## Blast radius

| Package | Change | Consumer impact |
| --- | --- | --- |
| `Regira.Entities.EFcore` | new extension methods and interceptor. `AttachmentPrimer`, `EntityAttachmentPrimer` and `AttachmentFileService.SaveFile` change behaviour | none at the source level. Files are now deleted after the commit instead of before, and `SaveFile` never overwrites |
| `Regira.Entities.DependencyInjection` | wires the interceptor under `PrimerInterceptors` | none for `UseDefaults()` and `WireDbContext(PrimerInterceptors)`. A context built outside DI must add the interceptor to get the new guarantees, and keeps today's behaviour until it does |
| `Regira.Entities.Web` | no code change | the shared `PUT attachments/{id}` stops leaking the previous file |
| `Regira.Entities` (guides) | documentation only | guide-only patch |
