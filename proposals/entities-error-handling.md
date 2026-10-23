# Error handling in Regira Entities

As of 2026-10-23. Sources: the `Regira-Packages` repository, branch `wip` at `eb79418`; the guides in
`src/Common.Entities/ai/` and `src/Common.Setup/ai/`; the API hosts of the PIM, Fleet and Samples apps; and a
throwaway test against `Entities.TestApi` on `net10.0` (see *Measured*), since removed.

**Status: built 2026-10-23 with the recommended decisions, uncommitted on `wip` (see *Outcome*).**

## Recommendation

The write pipeline's error contract is already in good shape. It has three exceptions with one mapping each, applied
on every surface, generic client messages, and the provider detail kept in the server log. This proposal leaves that
design alone and fixes the gaps around it. Each fix is small, adds no public API, and is either a defect or a guide
line.

1. **A 409 from an MVC action carries no `traceId` and no `type`.** Build it with the app's `ProblemDetailsFactory`,
   as the 400 already is (patch, `Regira.Entities.Web`).
2. **An unhandled exception answers an empty 500.** Add `AddProblemDetails()` and `UseExceptionHandler()` to the golden
   path's `Program.cs` (guides only).
3. **A read keeps running after the client has gone.** Pass `RequestAborted` to the read requests, but not to the
   writes (patch, `Regira.Entities.Web`).
4. **Three guide passages still describe a typed `catch` that no longer exists.** Correct them (guides only).
5. **A malformed PATCH body answers a bare 400.** Answer it the way the PATCH handler answers its other refusals
   (patch, `Regira.Entities.Web`; decision 2).

Items 1, 3 and 5 make one `Regira.Entities.Web` patch. Items 2 and 4 are guide patches to `Regira.Setup` and
`Regira.Entities`.

## What exists today

| What | Where | Effect |
|---|---|---|
| Three exceptions for refused writes | `EntityInputException` (and `<T>`), `EntityConstraintException`, `EntityConcurrencyException` in `src/Common.Entities/Models` | 400 for input a rule rejects, 409 for a database constraint, 409 for a stale write |
| One mapping, three places it runs | `EntityExceptionMapping` (MVC) is shared by `EntityExceptionFilter` (application-wide, registered by `ConfigureDefaultJsonOptions()`) and `[EntityConstraintConflict]` (on the controller bases). `EntityExceptionEndpointFilter` handles mapped endpoints | The same status on every surface, whether or not the host registers the filter |
| The 400 body | `EntityInputExceptionExtensions.ToValidationProblem`: `ValidationProblemDetails` from the app's `ProblemDetailsFactory`, plus `errorDetails` with scalar args | `traceId`, `type` and the app's `CustomizeProblemDetails` are on it |
| The 409 bodies | `EntityConstraintProblem.Create()` and `EntityConcurrencyProblem.Create()`, built with `new ProblemDetails { … }` | A client tells the two 409s apart by `title` ("Conflict" or "Concurrency conflict"). The Modules form does exactly that |
| Provider detail stays server-side | `EntityWriteService.SaveChanges` logs the provider exception as a warning and throws with the generic `ClientMessage` | No index names or other users' values reach the client. Transient faults are not wrapped and stay 500s |
| Constraint detection | `DbUpdateExceptionExtensions.IsConstraintViolation`: SQLSTATE class 23, SQLite 19, SQL Server 547/515/2601/2627. `IsSeveredRequiredRelationship` covers the change tracker's version of an FK violation | Covers PostgreSQL, MySQL, SQLite and SQL Server. An unknown provider stays a 500 |
| 404 | Handlers return `null`. Controllers answer `NotFound()`, which `[ApiController]` turns into a problem, and endpoints answer `Results.Problem(404)` | The same body on both surfaces (see *Measured*) |
| Reactor failures | `ReactionDispatcher.Report` logs an error, and the save stands | A failing side effect never fails the committed write |

## The findings

### 1. The MVC 409 bypasses the app's ProblemDetailsFactory

`EntityExceptionMapping.cs:21-22` wraps `EntityConstraintProblem.Create()` and `EntityConcurrencyProblem.Create()` in a
`ConflictObjectResult` as they are. MVC does not complete a `ProblemDetails` that an `ObjectResult` carries, so the
response has no `traceId`, no `type`, and none of the app's `CustomizeProblemDetails`. The same exception on a mapped
endpoint goes through `Results.Problem` and gets all three. That leaves two problems:

- The guide's answer to "why did I get a 409?" is that the constraint name is in the server log. Without a `traceId`
  in the response, nothing ties the client's 409 to that warning line.
- The two surfaces answer the same refusal with different bodies, and an app's `CustomizeProblemDetails` (a
  correlation id, say) appears on the 400 but not on the 409.

**Change.** Build the 409 the way `ToValidationProblem` builds the 400. If the factory is registered, call
`CreateProblemDetails(httpContext, 409, title, detail: …)` with the title and detail of `EntityConstraintProblem` or
`EntityConcurrencyProblem`. If it is not, fall back to the bare object. The title stays the discriminator (decision 1).
This is an internal change in `EntityExceptionMapping`. The two `Create()` methods keep their signatures.

### 2. The golden path leaves unhandled exceptions as empty 500s

Neither `project.setup.md` nor `entities.setup.md` registers `AddProblemDetails()` or `UseExceptionHandler()`. Neither
do the API hosts of PIM, Fleet and the samples: the only `Program.cs` among them that does is a console tool. The
Entities troubleshooting table already describes the symptom ("answers 500 with an empty body on a green build").
`web.instructions.md` recommends both calls, but nothing on the Entities path sends a reader there.

**Change.** Put the two lines into the golden path's `Program.cs` in `project.setup.md`, and into the fallback in
`entities.setup.md`:

<!-- no-compile -->
```csharp
builder.Services.AddProblemDetails();
// …
app.UseExceptionHandler();
```

Every error then reaches the client as `application/problem+json` with a `traceId`, and production hides the exception
detail. It is also what makes item 3 quiet. Update the troubleshooting row so the "empty body" case points at the
missing registration.

### 3. Reads ignore the request's cancellation

`IEntitySender.Send` takes a `CancellationToken`, but neither the controller helpers (`ControllerExtensions.cs:38`, and
on) nor the mapped endpoints (`EntityEndpointMapper`) pass one. When a client navigates away mid-search, a list,
search or download query runs to the end and its result is thrown away.

**Change.** Pass `HttpContext.RequestAborted` to the read requests: `DetailsQuery`, `ListQuery`, `SearchQuery`,
`AttachmentFileQuery` and the plain `AttachmentControllerBase.GetFile`. Leave writes on the default token. A write
cancelled between `SaveChanges` and its response leaves a client that cannot tell whether it committed. A write that
always finishes is the simpler contract.

A cancelled read throws `OperationCanceledException`. Since .NET 8, `UseExceptionHandler()` logs a client abort at
debug level and answers 499, and Kestrel logs it at debug level without the middleware too, so item 3 does not depend on
item 2. The exception is SQL Server: a query cancelled mid-flight surfaces as a `SqlException`, which is logged as an
unhandled error.

### 4. Three guide passages describe a typed catch that is gone

The generated actions used to catch their closed `EntityInputException<TEntity>`. Since `[EntityConstraintConflict]`
moved onto the controller bases (`ai/learnings.md`, 2026-10-12), no action catches anything, and the attribute matches
the non-generic base. These passages still describe the old behaviour:

- `entities.instructions.md:665`: "so the generated endpoint's typed `catch` matches".
- `entities.instructions.md:1943`: "`EntityInputException<T>` must name the *serviced* entity or it escapes as a 500".
- `entities.patterns.md:2043`: "the generated action's own `catch` is on its closed `EntityInputException<TEntity>` …
  a host without it answers 500".

**Change.** Any type argument answers 400 on every surface. Naming the serviced entity is still the convention for
`Item`, but it is not what decides the status. The *Catching it yourself* note in §Error Handling (catch the base) stays
as it is.

### 5. A malformed PATCH body answers a bare 400

On MVC, `ControllerExtensions.cs:104` answers JSON that does not parse with `ctrl.BadRequest()`, which arrives as a
problem titled "Bad Request" with no `errors`. On a mapped endpoint, the framework rejects the body before the handler
runs. In Development the title is the exception type name (`Microsoft.AspNetCore.Http.BadHttpRequestException`), and
the body includes the stack trace. In production the body depends on the middleware. Meanwhile, `PatchHandler` answers
the other bad patches (a non-object body, a value of the wrong type) with an `EntityInputException`, so those come back
as a `ValidationProblemDetails` with `errorDetails`.

**Change.** On MVC, throw the same `EntityInputException<TEntity>` the handler throws, under the empty key. The mapped
endpoint is decision 2.

## Measured

A throwaway test against `Entities.TestApi`, `net10.0`. Courses are served by controllers in `ContosoApiFactory` and by
mapped endpoints in `ContosoEndpointsApiFactory`. The host registers `AddProblemDetails()`.

| Request | MVC | Mapped endpoint |
|---|---|---|
| `POST /courses` with a missing `DepartmentId` (FK) | 409, `title` + `detail` only | 409 with `type` and `traceId` |
| `POST /courses/99999/files` (owner missing) | 409 "A database constraint rejected the change." | The same, with `type` and `traceId` |
| `GET /courses/99999` | 404 problem with `type` and `traceId` | The same |
| `PATCH /courses/1` with `{not json` | 400 "Bad Request", no `errors` | 400 titled with the exception type, stack trace in Development |
| `GET /courses?ids=abc` | 400 `ValidationProblemDetails` | 400 `ValidationProblemDetails` with `errorDetails` |
| `POST /courses/{id}/files` with a 0-byte file | 200, saved | 200, saved |

The TestApi serializes MVC through Newtonsoft with a camelCase resolver, so its MVC error keys come out camelCased. That
is the documented host-serializer effect, not a difference between the surfaces.

## Decisions

1. **What tells the two 409s apart.** (a) Keep `title`, which the Modules form already reads, and route it through the
   factory without changing it. (b) Give each 409 its own `type` URI, the RFC 9457 discriminator, which survives an
   app that rewrites titles. *Recommended: (a).* It is a contract clients already use, and (b) means minting and
   documenting URIs that nothing needs yet.
2. **A malformed PATCH on a mapped endpoint.** (a) Leave the framework's 400. (b) Read the body in the handler, as the
   MVC helper does, and declare it with `.Accepts<JsonElement>()` so OpenAPI still shows it. *Recommended: (a).* With
   item 2 in place, the production body is a plain problem, and (b) adds code for a request no client sends on purpose.
3. **The attachment upload's owner check.** An upload to an owner that does not exist answers the generic constraint
   409 (*Measured*). An upload to an owner the caller cannot read succeeds: `entities.instructions.md` documents that
   security gap and leaves the fix to a validator in the app. Closing it means checking the owner through its own,
   row-scoped read service before the upload. That answers 404 in both cases, but the upload command and
   `EntityAttachmentControllerBase` do not know the owner type today. *Recommended: its own proposal.* It is a security
   change with design work in it, not an error-shape fix.
4. **Cancellation on writes.** *Recommended: no* (item 3).

## Considered and left out

- **A common `EntityException` base.** The three types are caught in one shared mapping, and no consumer code catches
  them (none of the PIM, Fleet or Samples backends has a `catch` on them). It would add API with no defect behind it.
- **An `IExceptionHandler` that maps the entity exceptions for the whole app**, covering an app endpoint that forgets
  `.AddEndpointFilter<EntityExceptionEndpointFilter>()`. It would be a fourth mechanism beside the filter, the attribute
  and the endpoint filter, and it only runs where `UseExceptionHandler()` does. Revisit if a report shows an app
  endpoint leaking a 500.
- **A 404 thrown from inside the pipeline.** Handlers already answer a missing row with `null`, and the guide covers the
  one place where a validator's 400 stands in for a 404. Decision 3 removes the main case.
- **Renaming `[EntityConstraintConflict]`**, which maps the 400 too. Only the name is wrong, and a rename needs an
  `[Obsolete]` alias.
- **A startup check for an attachment DTO that does not derive from `EntityAttachmentDto`.**
  `EntityAttachmentUriAfterMapper` already throws on the first request with a message that names the fix. Under
  `learnings.md`'s rule (a startup check is for what fails silently), it is a troubleshooting row.
- **Mapping a truncated string (SQL Server 2628/8152, SQLSTATE 22001) to 409.** `[MaxLength]` on the input DTO stops it
  as a field-level 400 before the database sees it.
- **Quieting EF's `Failed executing DbCommand` error line**, which accompanies every constraint 409. EF logs it for
  transient faults too, and those must stay loud. At most, add a troubleshooting note that the Regira warning that
  follows it names the entity.

## Build notes

- **Versions.** Leave `Regira.Entities.Web` above its last published version (patch). Guide-only patches go to
  `Regira.Entities` and `Regira.Setup`. Add one `CHANGELOG.md` bullet per package.
- **Tests** (`tests/Entities.Web.Testing`, both factories):
  - A constraint 409 and a concurrency 409 carry `traceId`, `type` and an app's `CustomizeProblemDetails` extension
    (the shape at `EntityEndpointTests.cs:291`), with the titles unchanged.
  - A read helper and a read endpoint hand `RequestAborted` to the sender, through a capturing `IEntitySender`. A write
    gets the default token.
  - An MVC `PATCH` with unparseable JSON answers a `ValidationProblemDetails` with `errorDetails`.
- **Docs.** In `entities.instructions.md` §Error Handling, mention that the 409 carries `traceId` for matching with the
  log. In `entities.signatures.md`, nothing changes. In `Common.Entities/docs`, check `web-endpoints.md` and
  `services.md`, which describe the 409, against item 1.

## Outcome

Built on 2026-10-23 with decisions 1 (a), 2 (a), 3 (its own proposal) and 4 (no), and left uncommitted on `wip`
beside the handler-overrides work of another session.

- **Item 1.** `EntityExceptionMapping` builds both 409s with `ProblemDetailsFactory.CreateProblemDetails`, keeping the
  title and detail of `EntityConstraintProblem` and `EntityConcurrencyProblem`, and falls back to the bare object
  without a factory.
- **Item 2.** Templates 2, 3 and 4 of `project.setup.md` register `AddProblemDetails()` and call `UseExceptionHandler()`
  right after `Build()`. The fallback line in `entities.setup.md` names both, and a new troubleshooting row in
  `entities.instructions.md` covers the empty 500.
- **Item 3.** `DetailsQuery`, `ListQuery`, `SearchQuery` and `AttachmentFileQuery` are sent with `RequestAborted` from
  the controller helpers, the attachment controller bases and the mapped endpoints. So are the download-by-name lookup
  (`AttachmentRouteValues.FindLinkId`) and `AttachmentControllerBase.GetFile`. Writes keep the default token.
  *A client that disconnects* in §Error Handling and a bullet in `docs/web-endpoints.md` describe it.
- **Item 4.** The three passages are corrected. `docs/built-in-features.md` was already right.
- **Item 5.** The MVC `PATCH` helper throws the handler's `EntityInputException` ("A JSON merge patch is a JSON
  object.", empty key) for a body that does not parse. The PATCH paragraph in `entities.instructions.md` says so.
- **Tests.** `tests/Entities.Web.Testing/EntityErrorResponseTests.cs`, run against both factories: both 409s carry
  `traceId`, `type` and an app's `CustomizeProblemDetails` field; reads (details, list, search, both downloads) get a
  cancellable token, and a save and an upload do not; on the controller, a PATCH body that is not JSON answers the 400
  with `errorDetails`. All tests in `Entities.Web.Testing` and `Entities.Mediator.Testing` pass, and `Entities.Web`
  builds for `net8.0` and `net10.0`. The debug-level logging of a client abort was checked in the ASP.NET Core source
  (8.0 and 10.0), not by a test.
- **Review.** A separate review session found it ready, with nothing that had to be fixed. Its doc clarifications are
  applied. The guide now says a download's file read is not cancelled (`IFileService` takes no token), and that SQL
  Server reports a cancelled query as a `SqlException`. The template comment covers only errors the app raises. The
  tests check the downloads and the concurrency 409's `type`. Left as they are: the protected `FetchItem` without a
  token (changing it would change a protected signature), the developer quickstart's minimal `Program.cs`, and merging
  the two `Regira.Entities.Web` 6.6.1 changelog bullets, which is for release time.
- **Changelog.** There are two new bullets under Unreleased: `Regira.Entities.Web` 6.6.1, and `Regira.Setup` with
  `Regira.Entities` 6.6.1 for the guides. `Entities.Web.csproj` still reads 6.6.0, the published version.
