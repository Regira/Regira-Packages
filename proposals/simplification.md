# Simplifying the packages
<!-- {% raw %} -->

As of 2026-10-12. Sources: the `Regira-Packages` repository, branch `wip` at `cb565fc`, compared with the tag `v6.5.0`, the last release; `CHANGELOG.md` from 6.3.0 through *Unreleased*, which records why each mechanism was added (each item names the version it came with); and eight read-only reviews of the source and tests, one per package family, whose main claims were checked against the code. Nothing on this page has been built or run.

**Status: steps 0 (`80f3f9e`), 1 (`25f1905`), 2 (`c88a7bf`), 3 (`804b170`) and 4 (`9d99eff`) built and committed, step 5 (wrap-up) committed as `8d25056`; decisions 7 and 8 built after it, uncommitted (see *Outcome*). Decisions 1 (a failed save's file stays), 2 (originals by reading), 3 (only declared stamps compared), 4 (drop the MediatR adapter) and 6 (drop `FollowOn`) were taken as recommended, decision 5 the other way (the operations stay in `Regira.Entities.Mediator`); decision 7 is 6.6.0 and decision 8 *`AesEncrypter` needs a secret*, both as recommended, and *5a* stays out. In step 4 you took none of the optional rows, fixed `FilterIsActiveOn(null)`, documented the `Margins` order and Printing.GDI's margin unit, and adopted the deprecated-backends policy.**

## Recommendation

Most of the weight arrived in the last four cycles. The changelog bullets from 6.3.0 on describe edge cases more than features — savepoints, a `TransactionScope` without async flow, Npgsql reusing a transaction object, ids a vendor writes twice, the regular expressions MiniWord builds from keys — and each left machinery behind. The unpublished 6.5.1 alone adds about 6,300 net lines of C# across 154 files, most of it in `Regira.Office` (Word templating), the new `Regira.Entities.Mediator` and `Regira.Entities.Web`.

The plan removes roughly 4,500 lines of production code now, about 2,300 more at 7.0 through `[Obsolete]` markers, and about 1,500 lines of tests (estimates from the reviews). More than lines, it removes mechanisms:

- `SaveOutcomes`, the savepoint marking and the primer's second way of deleting files: one rule decides when an attachment file goes.
- The "stored originals" marks kept on EF's internal entries, with the query watch and the pool-lease re-subscription in `EntityReactorInterceptor`.
- Four per-backend ways of renumbering ids in Word copies (`VmlShapeIds`, `PackageIds` and two walks' renumbering).
- The generated-name layer Word.Mini puts between keys and MiniWord.
- The executor split, the untyped execution path and the MediatR adapter package in the request layer.
- About 300 lines of builder code nothing calls, and a registration list hand-kept per builder shape whose drift silently drops an application's own repository (see *Found while reviewing*).
- Startup checks that diagnose failures already loud at the first request.

If only five things are done: attachment files and reactors (*1*); the Word copy ids, Word.Mini names and the `ConditionalBlocks` copy (*5a*, *5d*, *5c*); the dead builder code and the registration helper (*3a*, *3b*); one sender in the request layer (*4a*); and the startup checks (*3c*).

## Rules

1. **Critical behaviour stays in code**: a crash or hang, a valid input that throws, lost or corrupted data, a security hole. Every item names what protects against those and keeps it.
2. **A rare shape whose handling costs more than it saves becomes a documented limitation**, written once where readers meet it (the `ai/` guide and the `docs/` page), the rest linking to it.
3. **One rule for every backend and every write path**, not a patch per vendor or per transaction shape.
4. **Unpublished 6.5.1 work is reshaped freely**, public API included. Its *Unreleased* bullet is rewritten to the final state rather than followed by a "removed" bullet.
5. **Published API keeps compiling.** Public and protected signatures stay; what nothing needs becomes `[Obsolete]` now and is deleted at 7.0. A published behaviour that changes gets a **Behaviour change:** clause in its package's bullet. An internal member a published package calls through `InternalsVisibleTo` keeps its signature (`ConditionalBlocks`, *5c*).
6. **Docs in final state**: guides, `docs/`, READMEs and XML docs describe the code after the step, with no history.

## How every step is tested

- **Baseline first.** Run the step's test projects on `net8.0` and `net10.0` before touching code, and note anything already red.
- **Bugs: a failing test first.** Each finding in *Found while reviewing* gets a test that fails on `wip`, then its fix. A finding whose test does not fail is dropped.
- **Refactors without a behaviour change: pin, then change.** Where no test covers the code (the path helpers, `PgSettings`, `ConditionalBlocks`, `IProcessHelper`'s defaults, `ListAsync`), the pinning tests are written first and stay. Where the existing tests are too coarse, a **temporary** pin records today's output, must still pass after the change apart from the differences the step intends, and is deleted in the step's last commit:
  - *T1*, registrations (*3b*): every builder shape × every `Use*`/`Has*` call, called first and chained after a base method, as a sorted list of service → implementation descriptors.
  - *T2*, Word documents (*5a*, *5f*): the `document.xml` of the loop, collection-table and nested-document scenarios, ids normalized.
  - *T3*, primer order (*2a*): the order in which primers prepare each entry on `ApplyPrimers()` and on `SaveChanges()`.
  - *T4*, concurrency (*2b*): which tokens are compared, and against what, for each token shape in `ConcurrencyTokenTests`.
- **Behaviour changes: the test follows the docs.** A test pinning a removed edge case is deleted, or inverted to pin the documented limitation, so a guide sentence and a test say the same thing.
- **Safety rules get a mutation check.** For a rule that protects data — the committed-row check before a file is deleted (*1a*) and its `IgnoreQueryFilters`, the owner checks on attachment routes (*1e*) — break the rule once and see a test fail.
- **Transactions on real providers.** Steps *1* and *2* also run `tests/Entities.Providers.Testing` with containers (`REGIRA_PROVIDER_TESTS=containers`, as `Regira.runsettings` sets).
- **Docs compile.** `dotnet run --project tools/GuideVerifier -- . --group <group>` for every guide group a step edits.

## Found while reviewing

Not over-engineering, but critical or close to it.

| Finding | Where | Class | Fixed in |
|---|---|---|---|
| The anonymous `auth/password/recover` mails the reset token inside a link built from the caller's `RecoverPasswordInput.SiteUrl` (and a subject from its `SiteName`); `UserControllerBase` builds its confirmation link from `ConfirmEmailUrl` the same way | `Security.Authentication.Web` (published) | Security | Its own change, outside this plan |
| `For<X>(e => e.Filter(…).HasRepository<XRepository>())` ends up with the default `EntityRepository<X>`: the chained call binds to the base builder, which does not register `IEntityRepository<X>`, so `For<>()` registers the default under every interface, last | `Entities.DependencyInjection` (published) | Security, where the repository enforces row rules | *3b* |
| A save another interceptor suppresses still reaches `SavedChanges`, so `AttachmentFileReactor` removes the stored file of a row that was never updated | `Entities.EFcore` | Data loss | *1a* |
| `DictionaryUtility.ToDictionary(…, renameDuplicateKeys: true)` and `DictionaryOptions.RenameDuplicateKeys` never advance their counter: a third equal key loops forever | `Common` | Hang | Step 0 |
| `PgSettings` and `MySqlSettings` split connection strings on `;` and `=`: a password holding either is cut or replaced by the default, and `host:5433` connects to 5432 | `DAL.PostgreSQL`, `DAL.MySQL` | Wrong credentials | *7* |
| `AesEncrypter` and `SymmetricEncrypter` fall back to a key hard-coded in `DefaultSecuritySettings` when no secret is configured | `Common.Security` | Security | Decision 8 |
| `UseDefaults(d => d.ConfigureNormalizing(…))` does not register the `?q=` filter, so a keyword search returns every row | `Entities.DependencyInjection` | Silent wrong result | Step 0 |
| API keys are compared with `==` | `Security.Authentication` | Security (timing) | Step 0 |
| The owner route deletes a replaced attachment only when the link's `AttachmentId` changed; the link route always does | `Entities.EFcore` | Orphaned row and file | *1e* |
| POM's `Save` sets the new id on its request object rather than the caller's payment, drops the pay link, and opens an `HttpClient` per call | `Payments.Pom` | Lost result | Step 0 |
| UziGranot's `QRCodeService` swallows every encoder error but one and saves an image that was never encoded | `Barcodes.UziGranot` | Wrong output | Step 0 |
| `MemoryCacheProvider.Keys` is never filled, so `RemoveAll()` removes nothing | `Caching.Runtime` | Silent no-op | Step 0 |
| The debug log of a MongoDB backup keeps `proxyPassword` | `DAL.MongoDB` | Credential in a log | *7* |
| Smaller: `Printing.GDI` never disposes its `Image` and maps a missing tray to `AutomaticFeed`; `GlobalExceptionHandlingMiddleware` sets the status after the response started and `RequestCultureMiddleware` answers 500 to `?culture=garbage` (*8*); `HasAttachments` ignores its navigation argument; `Margins` reads two values as (left/right, top/bottom); `FilterIsActiveOn(null)` filters where every other `FilterX(null)` does not | various | Minor | Step 0, or a line in the docs |

## 1. Entities — attachment files and reactors

**What exists.** `AttachmentPrimer` stores new content under a key of its own (`KeyBeside`, never over a stored file) and registers work with `SaveOutcomes`, a 171-line per-context registry: undo on failure and finish on success, with rules re-implementing EF's `BatchExecutor` to tell which failed saves EF rolled back (6.5.1), a thread-bound read for synchronous saves (6.5.1), and "abandoned work undone at the next pass". `AttachmentFileReactor` removes replaced and deleted files after the commit (6.5.0) — unless the context lacks the reactor wiring, in which case the primer removes them itself (6.5.0, moved after success in 6.5.1). To skip changes a rollback to a savepoint undid, the reactor interceptor marks every change waiting on the transaction (`MayBeUndone`), telling EF's own savepoint rollback apart through EF's internal `IStateManager.SavingChanges` (6.5.1). For updates and deletes a reactor gets the stored row, which comes from marks kept weakly on EF's internal entries — set by watching `ChangeTracker.Tracked` from the context's first command and again on every pool lease, by `Modify` and by the `Related()` sync — or from a keyed read (6.4.0); a context event drops the marks after a concurrency conflict (6.5.1).

**1a. One remover, after the commit, for a file no row names.** `AttachmentPrimer` only stores. `AttachmentFileReactor` alone removes files, and first reads the attachment by key in its reaction scope, past query filters, keeping the file when the row still names it:

```csharp
var file = change.Kind == EntityChangeKind.Deleted ? change.Entity : change.Original!;
var stillNamed = await db.Set<TAttachment>().IgnoreQueryFilters().AsNoTracking()
    .AnyAsync(x => x.Id!.Equals(change.Entity.Id) && x.Path == file.Path, token);
if (!stillNamed)
{
    await fileService.RemoveFile(file, token);
}
```

That one check replaces the savepoint marking (`MayBeUndone`, `MarkMayBeUndone`, the EF1001 read) and covers what the marking missed: a suppressed save, an abandoned transaction on a reused Npgsql connection. The primer's fallback goes with it (`RemoveOnceDeleted`, `FileReactorRuns`, `HasReactor`, `ReactorDiscovery.IsOf`, its `GetDatabaseValuesAsync` read). The reactor gains a constructor taking the context, which `WithAttachments` registers through a factory; the one-argument constructor stays. **Behaviour change:** a context without `DbContextWiring.Reactors`, or without the reactor, never removes an attachment file; startup validation already warns about such a context, and its message says so.

**1b. A failed save keeps the file it wrote** (Decision 1). `SaveOutcomes` goes, with the `SavedChanges`/`SaveChangesFailed`/`SaveChangesCanceled` overrides and the `BeginSavePass`/`EndSavePass`/`FailedBeforeDatabase` bookkeeping in `EntityPrimerContainerInterceptor` and `EntityPrimerContainer`. `StoredContent` remembers the path holding the content, so a retry of the same instance reuses the file instead of writing another. **Behaviour change:** a refused, conflicting, canceled or failed save with new content leaves one file no row names, where 6.5.0 removed it.

**1c. Stored originals by reading** (Decision 2). Every update and delete of an entity type a reactor covers has its stored row read during the save, one keyed query per entity type, through the existing `StoredRowReader`. The marks, the `Tracked` watch, the pool-lease fields, the `IDbCommandInterceptor` implementation, the conflict handler and the failure hooks with `Discard` go; `EntryMarks` stays for `ArchivablePrimer`. The 2026-09-24 principle — originals from positive evidence, never from flags — holds; only its optimization goes. **Behaviour change:** one primary-key `SELECT` per reactor-covered type per save that updates or deletes it (by default `Attachment`); `Original` is the row as stored at save time.

**1d. Reactor discovery without fallbacks.** `AddReactor*` registers the `IServiceCollection` as `UseEntities()` does, so the `GetServices<IEntityReactor>()` fallback (which misses typed registrations) and the "targets unknown until instantiated" state go; an untyped factory reacts to every entity through its own `CanReact`. The batches of a shared transaction dispatch one by one.

**1e. One per-link update for both attachment routes.** `EntityAttachmentPrepper` and `RelatedAttachmentsPrepper` share an internal `EntityAttachmentContent.PrepareLink(…)` holding `KeepAttachment`, `KeepToOwner`, `CreateFromNewContent` and `ResetTempKey`; each prepper keeps only its own tracking. A test first shows whether the owner route leaves a replaced attachment behind.

**Keeps:** waiting keyed on the `DbTransaction`, with the drops for a started, rolled-back or failed transaction (an abandoned Npgsql transaction must not react at the next commit); the ambient `TransactionScope` path; the depth limit of 8; `SyncOverAsync`; `KeyBeside` and unique keys; `KeepAttachment`/`KeepToOwner` (another owner's file); `StoredContent`; the startup warning for an unwired context.

**Docs.** `docs/attachments.md` *Files and transactions*, `entities.instructions.md` §Attachments step 6, *How reactors run* and *What a reactor receives*, `docs/services.md` *Entity Reactors*, the `AttachmentPrimer` and `AttachmentFileReactor` XML docs and the `InterceptorWiringValidator` message, in one wording:

> A replaced or deleted attachment's file is removed after the commit, by `AttachmentFileReactor`, once no stored row names it. A save that fails, a rollback — to a savepoint too — and a context without the reactor wiring leave a file no row names; an application that needs a clean store sweeps it against the stored `Path` values, as it does for the attachments of a deleted owner.

**Tests** (`Entities.Testing`, `Entities.Providers.Testing`, `Entities.Web.Testing`):
- Add: a suppressed save with the default wiring keeps the stored file (fails on `wip`); a row hidden by a query filter keeps its file; a reload after a failed save stores its new content again; `services.AddReactor<T, R>()` without `UseEntities()` reacts; the same replace gives the same rows and files on both attachment routes.
- Invert to the documented limitation: `Without_The_Reactor_Wiring_New_Bytes_Remove_The_Replaced_File`, `A_Deleted_Attachment_Without_An_AttachmentFileReactor_Has_Its_File_Removed`, and the failed-save group in `AttachmentPrimerTests` (`A_Save_Failing_In_The_Primer_Pass_Removes_The_File_It_Wrote`, `A_Refused_Upload_Leaves_No_File`, `A_Concurrency_Conflict_Leaves_No_New_File`, `A_Canceled_Save_Leaves_No_New_File`, `A_Save_Refused_Inside_A_Transaction_Leaves_No_File`) assert the unnamed file; `CourseAttachmentsControllerTests.A_Refused_Save_Leaves_The_Stored_File` changes its last assertion.
- Delete: the two "explicitly primed, then dropped" tests and `A_Synchronous_Save_Refused_Under_A_Thread_Bound_Scope_…`; in `ReactorTests`, the pool-lease and `Clear()` mark tests.
- Unchanged, as the proof that outcomes hold: the savepoint group (`A_Rollback_To_A_Savepoint_Keeps_The_File_Of_The_Row`, `A_Failed_Save_Caught_Inside_A_Transaction_…`, `…_After_A_Save_An_Interceptor_Ended_…`), `A_Retry_After_A_Concurrency_Conflict_Reports_The_Stored_Row`, the one-query batch tests and `ProviderReactorTests`. The three "without extra reads" tests expect one `SELECT` instead of none.

About 550 lines go; `EntityReactorInterceptor` shrinks from 408 lines to about 220. Order: the fallback half of *1a*, *1b*, the check of *1a*, *1c*, *1d*, *1e*.

## 2. Entities — EF Core write path

**2a. One primer pass.** `ApplyPrimers()` (`EntityPrimerContainer`) and the save interceptor run one internal pass: pending entries grouped once, primers in registration order, end-of-pass hooks once. Today the explicit path orders primers by the entity's type hierarchy, so `ArchivablePrimer` — registered first because it changes entry states — can run after the timestamp and token primers, and a primer implementing two closed `IEntityPrimer<>` runs twice. Pinned by *T3*; add `An_Explicit_ApplyPrimers_Soft_Delete_Moves_The_Token_And_Stamps_LastModified`. Docs: `docs/built-in-features.md` §Primers — "`ApplyPrimers()` runs the primers in registration order, as `SaveChanges()` does."

**2b. A token is a version stamp only when declared** (Decision 3). An undeclared `[ConcurrencyCheck]` token is a data column: written as sent, compared with the stored row. The `Undecided` weak table, `ApplyUndecidedClientTokens` and its two call sites go, and with them the false 409 the guide warns about when a prepper trims or rounds a data column. Startup validation warns about an undeclared `Guid`, `int`, `long` or `byte[]` token: declare it `[VersionStamp]` if the server moves it. **Behaviour change:** an app whose own primer moves an undeclared token stops refusing stale clients until it declares the token. Pinned by *T4*; the test fixtures that relied on detection get `[VersionStamp]`; `An_Update_Abandoned_On_A_Pooled_Context_Does_Not_Reach_The_Next_Request` goes. Docs: `entities.patterns.md` *Optimistic concurrency*, `docs/built-in-features.md`, the `VersionStampAttribute` XML doc.

**2c. Soft-delete bookkeeping in the pass.** The pass from *2a* calls `UndoCascades` once, so `CascadesUndone` and `BeginPass` go. `HasConcurrencyTokenDbPrimer` applies one rule — an update or delete whose token is `Guid.Empty` makes no claim and is compared with the stored token — so the `SoftDeletes` marks two primers share go. **Behaviour change:** a raw `Attach(stub)` edit of an `IHasConcurrencyToken` entity without a token goes through, as a `PUT` without one does, where it answered 409. The two "attached stub without a token is still checked" tests in `StubSoftDeleteTests` flip; `SoftDeleteDependentsTests` stay. `UndoCascades` itself stays (data loss), and so does `RemoveWithoutCascade`.

**2d. Smaller items.**

| Item | Change | Tests |
|---|---|---|
| `RelatedAttachmentsPrepper.ResolveAttachment` re-reads a token-carrying attachment before deleting it, which `HasConcurrencyTokenDbPrimer` already does | Delete; a stub, as for every other type | Add `Removing_A_Link_Of_A_Concurrency_Token_Attachment_Deletes_Its_Attachment` |
| Three conventions, each with its own `IConventionSetPlugin`, options extension and `ExtensionInfo` | One internal `ConventionOptionsExtension<TConvention>`; the public UTC types derive from it | `ConcurrencyTokenMarkerTests`, `ArchivedQueryFilterWiringTests`, `UtcDateTimeConventionTests` unchanged |
| Key and CLR-value helpers written three times (`DeleteCycleExtensions.KeyValues`, `StaleReferenceExtensions`, `ConcurrencyTokenExtensions.ReadClrValue`) | One internal `EntityValues` | `DeleteCycleTests`, `StaleReference*Tests` unchanged |
| The int `Related()` path zeroes negative ids before matching, duplicating `ResetTempKey`; the blueprints' `AdjustIdForEfCore()` runs after the sync and does nothing | Delete the loop; `[Obsolete]` on `AdjustIdForEfCore`; one sentence: a negative key in a `Related()` or attachment collection is a temp key at every depth | `RelatedCollectionTempIdTests` unchanged |
| `AutoTruncatePrimer`, `AutoNormalizingPrimer`, `EntityNormalizerContainer` and `AddRegisteredInterceptors` repeat what the interceptors do (twice when both are wired; the last builds a second service provider) | `[Obsolete(DiagnosticId = "REGIRA0001")]`, removed at 7.0; `AutoTruncateDbContextInterceptor` calls the extension it duplicates | Unchanged until 7.0 |

**Keeps:** `DeleteCycleExtensions` (each branch prevents a crash or a refused valid delete), `StaleReferenceExtensions` (a caller's foreign-key edit silently undone), `ChangeTrackerLog`, the stub read in `HasConcurrencyTokenDbPrimer`, `SyncOverAsync`, and `[VersionStamp(Required = true)]` (see Decision 3).

`Regira.Entities.DependencyInjection` compiles against `Entities.EFcore` internals (`ConcurrencyTokenValidator`), so *2* and *3* ship together.

## 3. Entities — dependency injection and startup checks

**3a. Dead builder code.** Five `Build()` overrides nothing calls, which drifted from `For<>()` (they bypass `UseRepository()`); about 60 commented-out lines in `ComplexEntityIntServiceBuilder`; and four wholly commented-out files under `Mapping/` — all go. `[Obsolete]` on what nothing can reach: `EntityServiceBuilderBase` (an older second fluent API), `EntityServiceCollectionBase`, `EntityIntSearchObjectServiceBuilder`, the six-arity `IEntityServiceBuilder` and `EntityServiceBuilder.Build()`. `void Build();` leaves `entities.signatures.md`. No test calls them; `TestFor1Services`…`TestFor5Services` and `TestUseRepository` keep covering `For<>()`.

**3b. One registration helper.** Each builder shape lists by hand which closed `IEntityService`, `IEntityRepository`, `IEntityManager` and `IEntityReadService` interfaces its `UseEntityService`, `HasRepository`, `HasManager`, `UseReadService` and `UseWriteService` register — about 60 bodies, already inconsistent (`HasManager` registers the service twice) and the cause of the silent repository swap. One `private protected RegisterAs(Type implementation, factory, families)` registers an implementation under every closed interface of the given families whose first type argument is the entity, read from what the type implements, as `EntityRepositoryRegistry.ServiceTypesOf` already does for the defaults. Signatures and `new` re-declarations stay; each body becomes one line. Pinned by *T1*; add the swap test (`e.Filter(…).HasRepository<CourseRepository1>()` resolves `CourseRepository1` for `IEntityService<Course>` and `IEntityService<Course, int>`).

**3c. Startup checks for silent failures only.** A check stays when what it catches is silent: archived rows exposed, primers or reactors that never run, attachment edits ignored, orphaned link rows, overwritten fields. One whose failure is loud at the first request becomes a troubleshooting row:

| Check | Change | Docs |
|---|---|---|
| `ConcurrencyTokenValidator`'s initializer findings — an initialized token, an input DTO that initializes it, an entity that is its own input DTO (instantiation and value heuristics) — and the Info rewrap of every finding for undeclared tokens | Delete; the findings for a token that is not a token, never moves, or a declared stamp a DTO lacks stay | The existing row "Every PUT/PATCH answers 409" |
| `DeleteCycleValidator` | Delete | New row: `DELETE` answers 500 "circular dependency", or SQL Server refuses the migration with Msg 1785 → *An entity that references one of its own children* |
| The Info line about built-in global filters that never run, with `DefaultGlobalFilters`, `TrackDefaultGlobalFilter` and `AddDefaultGlobalFilter` | Skip Regira's own filters; `[Obsolete]` on the two public members | "Regira's built-in filters are not checked" |
| `ArchivableReferenceDataValidator`'s remedy composer | The detection stays; the message names the entities and points at *Soft Delete* | — |

**Behaviour change:** the initializer findings no longer stop a Development host (6.4.0 made two of them Errors); such an app still answers 409 to every update. Tests: the matching `ConcurrencyTokenValidatorTests`, the delete-cycle and inert-built-in tests in `StartupValidationTests` and the remedy tests in `ArchivableReferenceDataValidatorTests` go; the detection and false-positive tests stay; one "built-in filters are never reported" test replaces four.

**3d. Plumbing.** `EntityValidationStartupService` already catches and logs any validator's exception, so the per-validator `try`/`catch` around resolving contexts goes: one lazily resolved list of `(Type, DbContext)` pairs per run, an unresolvable context reported once. The two entity-validator checks merge, and their shared `ConditionalWeakTable` goes; `EntityDtoShapes` loses its own failure channel. Rewrite `Primer_Resolution_Failure_Warns_Instead_Of_Crashing_Startup` and `A_Shape_Source_That_Throws_Is_Reported_Not_Swallowed` to the runner's message; add a test that a throwing validator neither stops the host nor silences the others.

**3e. Smaller items.** One body for the two `UseEntities` overloads; one helper for the four "last explicit configuration wins" blocks; one internal `GetOrAddInstance<T>()` behind `EntityRegistrationLog.For`, `EntityRepositoryRegistry.For` and `DbContextWiringRegistry.For`; one private body for the three `HasAttachments`. The five unpublished `Endpoints()` overloads become one `Endpoints<TBuilder>(…) where TBuilder : IEntityServiceBuilder` over a new non-generic builder interface. One `StartupValidationHarness` replaces six copies of the logger capture and SQLite setup in the tests.

**Keeps:** the per-shape `new` methods that keep the builder's own type (a CRTP base cannot be retrofitted onto the published hierarchy without breaking `HasAttachments` call sites), `EnsureGlobalKeyedFilters`, `UseRepository()` and its checks, the late binding of Web by type name, the licence count, and `AddValidator`'s refusal of a second scope.

## 4. Entities — request layer and web

Everything here but *4g* and *4h* is unpublished.

**4a. One sender** (Decision 4). `EntityRequestExecutor`'s typed path moves into `EntitySender : IEntitySender`: the default-handler map, the closed-handler override, the behaviours and `Duration`. `IEntityRequestExecutor`, `Execute(IEntityRequest)`, `RunUntyped`, `ResponseTypeOf` and the `Regira.Entities.Mediator.MediatR` package go. `entities.patterns.md` replaces *Dispatching through MediatR* and *A mediator library without an adapter* with one section, *Dispatching through a mediator library*, whose recipe needs no untyped execution:

```csharp
public sealed record EntityMessage(IEntityRequest Request, Func<IEntitySender, CancellationToken, Task<object?>> Run) : IRequest<object?>;

public class EntityMessageHandler(IServiceProvider services) : IRequestHandler<EntityMessage, object?>
{
    public Task<object?> Handle(EntityMessage message, CancellationToken token)
        => message.Run(ActivatorUtilities.CreateInstance<EntitySender>(services), token);
}

public class MediatREntitySender(ISender mediator) : IEntitySender
{
    public async Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
        => (TResponse?)await mediator.Send(new EntityMessage(request, async (inner, t) => await inner.Send(request, t)), token);
}
```

`IEntityPipelineBehavior` stays as the in-house way around every operation. Tests: `MediatRAdapterTests`, the MediatR version matrix and `Untyped_Execution_Answers_As_Typed_Execution_Does` go; a test double built like the recipe proves the generated endpoints, `Duration`, a closed handler and a refused write through a replaced sender. The licence lists, routing tables, `Regira-Packages.slnx`, the `Directory.Build.targets` comment and the GuideVerifier groups lose the package; Regira-Tools' `HowToTools.cs` fallback for the `entity-operations` recipe changes with the guide.

**4b. The operations in `Regira.Entities.DependencyInjection`** (Decision 5). `Regira.Entities.Mediator` references nothing but the DI package, which every host calling `For<>()` already has. Its folder moves into the DI project, namespaces unchanged; the `Type.GetType`/`Invoke` hook in `RegisterEntityMediator` becomes a direct call; `TypeForwards.cs` in Entities.Web keeps 6.5.0 code binding. Add a test that `UseEntities()` alone resolves `IEntitySender`.

**4c. No `MapEntity<TEntity>(route)`.** `MapEntityEndpoints()` already leaves controller-served entities to their controllers. `MapEntity` is the one way to map an entity a controller serves, and the served-twice warning, with its second controller discovery, exists for it; both go. A second route is a second `MapEntityEndpoints(o => o.Prefix = "v2")`, which keeps `ClaimDownloadNames` (a duplicate endpoint name fails every request) covered through the test API.

**4d. One input validator.** The DataAnnotations walker learns the non-nullable rule (`NullabilityInfoContext`, cached per type), and `MvcEntityInputValidator`, `IEntityInputValidator` and the reflection hook from the operations package up into Web go. A mapped `POST` then gets the same rules with or without `AddControllers()`; today a host without MVC lets a missing non-nullable string through. **Behaviour change** for MVC hosts: a `PATCH`'s merged input no longer runs app-registered MVC validator providers; DataAnnotations, `IValidatableObject`, entity validators and constraints still apply. Flip `DataAnnotations_Alone_Do_Not_Require_A_NonNullable_String`, delete `An_Mvc_Host_Validates_With_Mvc`, add a mapped `POST` without MVC answering 400.

**4e. Download by name through `AttachmentFileQuery`.** `AttachmentFileByNameQuery` and its handler go; one helper finds the owner's link by name (the lookup keeps `objectId`), and both surfaces send `AttachmentFileQuery`, so one handler override covers both downloads. Add that test on the endpoint surface.

**4f. A `PATCH` body is checked once.** The handler refuses a non-object with an `EntityInputException`; the mapped endpoint binds the body as a `JsonElement`. Add `"{ not json"` and `"[1]"` answering 400 on both surfaces.

**4g. One MVC exception mapping** (published). An internal `EntityExceptionMapping.TryHandle` serves `EntityExceptionFilter` and `[EntityConstraintConflict]`; the attribute goes on both `EntityControllerBase` roots; the `try`/`catch` in `ControllerExtensions.Write` and the three in the attachment controller base go; the filter's dedupe checks `OfType<EntityExceptionFilter>()` only. **Behaviour change:** a `Save`, `Patch` or `Delete` helper called from a controller deriving from neither base, in a host without `MapEntityExceptions()`, lets the exception through as a 500. Keep `Delete_Answers_400_Without_An_Exception_Filter` and `Upload_And_File_Replace_Answer_400_Without_An_Exception_Filter`; add a hand-written action on an entity controller answering 400 without the filter.

**4h. Unused published surface.** `[Obsolete]` on the AutoMapper `AttachmentUriResolver`s — the only reason the AutoMapper adapter references Entities.Web, and through it ASP.NET Core — and on `IMappedEntityServiceCollection` and `MappedEntityServiceCollection`; delete the commented-out `IMappedEntityServiceCollectionOptions.cs`.

**Keeps:** `EntityQueryBinder` (minimal APIs bind no complex type from a query string), the UTC JSON modifier and its `PostConfigure`, the requests in both shapes, the attachment owner checks, `ClaimDownloadNames`, `EntityEndpointMetadata`, the DTO-pair requirement, `TypeForwards.cs` and the walker's cycle guard.

## 5. Office — Word

Everything here but *5c* and *5e* is unpublished.

**5a. Copies repeat ids, apart from drawings.** Each backend got its own fix for the ids a copy repeats: Word.Spire renumbers content controls in the model and VML ids after saving; Word.Syncfusion rewrites the saved package on every save (`PackageIds`); Word.Aspose renames copied shapes, which shows in Word's Selection Pane; Word.Mini renumbers `w:id` and runs `VmlShapeIds`. `VmlShapeIds`, `PackageIds` and the per-backend renumbering go. Copies keep no bookmark (every backend), and Word.Mini still renumbers `wp:docPr`, which it writes as raw XML. Content-control ids and VML ids repeat, as they have in collection-table rows and nested documents since before 6.5.0. **Precondition:** open in Word, once, a loop copying an embedded object and a content control; if Word reports unreadable content for one of them, that one rule stays, as a single shared pass. Pinned by *T2*. Delete `A_Loops_Copies_Of_An_Embedded_Object_Name_Their_Own_Shapes` and `A_Repeated_Shape_Id_Is_Made_Unique_With_What_Names_It`; `A_Loops_Copies_Keep_Ids_Unique` asserts one bookmark and unique drawing ids. Docs (`office.word.instructions.md` *Loop Blocks*, `docs/word/README.md`): "A copy keeps no bookmark and its pictures take drawing ids of their own; content-control ids and the ids of legacy VML shapes repeat in each copy, as in a collection table's rows." About 330 lines.

**5b. `MergeOptions.FollowOn`** (Decision 6). No caller in the repository. It costs `MergeOptions`, a default interface method on `IWordMerger`, `MergeJoint`, three `Merge` and three `MergeDocuments` overloads and three `DocumentBuilder.WithMerge`. If it goes: each backend's joint is `previousIsPadded || isPadded ? OddPage : NewPage`, keeping the `EnforceEvenAmountOfPages` fix; the guide points at nested documents for running one document on inside another; `Merged_Documents_Follow_On_From_The_Previous_Page_When_Asked` and the `true` case of `DocumentBuilder_Joins_Loaded_Inputs_As_Merge_Does` go.

**5c. `ConditionalBlocks` as an adapter.** The published Word backends call `ConditionalBlocks.OpensBlock`, `ContainsMarker` and `Resolve(IReadOnlyList<string?>, WordTemplateInput)` through `InternalsVisibleTo`, so the class stays — as a 35-line adapter over `TemplateBlocks` instead of a 307-line copy that, since this cycle, no test covers. Same signatures; a loop marker among blocks throws `FormatException`, as 6.5.0 would. Add three tests: `OpensBlock("{{#each Items}}")` is false; `Resolve` drops the right indices of a nested `if`/`else`; a `{{#each}}` among blocks throws.

**5d. Word.Mini refuses keys MiniWord cannot read.** MiniWord compiles a regular expression from every key; 6.5.1 hides that behind generated `regira_{guid}` names and a tag rewrite. Instead, 6.5.0's key handling stays, with a guard that throws `ArgumentException` naming the key when a global key or image name holds `.` or one of `\ ^ $ | ? * + ( ) [ ] { }`, or a collection's key or field (a loop's too) is not letters, digits and `_`. **Behaviour change** from 6.5.0: a global key holding `.` or `(` throws, where it worked when no other tag collided. Docs: *Word.Mini limits* in both layers. Tests: the literal-key tests in `MiniTests` go; `MiniTests` overrides `A_Parameter_Key_Is_Matched_Literally` to expect `ArgumentException`; add `Create_Rejects_A_Key_MiniWord_Reads_As_A_Pattern` (`x[1`, `a.b`, `Total (EUR)`, a field `Unit Price`). About 100 lines.

**5e. Shared copies.** One `NestedDocumentGuard` in `Regira.Office` (the three are identical below their namespace line); one private `AddStory` per backend behind the `protected internal` `AddHeader` and `AddFooter`; optionally one `SpansTextWidth` beside `PictureScaling`. Existing tests cover them.

**5f. No placeholder fill.** A row's fields are filled directly; the two-pass fill through U+E000 placeholders goes. Markers stay safe, since every marker is read before any field is filled; a row value holding another field's `{{tag}}` may be filled, as a global or collection-table value already may. Delete the two "written as it is" tests; keep `A_Row_Value_Holding_A_Marker_Is_Written_As_Text`.

**5g. Word.Gotenberg scales top-level drawings.** A picture in a text box, or the shapes of a group or canvas, keep their size while their holder scales; the cap, the scale map and the canvas offsets go. No other backend scales nested drawings at all. Rewrite `PageSetup_Scales_A_Picture_In_A_Text_Box_Once`; delete the box and canvas tests.

**5h. Lighter tests.** The 22-inch clamp scenarios run 2 cases per backend instead of 6 and 5 (`PictureScalingTests` already sweeps the arithmetic); the `TemplateMarkers` limit tests run 2 instead of 6, without the GC-allocation assertions or the test of `System.IO.Compression`; one formatting test per block kind.

**Keeps:** marker rows and row-level content controls; nested loops over a row's list, JSON included; `{{else}}` in loops; the footnote and comment refusal; `TextBoxFallbacks`' text sync (the VML copy would keep tags and dropped branches); `PictureScaling` and the 22-inch clamp (crash); the odd-page padding and `fontReference`; `TemplateMarkers`' 32 MiB and 1,000-part bound (untrusted uploads); `SourceFormat`. A shared pipeline base for the three full backends was considered and left out: about 25 abstract members for little gain.

## 6. Office — Excel, PDF and barcodes

| Item | Change | Tests |
|---|---|---|
| MiniExcel's workbook-wide `DateFormat` (6.5.1; 6.5.0 declared the option and ignored it) | MiniExcel ignores it again; `[Obsolete]` on `Options.DateFormat`, pointing at ClosedXML | Delete the two `DateFormat` tests; keep `Create_Without_DateFormat_Keeps_Excels_Date_Format` |
| `SheetHeaders` reserves literal header names in a second pass (6.5.1) | One greedy pass: a blank header is `Column{n}`, a repeat the first free `{header}_{n}`, case-insensitive | Rewrite the expected keys of `Run_Read_Blank_And_Repeated_Headers` |
| `SheetRows` keeps the first of two keys differing only in case within one row and drops the other's value (6.5.1) | Throw `ArgumentException`, as MiniExcel did in 6.5.0 | Split `Run_Create_Keys_Differing_By_Case`: across rows one column, within a row a throw |
| NpoiMapper writes untyped object rows through Npoi.Mapper, unlike every other backend (published) | Always `FillSheet(SheetRows.ToDictionaries(…))`; the typed manager keeps Npoi.Mapper. **Behaviour change:** untyped object rows ignore Npoi.Mapper attributes | Add `NpoiMapperTests.Create_Nested_Object` |
| UziGranot carries `QRCodeTrace.cs` (all comments), the vendor's XML doc file, commented-out package references and an unused `QRCodeCommandLine` | Delete the first three; `[Obsolete]` on `QRCodeCommandLine`; the README says why the library is vendored (its decoder's own package targets .NET Framework only) | `UziGranotTests` unchanged |
| The PDF tests' `PdfFixtureAttribute`, an NUnit-internal fixture builder, with `[Needs]` attributes | `Service<T>()` with `Assert.Ignore`, as the barcode tests do | PDF.Spire shows 15 ignored cases |

A policy, if you agree: the deprecated backends (Excel.EPPlus, PDF.DocNET) take crash and data-loss fixes only; any other gap becomes an ignored test and a line in their docs.

## 7. Platform — data, storage, system and common

| Item | Change | Behaviour | Tests |
|---|---|---|---|
| SQL Server deletes a backup this process cannot read on the server (`xp_delete_files` or `xp_delete_file`, read back with `xp_fileexist`, `sysadmin` needed) and warns about files an overwritten database leaves | Delete both; the `IOException` names the file left in `BackupDirectory` | Misconfiguration path only; the restore's data protections stay | Rewrite `A_Backup_This_Process_Cannot_Read_Is_Removed_From_The_Server` |
| `List` and `ListAsync` are separate implementations on four storage backends, already diverged on the file system | One enumeration per backend; `ListAsync` yields what `List` returns | The file system's `ListAsync` gets `List`'s folder filter | One parity test replaces five `*_Async` twins |
| `BackupRestoreManager` repeats `PgBackupService` and `PgRestoreService` | Delegates to internal `DumpTo` and `RestoreFrom` | None | Add two manager tests |
| Three copies of the `/`-path `FileNameUtility` (Azure, GitHub, SSH) | One internal helper in `Common.IO.Storage`; the copies forward | None; the SFTP containment check stays | Add `UrlPathTests`, `../` refused |
| `PgSettings` and `MySqlSettings` parse and build connection strings by hand | The drivers' connection-string builders, as `SqlServerSettings` does | Canonical keys; fixes the password and port findings | Add `PgSettingsTests`, modelled on `SqlServerSettingsTests` |
| `IProcessHelper.ExecuteCommand(command, environment)`'s default writes `set` lines — secrets into a script — for an implementation that does not override it | Throws `NotSupportedException`, as the `ExecuteFile` default does | Custom implementations only | Add `IProcessHelperDefaultsTests` |
| `NormalizingUtility` locks around concurrent dictionaries, keeps a cache it never reads, and rebuilds two tables per character | `GetOrAdd`, no locks, static tables | None | Normalizing tests unchanged |
| MongoDB builds a redacted URI from a deny-list for its debug log | Log the host and the database | Less in the log | Add a `proxyPassword` case |
| Optional: `NetworkShareCommunicator` reference-counts connections process-wide, untested | One connection per instance, documented as one singleton per share and user | Two communicators on one share disconnect each other | A manual test on a share |
| Unused, undocumented public helpers in `Common`, `System.Hosting` and the `DAL` packages (listed in the review) | `[Obsolete]`; delete the all-comments `GuidConverter.cs` | Warnings only | `TypedServiceProviderTests` gets a `CS0618` pragma |

**Keeps:** the MongoDB URI round trip and its tool configuration file, `ProcessHelper`'s internals, the SQL Server and PostgreSQL restore protections, `ContentTypeUtility`, the 6.5.0 zip path work, `ZipManager`, the SFTP fingerprint and `GitHubService`'s SHA cache.

## 8. Security, web, media and the rest

| Item | Change | Behaviour | Tests |
|---|---|---|---|
| RazorLight keeps a second engine per parser for `TemplateKey`; RazorEngineCore keeps two caches and two compile paths (6.5.1 caching) | `TemplateKey` ignored and `[Obsolete]`; RazorEngineCore compiles every template against `HtmlEncodingTemplateBase` with an `Encode` switch | A keyed RazorLight parser renders the template it is given | Rewrite the two `TemplateCompilationTests` on keys and modes |
| Drawing.GDI finds encoders through a reflection map, MIME strings and a locked dictionary, and rotates in 176 lines | `Lazy<ImageCodecInfo[]>` and a GUID match; rotate with `RotateUtility.CalculateSize`, as Skia does; `[Obsolete]` on `ImageEncoders`, `KnownImageFormats` and `PrintUtility` | The canvas may be 1 px smaller at angles that are not right angles | Add `RotateImage45` to `ImageTestsBase`, and a layer-rotation test |
| OIDC copies 13 cookie options field by field; the Entra options and three `Require` helpers are duplicated; superseded OpenAPI transformers, `Basic` forward rules and `RefreshOptions` remain | One internal cookie-scheme builder; a shared Entra base; one helper; `[Obsolete]` on the rest | None | Add a test that OIDC's cookie settings reach the cookie scheme |
| `LicenseValidator.Validate` and `GetStatus` walk the same chain separately | One private `Evaluate` | None | `Status_Agrees_With_Validate` stays |
| Mollie's `Abstractions/` folder (excluded from compilation); unused Billit models and result types; two copies of `UblDocumentUtility` and `PeppolNamespaces` | Delete the folder; `[Obsolete]` on the rest; one copy in `Common.Invoicing` | None | — |
| Common.Web's own exception and culture middleware, `IRequestHandler` and `ServiceCollectionWrapper` | `[Obsolete]`, pointing at `AddProblemDetails()`/`UseExceptionHandler()` and `UseRequestLocalization()` | Warnings | — |
| `SymmetricEncrypter` (a deterministic IV, a key byte short), `SimpleHasher`, `GetRfc2898DeriveBytes` | `[Obsolete]`; `SymmetricEncrypter` stays for decrypting | Warnings | Add a test decrypting a fixed 6.5.0 ciphertext |
| Optional: bot rules reload live from a watched file; GeoIP2 resolves a path against two roots, as a file or a folder | Rules read at start-up; `DatabasePath` names the file | A restart applies rule edits; folder paths stop resolving | Delete the hot-reload and folder tests |

**Keeps:** the scheme selector and its forward rules, the security-scheme descriptors, claims normalization, refresh-token rotation, Entra issuer validation, the controller bases' 6.5.0 security fixes, `NamedFileResponse`, the parsers' `HtmlEncode` opt-ins, and the analytics pipeline and its bot lists.

## Decisions

1. **An attachment file written by a save that fails.** (a) It stays, unnamed: `SaveOutcomes` and its hooks go. (b) It is removed only when the save ran without a caller's or an ambient transaction: the hooks stay, the transaction-shape rules go. (c) 6.5.1 as it is. Recommendation: (a). It matches keeping a deleted owner's attachments for a sweep, and nothing destroys data on a failure path. An upload to a missing owner is the one failure a client can repeat at will; if that matters, the upload handler can check the owner before storing, in a few lines.
2. **Stored originals for reactors.** (a) Always read: one keyed query per covered type per save. (b) Keep the marks `Modify` and the `Related()` sync set, and drop only the query watch and the pool-lease handling (about 55 lines instead of 150). Recommendation: (a).
3. **Concurrency tokens.** (a) Only declared stamps are compared with the client's value, with a startup warning for undeclared tokens. (b) Keep the implicit detection. Recommendation: (a). Replacing `[VersionStamp(Required = true)]` with a validator recipe was also reviewed and is not recommended: it removes a protection applications opted into.
4. **The MediatR adapter and the executor split**, built in 6.5.1. (a) Drop them and document the recipe. (b) Keep them. Recommendation: (a). The mediator proposal's own rule was to ship an adapter only for a library consumers ask for, and adding one back later is additive.
5. **Where the entity operations live.** You chose a separate package "for now". (a) Move them into `Regira.Entities.DependencyInjection`. (b) Keep `Regira.Entities.Mediator`. Recommendation: (a): no host gains from the separation, and the reflection hook goes with it.
6. **`MergeOptions.FollowOn`.** You made following on the opt-in on 2026-10-06. (a) Drop it before it ships. (b) Keep it. Recommendation: (a), unless one of your applications merges documents to run on; it is the one 6.5.1 Word feature with no caller.
7. **The version line.** Several steps change published behaviour, and new `[Obsolete]` markers ship. The family sits at an unpublished 6.5.1; whether it stays a patch or becomes 6.6.0 is yours.
8. **The hard-coded default encryption key.** Requiring a configured secret for new encryption breaks applications that rely on the default; keeping the default for decryption keeps their data readable. It needs a small design of its own.

## Steps

Each step ends with its test projects green on both target frameworks, its guide groups verified, and its docs and changelog bullets in the same commit. Changelog bullets stay short: what changed for a consumer, the behaviour change, and the doc that explains it. Before a step that rewrites shared files, check `git status` for other sessions' work.

0. **Baseline and findings.** Run the full suite once and record the result. Fix the Step 0 findings, each with a failing test first: `DictionaryUtility`'s counter, the `?q=` filter with `ConfigureNormalizing`, the API-key comparison, POM's `Save`, UziGranot's swallowed error, `MemoryCacheProvider.Keys`, Printing.GDI. The password-reset finding goes to a change of its own.
1. **Unpublished work, before 6.5.1 ships.** Word *5a* (after the Word check), *5b*, *5d*, *5f* and *5g*; the three 6.5.1 rows of *6*; the request layer *4a* to *4f* and the `Endpoints()` overloads of *3e*; the Razor caching row of *8*. Their *Unreleased* bullets are rewritten to the final state. Tests: `Office.Word.testing` (the Gotenberg fixtures with containers), `Office.Excel.Testing`, `Entities.Mediator.Testing`, `Entities.Web.Testing`, `Entities.DependencyInjection.Testing`, `Web.HTML.Testing`.
2. **Attachment files and reactors**, *1a* to *1e*, in the order given there. Tests: `Entities.Testing`, `Entities.Providers.Testing` with containers, `Entities.Web.Testing`.
3. **Write path and dependency injection**, *2* and then *3*, shipped together, with the temporary pins *T1*, *T3* and *T4*. Tests: `Entities.Testing`, `Entities.DependencyInjection.Testing`, `Entities.Web.Testing`, `DAL.EFcore.Testing`.
4. **The rest.** First the items without a behaviour change — *4h*, *5c*, *5e*, *5h*, the UziGranot and PDF-test rows of *6*, and the refactors and `[Obsolete]` rows of *7* and *8* — then those with one: *4g*, NpoiMapper's untyped rows, and the remaining rows of *7* and *8*, the optional ones only if taken.
5. **Wrap-up.** The full suite, every GuideVerifier group, and the routing tables, licence lists, `Regira-Packages.slnx` and Regira-Tools fallbacks checked against the final package list.

## Risks

- **Applications leaning on a removed edge case**: implicit version stamps (*2b*), a context without reactor wiring deleting files (*1a*), MVC validator providers on `PATCH` (*4d*). Each is named in its changelog bullet, and the startup checks name the first two.
- **Word's tolerance for repeated ids.** *5a* rests on Word accepting repeated content-control and fallback VML ids, as it does in collection tables; its precondition checks that in Word itself.
- **Internals published packages bind to.** The Word, Excel and Entities backends call `Regira.Office` and `Entities.EFcore` internals through `InternalsVisibleTo`. A changed signature fails at run time, so those families ship on one number, and `ConditionalBlocks` keeps its 6.5.0 members.
- **Other repositories.** The MCP knowledge base is built from these guides; Regira-Tools carries fallbacks for some `how_to` recipes (`entity-operations` among them), and Regira-Modules generates templates from guide content. A guide change in *1* or *4a* is checked against both.
- **Parallel sessions.** The main checkout is shared; each step starts from `git status` and keeps the build compiling.

## Outcome

### Step 0

Built on `wip` on 2026-10-12 and committed as `80f3f9e`, at the family's unpublished 6.5.1 (no `<Version>` changed).

**Baseline.** The full suite at `cb565fc`, plus the uncommitted password-reset change, run offline (`--filter "TestCategory!=Network"`, `Regira.runsettings`, so the container fixtures ran): every project green on `net10.0`, and `Entities.Testing` on `net8.0` too. `dotnet test --artifacts-path` moves the output folder, and the ten projects that find their assets relative to it fail then; run those in place.

**Fixed, each with a test that failed first:**

- `DictionaryUtility.ToDictionary(…, renameDuplicateKeys: true)` advances its counter. The `DictionaryOptions.RenameDuplicateKeys` loop got the same one-character fix without a test: reflection returns only the most derived of several properties with one name, so that loop is never reached.
- `UseDefaults(d => d.ConfigureNormalizing(…))` registers `FilterHasNormalizedContentQueryBuilder` (`TestWithoutDbContext`).
- POM's `Save` sets the caller's `Id`. The new `CreatePaylink` also returns the link, a constructor takes the `HttpClient`, the default one is shared with a two-minute connection lifetime, and `GetPomClient` is `[Obsolete]` (`PomTests`, against a stubbed handler). From the step-0 review: a token POM refuses with `401` is renewed and the request sent once more, where it was cached for the instance's life.
- `MemoryCacheProvider.Keys` reads `MemoryCache.Default` under the prefix, so `RemoveAll()` works (`CacheProviderTests`).
- Printing.GDI uses the default tray when none is asked for, through an internal `SelectPaperSource` (`GdiPrintServiceTests` in `Drawing.Testing`).
- Found while writing the cache test: `CacheProviderBase`'s enumerator started at index 0 and incremented before its first read, so enumerating any provider skipped an entry. Fixed by the same test.

**Fixed without a failing test:**

- The API-key comparison: SHA-256 of both keys through `CryptographicOperations.FixedTimeEquals`, every key checked. Timing does not show in a unit test, so tests pin the matching instead: whole key, case, a key-less owner, the first of two equal keys.
- Printing.GDI disposes its `Image`. Seeing the leak needs a print job.

**Dropped:** UziGranot's swallowed error. The encoder assigns only Numeric, AlphaNumeric and Byte, so its one other `ApplicationException` (*Encoding mode error*) cannot be reached. If it were, `SaveQRCodeToPngFile` would throw *must be encoded first* rather than save an image.

**Not taken in step 0:** the rest of the *Smaller* row. `HasAttachments` goes with *3e*, the middlewares with *8*. The two-value `Margins` order and `FilterIsActiveOn(null)` change published behaviour and wait for a fix-or-document call. A new finding of the same kind: Printing.GDI passes `Margins` to `System.Drawing.Printing.Margins` unconverted, in hundredths of an inch, where `Regira.Office.Models.Margins` documents points.

**Tests after the step:** green on `net10.0` and `net8.0` for `Common.Testing` (on `net8.0` without `CollectionTests`, which calls a .NET 10 API), `Entities.DependencyInjection.Testing`, `Payments.Testing`, `Drawing.Testing`, `Entities.Testing` and `Web.Security.Testing`; `Entities.Web.Testing` and `Entities.Mediator.Testing` on `net10.0`. GuideVerifier group `commerce` compiles.

### Step 1

Built on `wip` on 2026-10-12 and committed as `25f1905` (Regira-Tools `cbe6801`), at the unpublished 6.5.1; no `<Version>` changed. Left out on your answers: *4b* (decision 5: the package stays) and *5a* (skipped for now, so each Word backend keeps its own id renumbering).

- **4a.** `EntitySender` runs the typed path itself; `IEntityRequestExecutor`, `EntityRequestExecutor`, the untyped `Execute` and `Regira.Entities.Mediator.MediatR` are gone. `entities.patterns` has one *Dispatching through a mediator library* section with the three-type recipe; `MediatorRecipeTests` runs that recipe against MediatR 14, a test-only dependency now. Licence lists (nine commercial packages), routing tables, `Regira-Packages.slnx`, `Directory.Build.targets`, GuideVerifier groups and `legal/REGIRA-COMMERCIAL-LICENSE.md` lost the package. Regira-Tools' `entity-operations` fallback follows the guide (it had also missed `PriceImporter` from `cb565fc`); McpServer 466 green.
- **4c.** `MapEntity<TEntity>(route)`, the served-twice warning and its controller discovery are gone; a second `MapEntityEndpoints(o => o.Prefix = "v2")` maps the set again, and the test API does that for `ClaimDownloadNames`.
- **4d.** `DataAnnotationsEntityInputValidator` applies MVC's implicit `[Required]` (via `NullabilityInfoContext`), and is the one input check: `IEntityInputValidator`, `MvcEntityInputValidator` and the reflection hook are gone. A mapped `POST` without MVC refuses a missing non-nullable string (new test); behaviour change for MVC hosts in the changelog.
- **From the step-1 review (4d).** An object missing a required property is not asked to validate itself, as MVC does not ask it: its `IValidatableObject.Validate` threw on the missing value, a 500 where 6.5.0 answered 400. `[ValidateNever]` (matched by name) leaves a property out. Both had a failing test first.
- **4e.** `AttachmentFileByNameQuery` and its handler are gone; both surfaces find the link with one helper and send `AttachmentFileQuery`. `One_File_Handler_Answers_Both_Downloads` failed on the endpoint surface first.
- **4f.** The patch handler refuses a non-object with an `EntityInputException`; the mapped endpoint binds a `JsonElement`. `"{ not json"`, `"[1]"` and a string answer 400 on both surfaces.
- **3e, `Endpoints()`.** One `Endpoints<TBuilder>() where TBuilder : IEntityServiceBuilder`, over a new non-generic `IEntityServiceBuilder` (`Services`, `EntityType`) that the generic one implements by default members, so published implementers compile unchanged.
- **5b.** `MergeOptions`, the `IWordMerger` default method, the options overloads and `WithMerge` are gone; the joint is a one-line `MergeJoints.StartsOnOddPage`, the padding fix kept. The guides point at nested documents.
- **5d.** Word.Mini is back on 6.5.0's key handling (`GetMiniValue`, `TrimTags`), with the guard; the generated names, `FieldName` and the spaced-dot field tags are gone. After the step-1 review the guard is one rule for every key, collections included: MiniWord finds a row's tags as text, so a space never broke a collection key or field (`{{ Order Lines.Unit Price }}` fills, under a new test), and the letters-digits-`_` rule refused more than it needed to.
- **5f.** Row fields are filled directly; the placeholder pass and its two tests are gone.
- **5g.** Word.Gotenberg scales top-level drawings only; the cap, the scale map and the canvas offsets are gone, and the 22-inch clamp stays.
- **6.** MiniExcel's `Options.DateFormat` is `[Obsolete]` and ignored; `SheetHeaders` is one greedy pass; a row holding keys that differ only in case throws on every backend.
- **8, Razor.** RazorLight's `TemplateKey` is `[Obsolete]` and ignored; RazorEngineCore compiles once against `HtmlEncodingTemplateBase` with an `HtmlEncode` switch.

**Tests after the step:** green on `net10.0` and `net8.0` for `Entities.Mediator.Testing`, `Entities.DependencyInjection.Testing`, `Entities.Testing`, `Web.HTML.Testing`, `Office.Excel.Testing` and `Office.Word.testing` (Gotenberg fixtures with containers); `Entities.Web.Testing` on `net10.0` only, as its test API uses .NET 10 OpenAPI. Solution build clean; GuideVerifier 20 groups, 312 snippets.

### Step 2

Built on `wip` on 2026-10-12 and committed as `c88a7bf`, at the unpublished 6.5.1; no `<Version>` changed. Decisions 1 and 2 as recommended. Production code: +271 −822 lines.

**Baseline** at `25f1905`, green: `Entities.Testing` on `net8.0` (575) and `net10.0` (578), `Entities.Providers.Testing` with containers (36), `Entities.Web.Testing` (277), `Entities.DependencyInjection.Testing` (136).

**A test that failed first, then the fix:**

- *1a.* `A_Suppressed_Save_With_The_Reactor_Keeps_The_Stored_File`: with the default wiring, a save another interceptor suppressed removed the stored file of a row that was never updated.
- *1d.* `A_Reactor_Added_Without_UseEntities_Reacts`: `AddReactor<Invoice, InvoiceReactor>()` on a context wired by hand never ran.
- *1e.* `A_Replaced_Attachment_Goes_On_Both_Routes(true)`: the owner route left the replaced attachment row and its file behind when the link kept its `AttachmentId`; the link route already passed.

**Built:**

- **1a.** `AttachmentPrimer` only stores. `AttachmentFileReactor` reads the attachment's row by key, past query filters, in its reaction scope, and keeps a file the row still names. Gone: the savepoint marking (`MayBeUndone`, `MarkMayBeUndone`, the EF1001 `IsSaving`) and the primer's fallback (`RemoveOnceDeleted`, `FileReactorRuns`, `HasReactor`, `ReactorRegistration.IsOf`, its `GetDatabaseValuesAsync`). A new constructor takes the `DbContext`; `WithAttachments` registers it through a factory with `TContext`, and the one-argument constructor is `[Obsolete]` and removes without reading. A factory registration names no implementation type, so the startup validator recognizes the reactor by the attachment type it is registered for; its message says that without the wiring no file is removed.
- **1b.** `SaveOutcomes` is gone, with the save hooks in `EntityPrimerContainerInterceptor`, `BeginSavePass`/`EndSavePass`/`FailedBeforeDatabase` and the `try`/`catch` around `ApplyPrimers()`. `StoredContent` keeps the path beside the content: a retry of the same instance saves the file it wrote, and an attachment reloaded to its stored path stores its content again. A new row's retry is recognized per tracking, through an `EntryMarks` in the primer, so a copy added with loaded bytes still gets a file of its own.
- **1c.** Every update and delete of a reactor-covered type is read through `StoredRowReader`. Gone: `StoredOriginalsExtensions` and its two callers, the `Tracked` watch, the pool-lease state, `IDbCommandInterceptor`, the conflict handler and the failure hooks with `Discard`; what a save captured is replaced when the next save starts. `EntityReactorInterceptor` went from 408 to 216 lines.
- **1d.** Every `AddReactor` registers the `IServiceCollection`. Gone: the `GetServices<IEntityReactor>()` fallback, `ReactorDiscovery.Resolved`, the unknown-targets state and `RegisteredReactor`; an untyped factory reacts to every entity through its own `CanReact`. The batches of a shared transaction dispatch one by one.
- **1e.** `EntityAttachmentContent.PrepareLink` holds the temp key, `KeepToOwner`, `KeepAttachment`, the attachment new content goes to and `CreateFromNewContent`, and returns the attachment a new one replaces; each prepper keeps its own typing, tracking and `ApplyBeforeSave`. The owner route deletes that attachment whenever one replaces it (with `IsStrictRelation`, as before).

**Mutation checks**, each rule broken once: without the stored-row check, the suppressed save and the savepoint group fail (5 tests); with the query filters applied, the hidden-row test fails; with content compared without its path, the reload test fails; without the mark for a new row, the retried insert fails.

**Tests.** Added: the three above, `A_Rollback_To_A_Savepoint_Keeps_The_File_Of_A_Row_A_Query_Filter_Hides`, `A_Retried_Insert_After_A_Refused_One_Saves_The_File_It_Wrote`, `A_Reload_After_A_Refused_Save_Stores_The_New_Content_Again`. Inverted to the documented limitation: the failed-save group, `A_Retried_Save_After_A_Refused_One_Saves_The_File_It_Wrote`, `Without_The_Reactor_Wiring_New_Bytes_Leave_The_Replaced_File`, `A_Deleted_Attachment_Without_An_AttachmentFileReactor_Keeps_Its_File`, the file count of `A_Failed_Save_Caught_Inside_A_Transaction_…` and the last assertion of `A_Refused_Save_Leaves_The_Stored_File` on both web surfaces. Deleted: the two "explicitly primed, then dropped" tests, the suppressed save without the reactor wiring, the thread-bound scope test, `A_Pooled_Context_Uses_What_It_Loads_On_Every_Lease`, `Clearing_The_Tracker_Forgets_That_Modify_Loaded_The_Stored_Row` and the `fileReactor: false` cases of the stub and refused deletes. The three "without extra reads" tests expect one `SELECT`.

**From the step-2 review:**

- *Shared attachments* (data loss, partly 6.5.0). Two links of one owner can share an attachment, since a new link may name one its owner links already. Removing one of them — on the owner's save, or `DELETE {objectId}/attachments/{id}` — or re-filing it — `PUT {objectId}/files/{id}`, and since *1e* the owner's save from server code — deleted the attachment and, through the cascade, the other link and its file. Now an attachment goes with a link only when no other link names it: `EntityAttachmentContent.IsLinkedElsewhere`, a keyed read of the stored links past query filters, on both routes, and on the owner's route also the owner's incoming links, judged once every link is prepared. Five tests failed first; breaking either guard fails a test. `HasStrictRelation` is documented.
- The synchronous capture runs inline on the saving thread, so its read stays inside a `TransactionScope` without async flow; on a lock-based database the thread-pool read could wait on the scope's own locks. Its test failed first.
- The docs say "once the attachment's own row no longer names it", that two rows naming one path share the file, that `WithAttachments` is called once per attachment type (a second call adds a file reactor reading the other database), and that composite, converted or owned keys are read a row at a time.
- The changelog names, as behaviour changes, the untyped factory reactor that now receives every change, the file reactor's registration without an `ImplementationType`, and a stored link's `NewBytes` on the owner's save when its attachment was not loaded (a new attachment, where the bytes were ignored); and that a reactor registered by type gets the obsolete constructor.
- `A_Pooled_Context_Reports_The_Stored_Row_On_Every_Lease` is back, with one `SELECT` per lease; a stale comment in `WithAttachments` is fixed.

**Docs.** `docs/attachments` (*Files and transactions*) and `entities.instructions` §Attachments step 6 lead with the one wording above; *What a reactor receives*, *How reactors run*, *Unwired interceptors*, `docs/services` (*Entity Reactors*), the `WithAttachments` signature comment and the XML docs follow. `ai/learnings.md`: the entries on originals, conflicts, failed saves and destroying reactors state the new rule. Regira-Tools needs no change: the `after-commit` fallback comes from `entities.patterns`, untouched.

**Tests after the step and its review:** green on `net10.0` and `net8.0` for `Entities.Testing` (584, 581); `Entities.Providers.Testing` with containers (36), `Entities.Web.Testing` (277) and `Entities.DependencyInjection.Testing` (136) on `net10.0`. Solution build without new warnings; GuideVerifier 20 groups, 312 snippets.

### Step 3

Built on `wip` on 2026-10-12 and committed as `804b170`, at the unpublished 6.5.1; no `<Version>` changed. Decision 3 as recommended. Production code: +602 −1781 lines of C#.

**Baseline** at `c88a7bf`, green: `Entities.Testing` on `net8.0` (581) and `net10.0` (584), `Entities.DependencyInjection.Testing` (136), `Entities.Web.Testing` (277), `DAL.EFcore.Testing` (8).

**A test that failed first, then the fix:**

- *2a.* `An_Explicit_ApplyPrimers_Soft_Delete_Moves_The_Token_And_Stamps_LastModified` (an explicit pass stamped the delete before the `ArchivablePrimer` made it an update) and `A_Primer_For_Two_Types_Of_An_Entity_Prepares_It_Once` (it ran twice).
- *2b.* `A_Data_Column_Token_A_Primer_Rewrites_Takes_The_Clients_Edit`: a primer trimming a `[ConcurrencyCheck]` column answered 409. `An_Undeclared_Token_A_Primer_Moves_Is_A_Data_Column` pins the behaviour change.
- *2c.* The two attached-stub tests of `StubSoftDeleteTests` flipped, on both save paths.
- *3b.* `A_Repository_Chained_After_A_Base_Builder_Method_Is_Kept`: `For<Course>(e => e.Filter(…).HasRepository<CourseRepository1>())` resolved the default `EntityRepository<Course>`.

**Pins**, temporary, deleted before the commit:

- *T3*, primer order: the explicit path went from type order to registration order and the two-interface primer from twice to once; the save path did not change.
- *T4*, concurrency: unchanged for every declared shape (the test fixtures that relied on detection got `[VersionStamp]`); the undeclared shapes are pinned by the two *2b* tests.
- *T1*, registrations (70 cases: five shapes × every `Use*`/`Has*`, first and chained after `SortBy`): the differences are the swap fixed in every chained case; a factory registered once, where up to five times; the `int` shape's factory repository and manager also under `IEntityRepository<X>` and `IEntityManager<X>`; a complex shape's factory service also under its five-type `IEntityService`; and every read service also under the five-type read interface it implements.

**Built:**

- **2a.** `PrimerPass` runs both paths: groups once, primers in registration order, the cascade undo once before the `ArchivablePrimer`'s first delete, `EndPass` once. `CascadesUndone` and `BeginPass` are gone.
- **2b.** `TrackAsUpdateOf` captures and compares declared stamps only; `Undecided` and `ApplyUndecidedClientTokens` are gone, and with them `An_Update_Abandoned_On_A_Pooled_Context_Does_Not_Reach_The_Next_Request`. Startup validation warns about an undeclared `Guid`, `int`, `long` or `byte[]` token.
- **2c.** `HasConcurrencyTokenDbPrimer` compares an empty-token update or delete with the stored token; the `SoftDeletes` marks and `IsSoftDelete` are gone.
- **2d.** `RelatedAttachmentsPrepper` deletes a stub (`Removing_A_Link_Of_A_Concurrency_Token_Attachment_Deletes_Its_Attachment` added, green before and after). One internal `ConventionOptionsExtension<TConvention>` behind the archived-filter and concurrency-token conventions, each keeping its named type (see the review). One internal `EntityValues` for the key and value helpers. The `int` `Related()` loop is gone and `AdjustIdForEfCore` is `[Obsolete]`. `REGIRA0001` on `AutoTruncatePrimer`, `AutoNormalizingPrimer`, their two registration helpers, `AddRegisteredInterceptors` and the explicit normalizer path; `AutoTruncateDbContextInterceptor` calls the extension it repeated.
- **3a.** The `Build()` overrides, the commented-out members of `ComplexEntityIntServiceBuilder` and the four commented-out `Mapping/Models` files are gone; `[Obsolete]` on `EntityServiceBuilder.Build()`, `EntityIntSearchObjectServiceBuilder`, `EntityServiceBuilderBase`, `EntityServiceCollectionBase` and the six-type `IEntityServiceBuilder`; `void Build();` left `entities.signatures`.
- **3b.** `private protected RegisterAs(implementation, factory, families)` on `EntityServiceBuilder`; the 45 `Use*`/`Has*` bodies of the five shapes are one call each.
- **3c.** The initializer findings and the Info rewrap are gone from `ConcurrencyTokenValidator`; `DeleteCycleValidator` is gone (troubleshooting row); Regira's own global filters are skipped by assembly, `DefaultGlobalFilters` and `TrackDefaultGlobalFilter` are `[Obsolete]` and `AddDefaultGlobalFilter` is gone; the reference-data message names the entities and points at *Soft Delete*.
- **3d.** `EntityValidationContext.DbContexts` resolves the contexts once per run, and the runner reports an unresolvable one once; the per-validator `try`/`catch` around resolving contexts, primers, normalizers, global filters and entity validators is gone; the scope and wiring checks of entity validators are one, without their `ConditionalWeakTable`; `EntityDtoShapes` has no failure channel. Added: `A_Throwing_Validator_Neither_Stops_The_Host_Nor_Silences_The_Others`, `An_Unresolvable_Context_Is_Reported_Once`; the two failure tests assert the runner's message.
- **3e.** One body for the two `UseEntities`; `RegisterLastConfigured` for the four "last explicit configuration wins" blocks; `GetOrAddInstance<T>()` behind the three `For(services)`; one private body for the three `HasAttachments`; `StartupValidationHarness` in place of seven copies of the logger capture and two of the host start.

**Taken differently from the plan:**

- The UTC convention types stay as they are: they are public, a public type cannot derive from an internal one, and `DAL.EFcore` shares no internals with `Entities.EFcore`.
- `ApplyNormalizers()` and `AddObjectNormalizingContainer` are `[Obsolete]` with `EntityNormalizerContainer`, which they cannot run without.
- `HasAttachments(navigation)` syncs the navigation it is given (the *Smaller* finding), fixed without a failing test: an entity's `IHasAttachments` collection and a different navigation of the same link type cannot be mapped side by side without a model of their own.
- The harness covers the logger capture and the host start; each fixture keeps its own SQLite connection.
- A left attachment another writer removed between the owner's read and its save is a conflict now, as for any stub, where the prepper's read skipped it.

**From the step-3 review** (nothing lost data on its own; two registration findings were security-class):

- *A repository short of the shape's interface* (security, partly 6.5.0). `RegisterAs` registers what a class implements, so an `EntityRepository<Course, int>` on `For<Course>()` — or a two-type service or manager, chained or not — served no `IEntityRepository<Course>` or `IEntityService<Course>`, and `For<>()` put the default over it under every interface, bypassing its row rules. `For<>()` now refuses an implementation the configuration registered under some of the shape's repository or service interfaces but not the one it resolves through. `A_Repository_Short_Of_The_Shapes_Interface_Is_Refused` failed first; `A_Factory_Repository_Serves_Every_Repository_Interface` pins the factory overload, whose 6.5.0 swap the *T1* baseline shows.
- *The `int` `HasAttachments` check* (security). The shared body checked `IEntityService<TLink, int>` where 6.5.0 checked `IEntityService<TLink>`, so a keyed registration of the link entity made earlier skipped the link defaults, the owner-scoping filter among them. Each overload passes its own check again. `The_Link_Defaults_Are_Added_Over_A_Keyed_Registration_Of_The_Link` failed first.
- *One broken context hid the others' errors.* A context with no database provider resolves, then throws at `.Model`, aborting a check with the errors it had found for the healthy contexts. The shared resolution reads the model and options too, and the runner keeps what a check reported before it threw. `A_Context_Without_A_Provider_Does_Not_Hide_The_Errors_Of_Another` failed first; `A_Check_That_Throws_Keeps_What_It_Reported_Before` was added and fails when the runner collects with `ToArray()` again.
- *The undeclared-token warning was too narrow:* it now covers the `Related()` children, every context mapping an entity, and integer, `DateTime` and `DateTimeOffset` tokens; a `string` token stays unreported, being far more often a data column (documented). Two tests, each failing on the narrow version.
- *A row still holding an empty token* (low): the write path marks its token modified, and such an update is compared with the token it read, an empty one included, where the primer re-read the stored token and let a writer that committed in between be overwritten. `A_Row_Still_Holding_An_Empty_Token_Is_Checked_Against_It` failed first.
- *Internals the published DI package calls* (rule 5): `ConcurrencyTokenExtensions.ReadClrValue` is back as a forwarder, and the two conventions keep their named extension types (`ConcurrencyTokenOptionsExtension`, `ArchivedQueryFilterOptionsExtension`, each a one-line subclass of the generic), which `FindExtension` matches exactly. Every other `Entities.EFcore` internal that `Regira.Entities.DependencyInjection` 6.5.0 calls still exists with its signature.
- Documented, not changed: the cascade undo recognizes `ArchivablePrimer` by its type, so a decorator around it is not recognized (`ArchivablePrimer` XML doc: derive from it rather than wrap it).
- Changelog: a bug fix for the `int` factory repository; behaviour-change clauses for the refusal, `HasAttachments(navigation)` (a `[NotMapped] Attachments` beside the mapped navigation), Regira's own filters no longer checked, and the obsolete `Build()` of a derived builder registering the base shape only; the warning bullet names what it covers. Docs: the `UseEntityService` comment in `entities.signatures`, the registration rule under *Custom Entity Services*, the delete-cycle sentences of `entities.patterns` and `docs/models`, the archivable-owner remedy wording, the silent-overwrite troubleshooting row, the token primer's XML doc and the overclaiming learning.

**Docs.** `entities.patterns` (*Optimistic concurrency*, the delete-by-key paragraph, *Soft Delete*), `entities.instructions` (*Startup validation*, the id rules, the primer table, Troubleshooting: the 409 row and a new delete-cycle row), `entities.card`, `entities.namespaces`, `entities.signatures`, `entities.blueprints`, `docs/built-in-features` (concurrency, *Startup validation*, *Primers*), the `DAL.EFcore` README, the `VersionStampAttribute` XML doc and `ai/learnings.md` (the concurrency entry rewritten; one pass for both primer paths, a call chained after a base-builder method, a startup check is for what fails silently). Regira-Tools needs no change.

**Tests after the step and its review:** green on `net10.0` and `net8.0` for `Entities.Testing` (582, 579); `Entities.DependencyInjection.Testing` (140), `Entities.Web.Testing` (277), `DAL.EFcore.Testing` (8), `Entities.Mediator.Testing` (26) and `Entities.Providers.Testing` with containers (36) on `net10.0`. Solution build without new warnings; GuideVerifier 20 groups, 312 snippets.

### Step 4

Built on `wip` on 2026-10-12 and committed as `9d99eff`, at the unpublished 6.5.1; no `<Version>` changed. Your answers: none of the three optional rows (`NetworkShareCommunicator`, the bot-rule reload, GeoIP2's path lookup); of the step-0 leftovers, `FilterIsActiveOn(null)` fixed, the two-value `Margins` order and Printing.GDI's margin unit documented; the deprecated-backends policy adopted, in `AGENTS.md` and the two backends' docs.

**Baseline** at `804b170`, green: the full suite on `net10.0`, and `Entities.Testing` (579) and `Web.Security.Testing` (203) on `net8.0` too.

**A test that failed first, then the fix:**

- *7, connection strings.* `PgSettingsTests` (4 of 7 failed) and `MySqlSettingsTests` in a new `tests/DAL.MySQL.Testing` (4 of 6): a password holding `;` or `=` was cut, a quoted value misread, and `host:5433` connected to the default port. Both settings classes read and write through the drivers' own builders; Npgsql's and MySqlConnector's default port stands for "no port given", so a port written with the host wins over it.
- *7, MongoDB log.* `Backup_Keeps_Secret_Options_Off_The_Command_Line_And_Out_Of_The_Log` with a `proxyPassword`: the deny-list masked three options and kept that one. The log names the host and the database only, and the redaction is gone.
- *7, `IProcessHelper`.* `ExecuteCommand_Refuses_Variables_It_Cannot_Set_On_The_Process`.
- *8, Common.Web* (the step-0 *Smaller* row): `CommonWebMiddlewareTests`, three cases: an exception after the response started never reached the handler, and an unknown or unparseable `?culture=` answered 500. Fixed before the types became `[Obsolete]`; the tests live in `Web.Security.Testing`, the one test project reaching Common.Web on both frameworks.
- *4g.* `A_Hand_Written_Action_Answers_400_Without_An_Exception_Filter` (it answered 500).
- *6, NpoiMapper.* `Create_Nested_Object`.
- *Leftover.* `FilterIsActiveOnTests.Without_A_Date_Nothing_Is_Filtered`.

**Pinned first, green before and after:** `ConditionalBlocksTests` (the three tests *5c* names), `Test_The_Cookie_Options_Reach_The_Cookie_Scheme` (OIDC), `RotateImage45` and `AddImage_Rotated` on both drawing backends, two `BackupRestoreManager` tests, `UrlPathTests` (`../` refused), and `ListAsync_Yields_What_List_Returns`, which replaces the async twins on every storage backend.

**Built:**

- **4h.** `[Obsolete]` on the AutoMapper `AttachmentUriResolver`s, `IMappedEntityServiceCollection` and `MappedEntityServiceCollection`; the commented-out options file is gone.
- **4g.** An internal `EntityExceptionMapping` behind `EntityExceptionFilter` and `[EntityConstraintConflict]`, which now maps the 400 too and sits on both `EntityControllerBase` roots; the `try`/`catch` in `ControllerExtensions.Write` and the three in the attachment controller base are gone; `MapEntityExceptions()` checks for an instance only, and `A_Consumer_Registration_By_Type_Is_Not_Duplicated` went with that check (a second filter is harmless: the first marks the exception handled).
- **5c.** `ConditionalBlocks` is a 35-line adapter over `TemplateBlocks`, which gained `OpensCondition` and `IsLoopMarker`.
- **5e.** One `NestedDocumentGuard` in Regira.Office; one private `AddStory` per backend; the optional `SpansTextWidth` too, as `TableScaling`, used by the four backends that fit tables.
- **5h.** The 22-inch clamp scenarios run 2 cases each; the `TemplateMarkers` limits keep two tests (the declared-size bomb, without the allocation assertion, and the part count); one formatting test per block kind (condition, loop, marker rows), the header and text-box formatting tests gone. `Office.Word.testing` 654 → 616 passed.
- **6.** UziGranot as planned (the README's reason checked on nuget.org: `QRCodeDecoderLibrary` targets .NET Framework 4.6.2 only). The PDF tests call `Service<T>()`, which ignores a scenario the backend cannot run; `PdfFixtureAttribute` and `[Needs]` are gone.
- **7.** SQL Server's server-side deletion and the leftover-files warning are gone; storage `ListAsync` yields `List`'s result on the file system, Azure, GitHub and zip backends (SSH already did); `BackupRestoreManager` runs the services' internal `DumpTo` and `RestoreFrom`; one internal `UrlPath` in Regira.IO.Storage behind the three `FileNameUtility` classes, the SFTP containment included; `NormalizingUtility` without locks or its unread cache, and `RemoveDiacritics`' tables built once (they were rebuilt twice per character in `StringUtility`, not in `NormalizingUtility`).
- **7, `[Obsolete]`.** The review's list was not kept, so an agent rebuilt it: public members with no use anywhere else and no mention in any guide, in `Common`, `System.Hosting`, `DAL.EFcore` and `DAL.PostgreSQL` (see the changelog). General utilities that have tests — string casing, roman numerals, the flag helpers — stay. `GuidConverter.cs`, all comments, was in Serializing.Newtonsoft and is deleted.
- **8.** Drawing.GDI encodes through `Lazy<ImageCodecInfo[]>` matched by format id and rotates with `RotateUtility.CalculateSize`; OIDC registers its cookie scheme from `OidcAuthOptions.Cookie` itself through an internal `AddCookieScheme`; a public `EntraIdOptionsBase` holds the shared registration values and the multi-tenant issuer check; one internal `RequiredOptions.Require`; `LicenseValidator` reads a key through one private `Evaluate`; Mollie's excluded folder is gone; `UblDocumentUtility` and `PeppolNamespaces` live in `Regira.Invoicing.Peppol`, the two copies forwarding; the `[Obsolete]` rows as planned. The JWT test host registers `AuthenticationSchemeDocumentTransformer` instead of the obsolete one, and the OpenAPI tests pass with it.

**Taken differently from the plan, or found on the way:**

- GDI+ cannot draw an image under `RotateTransform(-90)` ("An object could not be created"); `Rotate` normalizes the angle to 0–359, as the old code did for negative angles. `RotateImage90Left` found it.
- New public API, for your version decision (7): `EntraIdOptionsBase`, `Regira.Invoicing.Peppol.UblDocumentUtility` and `PeppolNamespaces`, and `DAL.MySQL.Testing` in the solution.
- `SymmetricEncrypter`'s existing tests already decrypt fixed ciphertexts, the default-key ones included, so they are the 6.5.0 compatibility test; they and the `SimpleHasher` tests carry a `CS0618` pragma.
- `Validate`'s version-mismatch message was garbled ("requires version 6.x but version 7.x is required"); it reads "is for version 6.x, but version 7.x is required".
- `entities.blueprints` named `PageItems`' namespace wrongly (`Regira.Utilities`, not `Regira.DAL.Paging`); fixed while the helper list was rebuilt.

**From the step-4 review** (four bugs, each a test that failed first):

- *The exception middleware hid a failure after the response started.* It handled the exception and returned, so a streamed response cut off halfway ended as a 200; 6.5.0 threw while setting the status. The handler sees it, then it goes on. `An_Exception_After_The_Response_Started_Reaches_The_Handler_And_Goes_On`.
- *The OIDC cookie scheme's claim types changed order.* 6.5.0's copy merged `OidcAuthOptions.Cookie.Claims` into fresh defaults, which puts an app's own types ahead of them; taking the instance put them last, so an added `employee_id` lost to `sub` as the subject. An internal `ClaimNormalizationOptions.LayOverDefaults()` restores the merge before the scheme is registered; the cookie-options test asserts the order.
- *`PgSettings` and `MySqlSettings` split hosts they should not.* With no port given, `CleanupHostAndPort` split a host list (`h1:5433,h2`, then throwing at build) and an IPv6 address (`fe80::1` as `fe80` on port 1). Only a host with one colon, no comma and a numeric tail is split. A `Port=5432` in the string read as "no port"; MySqlConnector's `ContainsKey` tells presence, Npgsql's answers for every keyword it knows, so `PgSettings` asks a plain `DbConnectionStringBuilder`. Five tests per class.
- *The culture middleware set a read-only culture*, the cached instance `GetCultureInfo` returns, so code changing its formats downstream threw. It sets a writable copy. `A_Requested_Culture_Can_Be_Adjusted_Downstream`.
- Also fixed, offered by the review as 6.5.0 issues: GDI's `Rotate` threw for an indexed image (a GIF, an 8-bit BMP) and leaked its canvas when drawing failed (`Rotate_An_Indexed_Image` failed first), and an image layer leaked the bitmap it read. A file-system folder listed under an extension filter matched by string prefix (`dir2.1` for a file in `dir2.10`); `A_Folder_Whose_Name_Begins_Another_Is_Not_Listed_For_Its_Files` failed first.
- Tests: `A_Part_Declared_Smaller_Than_It_Inflates_Is_Read_Cut_Short` is back, with its `Is.False` alone, since `TemplateMarkers` still promises that case; `Split_Refuses_A_Range_Outside_The_Document` resolves its splitter before the assertion, so a backend without one ignores it.
- `LicenseValidator.Validate` accepts the same allow-list `GetStatus` does (`IsAccepted`); the SQL Server message no longer claims where the file is; `UrlPath`, `NestedDocumentGuard` and `TableScaling` say that published backends call them; the deprecated-backends policy reads "crashes, data loss and security holes".
- Changelog: the Entities.Web helpers now throw (code after `await this.Save(…)` no longer runs; `Patch`'s two refusals; an app's own exception filter or `UseExceptionHandler()` no longer sees these from an entity controller); `ListAsync` reads the whole listing first and changes order on the file system and Azure; the folder-filter fix; for the settings classes, a customised default port now reaches the connection string and an unknown key throws. Stale "the generated actions catch their own closed generic" sentences fixed in `entities.instructions`, `entities.signatures`, `docs/built-in-features`, the exception XML docs and `ControllerExtensions`' summary; the storage guide no longer says "one at a time".
- Left for its own task: the Azure and GitHub stores never refuse a `../` in a name (6.5.0; only SFTP checks containment), flagged by the review as a separate task. Another session fixed it in `c4c0ca4`, on top of `UrlPath`.

**Docs.** `Common.Web` README, `docs/examples` and the packed `web.instructions` and `web.examples` (ASP.NET Core's exception and localization middleware; the verifier group drops the three namespaces); `docs/cryptography` and `security.instructions` (the obsolete encrypter and hasher, the Basic rule and the old transformers); `entities.setup`, `docs/built-in-features` and `docs/web-endpoints` (where the exception mapping happens); `entities.instructions` and `entities.signatures` (`FilterIsActiveOn(null)`); `docs/excel` and `office.excel.instructions` (NpoiMapper's object rows); `docs/pdf` and `office.pdf.instructions` (the `Margins` array orders); the DAL.SqlServer and DAL.MongoDB READMEs; `processes` and `system.instructions` (`IProcessHelper`); `io.storage.instructions` (`ListAsync`); the media backend comparison (no printing row); the UziGranot, EPPlus, DocNET, Printing.GDI and Drawing.GDI READMEs; `ai/learnings.md` (the process-helper default, the backup left on the server, the GDI+ angle, the mapping on the controller bases, Npgsql's `ContainsKey`, the read-only cached culture); `AGENTS.md` (*Deprecated backends*); the changelog as a step-4 group.

**Tests after the step and its review:** the full suite green on `net10.0` (`Entities.Testing` 584, `Entities.Web.Testing` 276, `Office.Word.testing` 617 and 7 skipped, `Office.PDF.Testing` 148 and Spire's 15 ignored, `Web.Security.Testing` 219, `IO.Testing` 127, `DAL.PostgreSQL.Testing` 27 and `Entities.Providers.Testing` 36 with their containers, the new `DAL.MySQL.Testing` 10, the rest at their baseline or above it by the tests named here). On `net8.0`: `Entities.Testing` (581) and `Web.Security.Testing` (208), and, built for .NET 8 in place, `Common.Testing` (292, without `CollectionTests`), `System.Testing`, `Security.Testing`, `Licensing.Testing`, `Drawing.Testing`, `Office.Excel.Testing`, `IO.Testing`, the three SQL test projects and `Office.Word.testing`; `DAL.MongoDB.Testing`'s own `RepositoryTests` does not compile for .NET 8. Solution build without new warnings; GuideVerifier 20 groups, 307 snippets (the Common.Web examples and the obsolete encrypter's snippet left).

### Step 5

Wrap-up on 2026-10-13 at `c4c0ca4` (step 4 and the storage containment fix), committed as `8d25056`; no `<Version>` changed.

**Tests.** The full suite, offline with the container fixtures, green on `net10.0`: every test project at its step-4 count or above (`IO.Testing` 168 with the containment tests; `Office.Clients.Testing` has network tests only). On `net8.0`: `Entities.Testing` (581) and `Web.Security.Testing` (208), and, built for .NET 8 in place, `Common.Testing` (292, without `CollectionTests`), `System.Testing`, `Security.Testing`, `Licensing.Testing`, `Drawing.Testing`, `Office.Excel.Testing`, `IO.Testing` (166; `ListAsync` is .NET 10 only), the three SQL test projects, `Office.Word.testing`, `Office.PDF.Testing` and `Payments.Testing`. Solution build without new warnings; GuideVerifier 20 groups, 307 snippets.

**Checked against the package list** (87 packages; none added or removed since step 1 dropped the MediatR adapter):

- `ai/AGENTS.md`'s routing table names all 87; `copilot-instructions.md`'s lists the primary families, 19 rows as at 6.5.0.
- The nine packages that set `RegiraLicenseEnforced` are the nine in `Directory.Build.targets`, `legal/REGIRA-COMMERCIAL-LICENSE.md`, `licensing.md` and the README.
- `Regira-Packages.slnx` missed `src/Common.Payments` (`Regira.Payments`, guides and build targets only), already at 6.5.0; it is in the *Invoicing/Payments* folder now. Every other project on disk is in it.
- Every guide and `docs/` page with C# snippets is in a GuideVerifier group, and every path a group names exists. No guide teaches an API that became `[Obsolete]` in 6.5.1; those it names are labelled obsolete.
- Regira-Tools: the `how_to` fallbacks match `entities.patterns` (`McpServer.Testing` 466, `Kb_Driven_And_Fallback_Paths_Return_Identical_Recipe_Bodies` among them); no change. Regira-Modules builds nothing from these guides.

**Changelog review** of the whole *Unreleased* section against the code, each finding checked: the storage bullet said the SFTP containment check was unchanged, where `c4c0ca4` added the null-character refusal; the startup-validation bullet listed the undeclared-token types before the step-3 review widened them; `Regira.Office.Clients`' `Convert` and `Merge` refusals, which 6.5.0 ignored, had no **Behaviour change** label; the four Excel bullets labelled the sheet-name refusal, the case-only duplicate keys and `headers` differently per backend, and now call them behaviour changes wherever 6.5.0 did otherwise (on NpoiMapper a dictionary row with `Id` beside `ID` already threw, so only an object row's properties changed — the step-5 review's correction); the Chromium page-settings helper, new in 6.5.1, had a second bullet for its rewrite in centimetres, now folded into the first. `RegisterStartupValidation`'s XML doc still listed the removed delete-cycle check and missed the attachment foreign-key check.

**Left open:** decisions 7 (the version line; new public API: `EntraIdOptionsBase`, `Regira.Invoicing.Peppol`'s `UblDocumentUtility` and `PeppolNamespaces`) and 8 (the default encryption key); *5a*, skipped in step 1; `DAL.MongoDB.Testing`'s own `RepositoryTests` does not compile for .NET 8.

### Decisions 7 and 8

Taken on 2026-10-13, as recommended, after step 5; uncommitted. *5a* stays out: each Word backend keeps its own renumbering, and the Word check it rests on was not run.

- **8, the default key.** `AesEncrypter.Encrypt` throws `InvalidOperationException` without a secret — `CryptoOptions.Secret` or a key argument, an empty or blank one counting as none; `Decrypt` still falls back to `DefaultSecuritySettings.SaltKey`, so what it encrypted stays readable. `SymmetricEncrypter` and `SimpleHasher`, already `[Obsolete]`, are unchanged: PIM and Fleet decrypt their JWT secret with `SymmetricEncrypter`'s default key, and CRM encrypts account passwords with it. `Encrypt_Without_A_Secret_Throws` (three cases, then two blank ones from the review) failed first; `Decrypt_Without_A_Secret_Reads_What_The_Default_Key_Encrypted`, on a ciphertext Regira.Security 6.5.0 produced, and `Encrypt_With_A_Key_Needs_No_Secret` pin the rest, and the empty- and missing-secret cases left `Decrypt_Encrypted_Success`. `security.instructions` and `docs/cryptography` (the two encrypters and `CryptoOptions.Secret`), the changelog's behaviour change and a learning follow. `Security.Testing` 51 on both frameworks, guide group `security` (40 snippets), Regira-Tools 466.
- **7, the version.** You set `Common` and `Common.Setup` to 6.6.0, the family's one-bump mechanism; the other projects stay at 6.5.1 for the release tooling to raise. The changelog's *Unreleased* names 6.6.0 throughout (151 mentions) and opens with the family bullet: six new packages and new public types make the minor, and 65 packages carry a change of their own, counted from the bullets and checked against the diff since `v6.5.0` (the review's recount found the first count off: `Regira.Payments`' guide change sat in the POM bullet under its folder name). Regira-Modules' unreleased changelog names `Regira.Security.Authentication.Web` 6.6.0 (uncommitted there).

<!-- {% endraw %} -->
