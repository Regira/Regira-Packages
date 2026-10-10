# Transactions and attachment file consistency in Regira Entities

As of 2026-10-10. Sources: the `Regira-Packages` repository, branch `wip` at `e73e5c0`, and probes run against that commit. The probes used SQLite, a `BinaryFileService` in a temporary folder, and the `AttachmentPrimerTests` fixture. EF Core hook behaviour comes from the `dotnet/efcore` sources on `main` (`DbContext`, `BatchExecutor`). A probe confirmed the hooks for the concurrency case.

**Status: built 2026-10-10, uncommitted, at the family's unpublished 6.5.1 (see *Outcome*). The attachment half shipped in 6.5.0 (`b5a29bb`, 2026-10-04), built on a different design from the one first proposed here (see *What 6.5.0 built*). The four fixes, their tests and the documentation of Part 1 followed the recommended answer to every open question.**

## Recommendation

Keep the 6.5.0 design. A public `AttachmentFileReactor` removes files after the commit, and the internal `SaveOutcomes` undoes the writes of a failed save. Add no transaction API. Fix the four paths the probes found. Two of them still let a row name a missing file:

1. **A caller's rollback to a savepoint.** At the commit the reactor removes the file of a replace or a delete that the rollback undid. The row then names a removed file.
2. **A failed save that EF did not roll back.** The save removes the file it wrote, although the row it wrote may still be committed.
3. **A delete without the `AttachmentFileReactor`.** A stub delete throws, and a delete the database refuses has already lost its file.
4. **A concurrency conflict** skips the reactor interceptor's discard.

After the fixes, one rule holds: whatever outcome the library can observe, storage holds every file a stored row names. An outcome it cannot be sure of costs an orphaned file, never a dangling row.

All four are fixes and add no public surface, so each is a patch.

## Part 1: database transactions need no new API

Every write path in the library flushes once, and EF wraps that flush in its own transaction.

| Write path | Flushes | Transaction |
| --- | --- | --- |
| Generated endpoints: controller actions and mapped minimal-API endpoints. Both send requests through `IEntitySender`, and the default handlers of `SaveCommand`, `PatchCommand`, `DeleteCommand` and the three attachment commands (upload, metadata update, file replace) each call `SaveChanges` | one `SaveChanges` | EF's implicit one |
| The shared `AttachmentControllerBase` upload and replace | one `SaveChanges` | EF's implicit one |
| `EntityWriteService.SaveChanges` | one `SaveChangesAsync`, then `ChangeTracker.Clear()` on success. On failure the tracker is kept, so the caller can retry | EF's implicit one |
| A prepper that loads and changes related rows | the owner's single save | EF's implicit one |
| `SaveChangesBreakingDeleteCycles` | two statements when it breaks a cycle, one save otherwise | opens its own when it breaks a cycle, or joins a caller-owned or ambient one |
| An `IEntityPipelineBehavior` that opens a transaction around a request | the request's single save | the behaviour's own. Reactors, and so the attachment removals, wait for its commit |

A unit of work that spans several saves or several services belongs to the consumer. The tools already exist: `BeginTransaction()` inside `CreateExecutionStrategy().Execute(...)`, or a `TransactionScope` with `TransactionScopeAsyncFlowOption.Enabled`. The library joins both. A transaction abstraction of our own would duplicate EF's, and every wrapper around a save would have to detect the caller's transaction itself. `Database.CurrentTransaction` never sees an ambient `TransactionScope` (`ai/learnings.md`, 2026-08-31).

No documentation layer says this yet. Today the commit only appears in the text on reactor timing (`docs/services.md` *Entity Reactors*, *How reactors run* in `entities.instructions.md`) and in the delete-cycle helper. Step 3 adds the paragraph.

## What 6.5.0 built

The attachment work was built during the 6.5.0 pre-PR review rounds. Here is how it compares with what this proposal first designed:

| Proposed | Built |
| --- | --- |
| Internal commit actions (`AfterCommit`, `AfterRollback`) on commit tracking extracted from `EntityReactorInterceptor` | Nothing was extracted. The removals after the commit are a public reactor, `AttachmentFileReactor<TAttachment, TKey>`, which `WithAttachments` registers and which rides the reactors' commit tracking. What a primer leaves to the end of a save goes to the internal `SaveOutcomes`: undone when the save fails, finished when it succeeds. `SaveOutcomes` follows the save, not the transaction |
| A content write never overwrites a file | Built. `AttachmentPrimer` stores new content for a stored file under a key beside the stored one, unless the identifier generator already made a new key. `AttachmentFileService.SaveFile` never writes a stored item over another file |
| A signal for content that came with the write (old open question 5) | Built as recommended: `StoredContent`, a weak table holding the content instance that `AttachmentProcessor` loaded or the primer stored. A rename on the link's own route leaves the file alone |
| The concurrency hook (old open question 1) | `SaveOutcomes` listens to the context's `SaveChangesFailed` event, which a concurrency conflict raises, as well as the primer interceptor's failure hooks. A probe confirms that a conflict removes the new file |
| Wiring a primer-only setup (old open question 2; the recommendation was to wire the reactor interceptor under `PrimerInterceptors`) | The other option was built. The startup warning for a context without the reactor interceptor names the attachment files. Without the reactor, the primer falls back: it removes a replaced file once the save succeeds, and a deleted attachment's file during the save |
| Public or internal (old open question 3; internal recommended) | Internal. The only public addition is the reactor class |
| A primer that throws runs the save's rollback actions | Built. The primer interceptor and the reactor interceptor report a failure of their own `SavingChanges` pass. Work left by a save that failed in another interceptor's `SavingChanges` is undone when the next primer pass begins |
| A retry on the same tracker starts from the stored state | Built. A failed save gives the row back its `Identifier`, `Prefix`, `Path` and `Length` |

The "never overwrite" rule, the stored-content signal and the reactor are described in `docs/attachments.md` (*Dependency Injection*) and in step 6 of the attachments section of `entities.instructions.md`.

## Behaviour on `wip`

With the reactor wiring that `UseDefaults()` sets:

| Operation | The database refuses the save | Save succeeds | Caller's transaction rolls back after a successful save |
| --- | --- | --- | --- |
| Upload | new file removed | — | new file kept, an orphan |
| Replace, or new bytes on any route | new file removed, old file intact, row values restored for a retry | old file removed after the commit | new file kept, an orphan. Old file intact |
| Delete | file intact | file removed after the commit | file intact |
| Rename or other metadata edit | no file touched | no file touched | no file touched |
| Storage fails partway through a save | the files this save already wrote are removed. No delete has run yet | — | — |
| Concurrency conflict | new file removed (probe) | — | — |

`docs/attachments.md` documents the orphan a rolled-back transaction leaves.

### Gaps found by the probes

**A caller's savepoint rollback, then a commit: the reactor removes a file the row names.** The probe ran `BeginTransaction()`, then `CreateSavepoint("before")`, then a save that replaced or deleted an attachment, then `RollbackToSavepoint("before")` and `Commit()`. In both cases the row was back to its stored state, but its file was gone. `EntityReactorInterceptor` does not see a rollback to a savepoint, and EF's savepoint events name no savepoint. So at the commit the reactions of the undone save still run, as the reactor docs say. For most reactors that means a reaction to a change that never happened. For `AttachmentFileReactor` it means a lost file.

**A failed save inside a transaction EF did not roll back: the file goes, and the row may stay.** The probe called `BeginTransaction()` with `AutoSavepointsEnabled = false` and made one save holding two uploads. A trigger refused the second insert, and the caller committed anyway. The first row was committed with its `Path`, and its file was gone. With savepoints on (the default), EF rolls back to its savepoint, nothing is committed, and removing the file is right.

- EF makes no savepoint under an ambient `TransactionScope`, with `AutoSavepointsEnabled` off, or when the transaction does not support savepoints (SQL Server with MARS).
- `SaveOutcomes.Failed` removes the files whatever the transaction.
- Only a caller that commits after a failed save reaches this path.

**Without the `AttachmentFileReactor`, a stub delete throws.** The probe removed `new Attachment { Id }` with the reactor registration taken out. `SaveChanges` threw an `ArgumentNullException` for `Path`, and both the row and the file stayed.

- The primer looks `Path` up with a tracking query. Identity resolution hands back the tracked stub, whose `Path` is empty, and `RemoveFile` refuses it.
- `RelatedAttachmentsPrepper.ResolveAttachment` makes the same kind of stub for a dropped link whose attachment was not loaded.
- With the reactor the delete works, because the reactor reads the stored row.
- In this fallback the file of a deleted attachment also goes during the save, before the database takes the delete. A refused delete therefore loses its file. The startup warning says so, and so does `docs/attachments.md`.

**A concurrency conflict skips the reactor interceptor's discard.** A probe recorded the hooks: on a conflict, EF raises `ThrowingConcurrencyException` and the context's `SaveChangesFailed` event, not the interceptor's `SaveChangesFailed`.

- `EntityReactorInterceptor.Discard` listens only to the interceptor hooks, so the save's stored-original marks outlive it.
- Its capture is replaced at the next save, so no reaction leaks.
- A retry that refreshes only the concurrency token reports stale originals as `Original` to every reactor. For the attachment reactor the worst case is an orphan.

## Design of the fixes

### 1. A savepoint rollback leaves the file to the row

- `EntityReactorInterceptor` gains `RolledBackToSavepoint` and `RolledBackToSavepointAsync`. These mark every batch waiting on that `DbTransaction` as possibly undone. Which saves the rollback undid is unknown, so all of them are marked.
- At the commit the batches run as they do today. Reactors in general keep their documented behaviour.
- `AttachmentFileReactor` does not react to a change from a marked batch. The mark is internal to `Entities.EFcore`: a weak table keyed on the change, set before the batch is dispatched. The file stays. It is an orphan if the row change stood, and the row's own file if the change was undone.
- EF's own rollback to its savepoint after a failed save raises the same event. It comes from inside that save and undoes it alone, so it marks nothing: EF's internal `IStateManager.SavingChanges` is set around the statements and reset in a `finally`, which tells it apart.

Open question 1 weighs this against a check on the stored row.

### 2. A failed save keeps its files unless EF rolled it back

In `SaveOutcomes.Failed`, decide whether EF undid the failed save. It did in two cases:

- The save ran in EF's own transaction: no caller transaction, no ambient transaction, no enlisted one.
- The save ran in a caller's transaction where EF made a savepoint: `Database.CurrentTransaction` supports savepoints, `AutoSavepointsEnabled` holds, and there is no ambient transaction (`BatchExecutor`'s own rule).

Otherwise the row's values are restored for a retry as they are today, but the file is kept. It is an orphan if the transaction rolls back, and the row's file if the caller commits.

Open question 2 weighs this rule against observing the savepoint rollback.

### 3. The fallback removes a deleted file once the save succeeds

When the reactor does not run for the attachment:

- `AttachmentPrimer` reads `Path` from the stored row (`entry.GetDatabaseValuesAsync`, which skips identity resolution) when the entity has none. A row that is already gone has no file to remove.
- It removes the file through `SaveOutcomes` once the save has written the delete, with a guard like the one the replaced file uses: the entry is detached after an accepting save. A refused delete keeps its file.

The fallback then has one rule: a replaced or deleted file goes once the save succeeds. A transaction that then rolls back can still leave a row naming a removed file. The startup warning covers that, and the reactor wiring is the answer.

### 4. A concurrency conflict discards the capture

`EntityReactorInterceptor` also listens to the context's `SaveChangesFailed` event and runs `Discard`. It reattaches the handler on each save, as `SaveOutcomes` does, because a pooled context drops its handlers when it is returned.

## Implementation steps

### Step 0: tests first

Add these to `tests/Entities.Testing/AttachmentPrimerTests.cs`. Its `FileContext` and `RecordingFileService` serve every case, and a SQLite trigger with `RAISE(ABORT)` refuses a chosen row. A replace has to change a mapped column (`FileName`) as well as `Bytes`, which is not mapped, or EF sees nothing to save. The first three fail today:

- A caller's savepoint rollback after a replace, and after a delete, followed by a commit: the row's file survives.
- A failed save inside a transaction with `AutoSavepointsEnabled = false`, followed by a commit: no row names a missing file. The same save with savepoints on leaves no file.
- A stub delete (`new Attachment { Id }`) with and without the `AttachmentFileReactor`: the file goes. Without the reactor, a refused delete keeps the file.
- An upload the database refuses leaves no file.
- A concurrency conflict leaves no new file, and a canceled save leaves none either.
- The shared replace, through `IEntityService<Attachment, int>` as `AttachmentControllerBase` calls it, leaves exactly one file. No test API subclasses that controller, so the service level is the place for this test.
- An upload inside a transaction that rolls back: the row is gone, and the new file stays as documented.
- An ambient `TransactionScope` disposed without `Complete()` removes no stored file. SQLite cannot enlist, so the rows are written at once; what is pinned is that the reactor runs nothing (as in `ReactorTests.An_Ambient_Transaction_Reacts_When_It_Completes`).

Not repeated for attachments: the retrying strategy, pooled contexts and the synchronous save. `ReactorTests` covers them for the commit tracking, and `A_Save_Failing_In_The_Primer_Pass_Removes_The_File_It_Wrote` covers the synchronous failure path of `SaveOutcomes`.

### Step 1: fixes 1 and 4, in `Entities.EFcore`

Add the savepoint mark and the concurrency discard to `EntityReactorInterceptor`, and make `AttachmentFileReactor` skip marked changes. The `ReactorTests` suite stays green. One new test there checks that a reactor other than the attachment reactor still runs after a caller's savepoint rollback, as documented.

### Step 2: fixes 2 and 3, in `Entities.EFcore`

Change `SaveOutcomes.Failed` and the `Deleted` branch of `AttachmentPrimer`. The existing `AttachmentPrimerTests` and the attachment tests in `Entities.Web.Testing`, which run on both surfaces, stay green.

### Step 3: documentation

Each explanation gets one home per layer. Everything else links to it.

- **`docs/services.md`:** the Part 1 paragraph joins the *Entity Reactors* text on when a save counts as committed.
- **`docs/attachments.md`** (*Dependency Injection*):
  - A failed save removes the file it wrote and gives the row back its stored path, unless it failed inside a transaction EF did not roll back.
  - After a caller's rollback to a savepoint, the replaced and deleted files stay.
  - The fallback removes a deleted file once the save succeeds.
  - The comment in the code sample under it names the reactor.
- **`entities.instructions.md`:**
  - The Part 1 paragraph joins *How reactors run*.
  - Step 6 of the attachments section gets the rules above in short form, and states that new bytes go under a key of their own.
  - *Unwired interceptors* points to it for the attachment files.
- **`entities.signatures.md`:** the `WithAttachments` comment names the reactor.
- **`ai/learnings.md`:**
  - A concurrency conflict reaches `ThrowingConcurrencyException` and the context's `SaveChangesFailed` event, never the interceptor's `SaveChangesFailed`.
  - EF makes no savepoint under an ambient transaction, with `AutoSavepointsEnabled` off, or without savepoint support, so a failed save there may leave statements standing.
  - A reactor that destroys something must allow for a savepoint rollback it cannot see.

`entities.instructions.md`, `entities.signatures.md` and `entities.patterns.md` carry uncommitted edits from other work as of this writing. Re-read them before editing.

### Step 4: changelog and versions

Add one `CHANGELOG.md` bullet per changed package under `## Unreleased`: `Regira.Entities.EFcore` for the four fixes, and `Regira.Entities` for the guides. Both are at an unpublished 6.5.1, and every change here is a patch, so the work fits that number. The version line is the maintainer's call.

## Open questions

1. **How the attachment reactor learns that a savepoint rollback may have undone its change.**
   - The recommended option is the mark described under fix 1. It is internal, does no IO, and leaves the reactor API alone.
   - The alternative has `AttachmentFileReactor` check, before it removes a file, that no stored row names it. That needs one query per removed file after the commit. It covers any outcome the interceptor misses, not only savepoints. But the reactor would need the attachment's `DbContext` type, and its public constructor, which derived reactors depend on, would have to stay as it is.
2. **How `SaveOutcomes` decides that EF rolled back a failed save.**
   - The recommended option applies EF's own savepoint rule, as described under fix 2. It needs no new interface on the public primer interceptor.
   - The alternative observes `RolledBackToSavepoint` during the save, which means `EntityPrimerContainerInterceptor` implements `IDbTransactionInterceptor`. It is exact even when EF's rollback to its savepoint fails. That failure is only logged, but it leaves a transaction that cannot commit anyway (a broken connection, a doomed SQL Server transaction).
3. **Removing the new files of a transaction that rolls back after a successful save.** Today they stay as documented orphans. Removing them would need `SaveOutcomes` to follow the transaction's outcome, using the commit tracking `EntityReactorInterceptor` has: `TransactionRolledBack`, `TransactionFailed`, and an ambient status of `Aborted`. Recommendation: not now. Orphans cost storage, never data, and a sweep (see *Out of scope*) covers an app that needs a clean store.

## Out of scope

- **Staging files and moving them on commit.** On Azure and SSH, `IFileService.Move` is a copy plus a delete. That doubles the storage IO without adding safety over the rules above.
- **An outbox table, or a job that reconciles storage against the database.** These only address the orphans described above. A consumer who needs that can sweep storage against the stored `Path` values. A helper for it could come later.
- **Reactions to a change a savepoint rollback undid**, for reactors other than the attachment reactor. This is the reactors' documented limit, and fix 1 leaves it as it is.
- **Saves with `acceptAllChangesOnSuccess: false`.** Those entries stay `Added`, so the next save primes them and stores their content again. This is an existing, separate issue.

## Blast radius

| Package | Change | Consumer impact |
| --- | --- | --- |
| `Regira.Entities.EFcore` | `EntityReactorInterceptor` handles the savepoint rollback and the concurrency discard. `AttachmentFileReactor` skips changes a savepoint rollback may have undone. `SaveOutcomes` keeps the files of a failed save EF did not roll back. The fallback in `AttachmentPrimer` reads a deleted file's path from the stored row and removes it after the save | None at the source level. Some outcomes that removed a file now leave an orphan instead. A stub delete without the reactor stops throwing |
| `Regira.Entities` (guides) | Documentation only | Guide-only patch |
| `Regira.Entities.DependencyInjection`, `Regira.Entities.Web`, `Regira.Entities.Mediator` | No change | None |

## Outcome

Built on `wip` on 2026-10-10 as designed, with the recommended option for each open question: the mark (1), EF's own savepoint rule (2), and no cleanup of a rolled-back transaction's new files (3). Uncommitted.

**Code** (`Regira.Entities.EFcore`):

- **Fix 1.** `EntityReactorInterceptor` implements `RolledBackToSavepoint` and its async twin. They mark every change waiting on that `DbTransaction` in an internal weak table, read through `EntityReactorInterceptor.MayBeUndone(change)`. The mark is set when the rollback happens, so before any dispatch. `AttachmentFileReactor.CanReact` refuses a marked change. A rollback raised while the context is inside EF's own save (`IStateManager.SavingChanges`, an internal API used under `EF1001` as `EntryMarks` does) is EF's rollback of that failing save, and marks nothing. The review suggested a flag set in the interceptor's `SavingChanges` instead. A test shows why that was not taken: when a later interceptor ends the save in its own `SavingChanges`, no hook clears the flag, and a caller's savepoint rollback after it would go unmarked and lose a file.
- **Fix 2.** `SaveOutcomes` has two failure entry points. `FailedBeforeDatabase` undoes everything: the primer pass, the reactor capture, an explicit `ApplyPrimers()` that throws, and work a save abandoned in another interceptor's `SavingChanges`. `Failed` serves the failure hooks and the context event, and tells each piece of work whether the failed save's statements may stand, by `BatchExecutor`'s rule. That rule also counts `AutoTransactionBehavior.Never`, which the design above did not name. A synchronous hook reads the rule before it hands the work to `SyncOverAsync` (`SaveOutcomes.FailedSync`), because a scope without async flow is bound to the saving thread, and under a non-default `TaskScheduler` the work runs on the thread pool. The reactor interceptor's synchronous `SavedChanges` reads `Transaction.Current` the same way, which fixes a gap that predates this work: there the reactors ran at once instead of waiting for such a scope. `AttachmentPrimer` still restores the row's values for a retry, and keeps the written file when the statements may stand.
- **Fix 3.** As designed: the fallback reads a missing `Path` with `entry.GetDatabaseValuesAsync` and removes the file at `SaveOutcomes.Saved` when the entry is `Detached`. A probe showed that `ChangeTracker.Clear()` leaves a dropped entry reporting `Deleted` and raises no `StateChanged`, so the guard also keeps the file of an explicitly primed delete the caller dropped.
- **Fix 4.** A static handler on the context's `SaveChangesFailed` event runs `Discard`. It is re-attached at every capture, because a pooled context drops its handlers when it is returned.

**Beyond the blast radius above:** the startup warning in `Regira.Entities.DependencyInjection` (`InterceptorWiringValidator`) described the old fallback ("removed during the save"). It now says that the replaced and the deleted files go once the save succeeds. That is a text change in a third package.

**Tests.** `AttachmentPrimerTests` gained 19 cases and `ReactorTests` 3. They cover the step 0 list, plus a dropped explicitly primed delete and a refused delete with the reactor. Before the fixes, 7 per target framework failed, as predicted: the savepoint replace and delete, the failed save without savepoints, the stub delete and the refused delete without the reactor, the dropped explicit delete, and the retry after a concurrency conflict. A mutation of the `Detached` guard fails the dropped-delete test. With the review addressed, everything passes: Entities.Testing 572 (net8.0) and 575 (net10.0), Entities.DependencyInjection.Testing 134, Entities.Web.Testing 268, Entities.Mediator.Testing 27.

**Review (2026-10-10).** One should-fix and two nits, all addressed. The should-fix: a failed save the caller catches inside its transaction, followed by a commit, orphaned the files of the earlier saves, because EF's rollback of the failed save marked them. Nit 1: the synchronous failure hooks read `Transaction.Current` on a pool thread under a non-default `TaskScheduler`. Nit 2: the docs now say that `SaveChanges(acceptAllChangesOnSuccess: false)` leaves the files in storage without the reactor wiring. Five tests came with it, and four of them failed before: the caught failed save, for a replace and for a delete; a synchronous refused save under a thread-bound scope on an exclusive scheduler; and the reactors under such a scope. The fifth is the rollback after a save an interceptor ended, which passes with `IStateManager.SavingChanges` and fails with the suggested flag.

**Docs.** As in step 3. `docs/services.md` and *How reactors run* carry the Part 1 paragraph and the savepoint behaviour. `docs/attachments.md` gains *Files and transactions*, and §Attachments step 6 has the same rules in short. *Unwired interceptors* and the `WithAttachments` signature name the reactor. `ai/learnings.md` gains the three entries from step 3 and one on `ChangeTracker.Clear()`. `CHANGELOG.md` has bullets for `Regira.Entities.EFcore`, `Regira.Entities.DependencyInjection` and `Regira.Entities`, all at 6.5.1.
