# Applying the .NET practices review
<!-- {% raw %} -->

As of 2026-10-13. Sources: the `Regira-Packages` repository, branch `wip` at `3d3464a`; a list of 25 .NET practice claims reviewed in chat the same day, whose correct claims (with the caveats the review added) are the input here; and four read-only audits of `src/`, one per group of practices. The bug findings were re-read in the code. The SDK behaviour quoted for Mollie.Api 5.1.0, SendGrid 9.29.3 and RestSharp 114 was read from the cached NuGet binaries, not run. Nothing on this page has been built or run.

**Status: built 2026-10-13, uncommitted, trimmed to the priorities set during the build — security, then simplicity, then best practices (see *Outcome*).**

## Recommendation

The packages already follow most of these practices where they matter:

- Writes never take the request's token: Entities.Web passes none, and reactors run on `CancellationToken.None`.
- Pom and Gotenberg show the right `HttpClient` shapes.
- Web.Analytics queues page views on a bounded channel.
- Nothing fires and forgets, nothing scans assemblies, and `ValueTask` appears only where it fits.

The gaps cluster in four places:

- The older provider packages that build their own `HttpClient`: SendGrid, ViaAdValvas, Mollie and GitHub.
- The System.Hosting background queue.
- There is no clock abstraction, so expiry tests wait on real time.
- Sixty constant regular expressions are parsed at run time.

The audits also found fifteen defects (see *Found while reviewing*). Two of them crash, and one misreports every synchronous background task.

If only five things are done, do these:

1. The defects in step *1*.
2. One `HttpClient` shape for the provider packages (*2a*).
3. `TimeProvider` in the token and analytics code, so the tests stop waiting (*4*).
4. `[GeneratedRegex]` for the constant patterns (*6a*).
5. Stating the reactors' at-most-once guarantee in the guides (*7a*).

## Rules

1. **The library enables; the app decides.** The packages make the right thing possible and keep their defaults safe. Some choices stay with the app, each with a guide line showing how: its resilience policy, its timeouts, start-up validation of its own settings, and whether to publish AOT.
2. **Published API keeps compiling.**
   - A new constructor or overload goes beside the existing one.
   - An interface grows through a default interface method.
   - A class that DI activates gains a constructor only of a different length (two constructors of one length make MS DI throw).
3. **A published behaviour that changes gets a `Behaviour change:` clause** in its package's changelog bullet. It is made an opt-in where an app could rely on the old behaviour.
4. **Performance work without a correctness reason waits for a benchmark** (*6.0*). A mechanical change with its own payoff, such as a regex checked at compile time, does not wait.
5. **Fix small, document hard.**
   - Crashes, hangs and lost data are always fixed.
   - A rare shape whose fix needs new machinery becomes a sentence where readers meet it.
6. **Docs in final state**, as `AGENTS.md` asks.

## How the practices map onto the packages

| # | Practice | Applies here? | Where | Step |
|---|---|---|---|---|
| 1 | `ValueTask` only when usually synchronous; await once | No change | Web.Analytics' `IPageViewEnricher` / `IVisitContributor` and the EF interceptor overrides, each awaited once. Switching a public `Task` member breaks every implementer for one small allocation | — |
| 2 | `HttpClient` lifetime: no client per call, and pick up DNS changes | **Yes** | SendGrid (a client per resolve), ViaAdValvas (per call), Mollie and GitHub (per instance, never disposed), MailGun (RestSharp's static cache, no connection lifetime). Pom, Gotenberg, Billit and Office.Clients are right already | *2a* |
| 3 | The 100 s default timeout | **Yes**, as an option | Only Gotenberg exposes `Timeout` | *2b* |
| 4 | `AddStandardResilienceHandler()` | Docs | No package retries outbound calls. An app chains the handler onto a package's client name | *2c* |
| 5 | Retries need idempotency | Partly | Mollie sends an `Idempotency-Key` per request, but `Save` gives the caller no way to repeat it. Billit's `Send(IInvoice)` creates and then sends | *2d* |
| 6 | Outbox for "save + publish" | Docs | Reactors are at-most-once, and no guide says so | *7a* |
| 7 | No `await` in a `lock`; `SemaphoreSlim` | Locks are clean; three initialisation races | Azure sync-over-async, SFTP connect, the Puppeteer download | *3c* |
| 8 | Bounded parallelism | **Yes** | `FileProcessor` on net8.0, `ProjectService` | *1*, *3a* |
| 9 | Bounded channels | **Yes** | `BackgroundTaskQueue` is unbounded. `PageViewQueue` is bounded already | *3b* |
| 10 | `Span<T>` / `stackalloc` | After a benchmark | `DefaultNormalizer` is the only hot parser | *6c* |
| 11 | `ArrayPool`, the 85,000-byte LOH threshold | Mostly sizing, little pooling | `MemoryFileExtensions`, `FileUtility`, `ZipManager` | *6b* |
| 12 | `FrozenDictionary` | One table | The 430-entry diacritics map in `StringUtility` | *6c* |
| 13 | `[GeneratedRegex]` | **Yes** | 60 constant patterns | *6a* |
| 14 | Message templates in logs | **Yes** | 13 call sites | *5a* |
| 15 | `ValidateOnStart` | Partly | Three settings fail at first resolve with a `NullReferenceException`. The JWT key check runs at the first authenticated request | *5b* |
| 16 | `BackgroundService`, not `Task.Run` | Followed. One service crashes the host | `QueuedHostedService` | *1* |
| 17 | Keyed services instead of switch factories | No | No package picks an implementation in a switch. The pre-.NET 8 `TypedServiceProvider` is already `[Obsolete]` | *5c* |
| 18 | `IExceptionHandler` + ProblemDetails | Partly | Common.Web's middleware is obsolete in favour of it already. Security.Authentication.Web answers 400 in three shapes | *5d* |
| 19 | HybridCache | No | Nothing in the packages caches a database or API result | — |
| 20 | `TimeProvider` | **Yes** | JWT and refresh tokens, the audit-timestamp primers, Web.Analytics | *4* |
| 21 | .NET 11 runtime async | No | It needs a net11.0 target; framework awaits get it for free when an app runs on .NET 11 | — |
| 22 | Native AOT | Not now | Every package depends on `Regira.Common`, whose reflection is not annotated | — |
| 23 | The request token and side effects | Right for writes; reads ignore it | Entities.Web | *7b* |
| 24 | Assembly scanning and cold starts | No | None in `src/`. The FluentValidation scan is the app's call | *5e* |
| 25 | Measure first | **Yes** | There is no benchmark project | *6.0* |

## Found while reviewing

| # | Finding | Where | Class | Fixed in |
|---|---|---|---|---|
| F1 | `BackgroundQueueManager.Execute(Action)` wraps the action in a function that returns `null!` as its `Task`. Awaiting it throws, so every synchronous background task ends with status *Error* after its action ran | System.Hosting `Background/Services/BackgroundQueueManager.cs:24-28` | Wrong result | *1* |
| F2 | `QueuedHostedService` has no try/catch. A work item queued directly on `IBackgroundTaskQueue` that throws faults `ExecuteAsync`, and .NET's default `BackgroundServiceExceptionBehavior.StopHost` stops the app. `BackgroundQueueManager` catches its own items | System.Hosting `Background/Core/QueuedHostedService.cs:10-19` | Crash | *1* |
| F3 | `RegexUtility.ExtractPhoneNumbers(x, allowShortNumbers: true)` builds a pattern with one `)` too many, so it always throws `RegexParseException`. The only test never passes `true` | Common `Utilities/RegexUtility.cs:243` | Crash | *1* |
| F4 | `FileProcessor.ProcessFiles` behaves differently per target framework. On net8.0 it starts every file and every sub-folder at once (`Task.WhenAll`, recursively); on net10.0 it runs sequentially. The two recursions also pass different arguments: `dirUri` and `so.Recursive` on net8.0, the absolute URI and `recursive` on net10.0 | Common.IO.Storage `Helpers/FileProcessor.cs:28-68` | Floods a remote store; wrong folders on one target | *1* (decision 2) |
| F5 | `StringUtility.InjectSingleValue` splices the key into a regex unescaped. A key holding `.`, `(` or `[` matches other tags or throws. It also builds a new `Regex` per key per call, and `HtmlTemplateParser` calls it rows × keys times | Common `Utilities/StringUtility.cs:514` | Wrong output | *1* |
| F6 | `SendGridMailer` builds a `SendGridClient`, and with it a new `HttpClient`, per instance. `AddSendGrid` registers the mailer as transient, so every resolve opens new connections | Mail.SendGrid `SendGridMailer.cs:11` | Socket exhaustion under load | *2a* |
| F7 | `PeppolService.Send` creates an `HttpClient` per call. It also adds a header named `ContentType`, which is not `Content-Type` and does nothing | Invoicing.ViaAdValvas `Services/PeppolService.cs:14-21` | Socket exhaustion under load | *2a* |
| F8 | `BackgroundTaskQueue.Options.InitialQueue` items never run unless `InitialConcurrentCount` equals their count | System.Hosting `Background/Core/BackgroundTaskQueue.cs:15-16` | Silent no-op | *3b* |
| F9 | `BackgroundTaskManager` keeps every task ever run in a static dictionary | System.Hosting `Background/Services/BackgroundTaskManager.cs:11` | Unbounded memory | *3b* |
| F10 | `PageViewWriter` backs off 30 s after a store failure using `CancellationToken.None`. Shutdown waits for the delay, then the flush runs against a disposed container | Web.Analytics `Services/PageViewWriter.cs:80` | Shutdown hang (≤ 30 s) | *3b* |
| F11 | The sync members of Azure's `BinaryBlobService` (`GetAbsoluteUri`, `GetIdentifier`, `GetRelativeFolder`) block on `Open().GetAwaiter().GetResult()`. Its first call does network I/O, and it runs on the attachment save path | IO.Storage.Azure `BinaryBlobService.cs:185,197,203` | Blocked threads | *3c* |
| F12 | `SftpCommunicator.Open` connects without synchronization, so concurrent first calls race | IO.Storage.SSH `SftpCommunicator.cs:36-42` | Race | *3c* |
| F13 | PDF.Puppeteer queues every render behind the browser-download lock. It has no "done" flag, unlike Playwright | PDF.Puppeteer `PdfManager.cs:17-29` | Throughput | *3c* |
| F14 | The mail registrations hand the config to the sender through `IOptionsSnapshot<T>` (scoped) inside a transient factory. Resolving a sender from the root provider, as a singleton does, throws under scope validation, which Development turns on | Mail.MailGun, Mail.SendGrid, Mail.MailKit `DependencyInjection/ServiceCollectionExtensions.cs:12-13` | Start-up error in Development | *5b* |
| F15 | `UserControllerBase` creates the user and then mails the confirmation link. If the mail fails the answer is 500; a repeat finds the user and answers 200 without mailing | Security.Authentication.Web `Controllers/UserControllerBase.cs:40-63` | Lost mail | *7a* |

## 1. The defects that crash or misreport

Each fix starts with a test that fails on `wip`. Nothing in `tests/` covers the System.Hosting background code yet; its tests go into `tests/Hosting.Testing`.

- **F1:** the action's wrapper returns `Task.FromResult<object?>(null)`.
- **F2:** the loop catches each work item's exception and logs it, then takes the next item. The logger arrives through a second constructor, `(IBackgroundTaskQueue, ILogger<QueuedHostedService>)`; `AddHostedService` picks the longer one. **Behaviour change:** the host survives a throwing work item.
- **F3:** remove the stray `)`. A test with `allowShortNumbers: true` pins it.
- **F4:** one code path on both targets (decision 2). A test over nested folders on the file-system backend decides which of the two recursions is right before the other is removed.
- **F5:** `Regex.Escape(key)`. **Behaviour change:** a key holding regex metacharacters matches literally. Building one regex per call instead of per key is performance work for *6b*.

## 2. Outbound HTTP

### 2a. One shape for every package that builds its own client

The parameterless path shares one static `SocketsHttpHandler` per package, with `PooledConnectionLifetime = 2 min`. Each instance wraps it in its own `HttpClient(handler, disposeHandler: false)`, so per-instance settings stay per instance, connections are pooled, and DNS changes are picked up. A constructor overload taking an `HttpClient` lets an app hand in a factory client. It also lets tests run offline with a fake handler.

| Package | Today | Change |
|---|---|---|
| Invoicing.ViaAdValvas `PeppolService` | A client per `Send` (F7) | The shared-handler shape. Drop the `ContentType` header |
| IO.Storage.GitHub `GitHubCommunicator` | A client per communicator | The shared-handler shape, plus an `HttpClient` overload |
| Payments.Mollie `PaymentService` | `new PaymentClient(key)`, which builds a client per instance that is never disposed | Pass a client to Mollie's `PaymentClient(string, HttpClient)`, from the shared handler or the overload |
| Payments.Pom `PaymentService` | A static `HttpClient` on a handler with a connection lifetime | Move to the shared-handler shape, so *2b*'s timeout can be set per instance (a shared client's `Timeout` cannot change after its first request) |
| Mail.SendGrid `SendGridMailer` | A client per resolve (F6) | `AddSendGrid` registers a named client (`SendGridMailer.HttpClientName`) and builds the mailer by factory with `new SendGridClient(httpClient, new SendGridClientOptions { ApiKey = … })`. Direct `new` uses the shared handler |
| Mail.MailGun `MailGunMailer` | RestSharp's static per-URL client cache, on a handler with no connection lifetime | The same as SendGrid, with `new RestClient(httpClient, options)`. Low priority: it only matters when Mailgun's DNS moves |
| Common.Office.Clients | 16 clients, each named after its interface. Any other registration of the same interface shares that name and runs both configure actions (`ai/learnings.md`, 2026-09-16) | Prefix each name with the package (`Regira.Office.Clients.IWordConverter`) and expose the prefix. **Behaviour change** for an app that configures these clients by interface name |

### 2b. A timeout option

Each config that leads to an `HttpClient` gains `TimeSpan? Timeout`, applied the way Gotenberg applies it, with `null` keeping the 100 s default:

- `BillitConfig`
- `OfficeClientOptions`
- `PomSettings`
- `GatewaySettings`
- `MailgunConfig`
- `SendGridConfig`
- `MollieConfig`
- `GitHubOptions`

These are additive properties.

### 2c. Resilience in the guides

One general section shows how to chain `AddStandardResilienceHandler()` onto a package's client name. The home is picked in the build, most likely the shared setup guide, and each family guide links to it. It carries two warnings:

- The handler retries POSTs by default. For calls that create something (Billit orders, Pom pay links, mail sends), call `options.Retry.DisableForUnsafeHttpMethods()`.
- Its 10 s attempt timeout and 30 s total timeout cut off Gotenberg and Office.Clients conversions, so raise `AttemptTimeout` and `TotalRequestTimeout` there.

Every package with a named client needs its name public for this. Gotenberg and Billit expose theirs already; Office.Clients, SendGrid and MailGun get one in *2a*.

### 2d. Idempotency

- **Mollie:** `PaymentService` gains `Save(IPayment item, string idempotencyKey)`, which wraps the call in `_paymentClient.WithIdempotencyKey(key)`. A caller that repeats the save with the same key gets the first payment back rather than a second one. This is a class member, not part of `IPaymentService`.
- **Billit:** the guide says that a retryable flow calls `Create`, keeps the id, and then calls `Send(id)`, since repeating `Send(IInvoice)` after a failed send creates a second order.
- **Mail senders:** neither Mailgun nor SendGrid takes an idempotency key, so the guide says not to retry a send that may have reached the provider.

## 3. Background work and concurrency

### 3a. Bounded parallelism

- **`FileProcessor`:** see step *1* and decision 2.
- **`System.Projects` `ProjectService`:** `Task.WhenAll(projectUris.Select(Details))` reads every `.csproj` in the tree at once. It becomes `Parallel.ForEachAsync` with a cap of 8, writing into an indexed array to keep the order.
- **`CollectionUtility.ChunkActionsAsync`** is bounded already and stays as it is.

### 3b. The System.Hosting queue and the analytics writer

- **`BackgroundTaskQueue`** moves onto a `Channel`, with an opt-in `Options.Capacity` (decision 4). It signals `InitialQueue` items itself (F8).
- **`BackgroundTaskManager`** gains an opt-in `Options.Retention`: finished tasks older than it are dropped (F9). With no retention set, tasks are kept as today.
- **`PageViewWriter`'s back-off** takes `stoppingToken` (F10). Cancellation then reaches the existing `catch` that flushes and returns.

### 3c. Initialisation races

- **Azure (F11):** the `BlobContainerClient` constructor does no I/O, so it is built lazily on first use. The sync members then no longer need `Open`, and the exists/create check stays in the async `Open`. **Behaviour change:** those members stop throwing when the container is missing.
- **SFTP (F12):** a `SemaphoreSlim` around the connect, using `ConnectAsync(token)`.
- **Puppeteer (F13):** a static `Lazy<Task>` for the browser download.

## 4. A clock

These take a `TimeProvider`:

- `JwtTokenHelper`, for issue and expiry times.
- `RefreshTokenService`, which already passes `now` through.
- `CookieSignInExtensions`, which resolves one from `RequestServices` and falls back to `TimeProvider.System`.
- `HasCreatedDbPrimer` and `HasLastModifiedDbPrimer`.
- Web.Analytics: the page-view timestamp, the stats window, the writer's delays, its `PeriodicTimer` and its retention cut-off. net8.0 has `TimeProvider` overloads for `Task.Delay` and `PeriodicTimer`.
- ViaAdValvas' seal date.

**Shape:** each class keeps its constructor and gains one with a trailing `TimeProvider`; the old one uses `TimeProvider.System`. Each package's registration calls `services.TryAddSingleton(TimeProvider.System)`, so DI picks the longer constructor. The build checks each class's registration for the equal-length trap in rule 2.

**Tests:** these switch to `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`):

- **JWT and refresh tokens.** `JwtAuthenticationTests` and `RefreshTokenEndpointTests` each wait 2.5 s for a token to expire. They issue it at a fake past time instead, and the real validator sees it as expired.
- **Analytics writer.** About fifteen tests each wait at least a second; they advance the fake clock instead. The 30 s back-off gets a test of its own.
- **Audit timestamps.** `UtcTimestampTests` asserts exact values in place of `Within(5s)`.

**Not changed:**

- **Licensing** already takes an explicit `now`.
- **`MemoryCacheProvider`** expires entries on System.Runtime.Caching's own clock, so it only swaps `DateTime.Now` for `DateTimeOffset.UtcNow`.
- **Pom's model defaults** are property initialisers.

## 5. Logging, settings, DI and errors

### 5a. Log message templates

The 13 call sites become templates: Entities.EFcore's `EntityWriteService`, `EntityReadService` and `QueryBuilder` (11), and DAL.PostgreSQL's backup and restore services (2). `EntityReadService.cs:92` joins every listed id even when Debug is off, so it is guarded by `IsEnabled(LogLevel.Debug)`.

`[LoggerMessage]` is not used. The services are generic, so the generator cannot host them, and the calls are Debug level. The templates give the structured search, which is the point.

### 5b. Settings

- **Fail at registration, with a message, where a value already fails at first resolve.** This moves the same failure earlier, as Gotenberg's registration already does:
  - Office.Clients' `BaseUrl` and Billit's `BaseUrl`, which today throw `NullReferenceException` on the first client.
  - JWT's "exactly one key source" check, which today runs at the first authenticated request.
- **Mail registrations use `IOptions<T>` in place of `IOptionsSnapshot<T>` (F14).** The config comes from an `Action`, so a snapshot only recomputes the same values.
- **Everything else is documented** (decision 1). The mail packages register their config through `Configure<T>`, so an app adds `services.AddOptions<SendGridConfig>().Validate(c => !string.IsNullOrEmpty(c.Key)).ValidateOnStart()`. The account-links guide shows the same for `AccountEmailOptions.AllowedOrigins`, for apps that send links.

### 5c. Keyed services

The `[Obsolete]` messages on `TypedServiceProvider` and `TypedWrapper` name `AddKeyed*` as the replacement. Nothing else changes.

### 5d. Error shapes

- **Entities.Web's 409:** a test checks whether it carries the same fields as the 400 (`traceId`). If not, it is built through `ProblemDetailsFactory` like the 400. This adds a field to the body.
- **Security.Authentication.Web's 400:** see decision 3. Today it answers with three shapes: the automatic `ValidationProblemDetails`, a `SerializableError` from `BadRequest(ModelState)`, and a bare `ProblemDetails`. Its 401 bodies stay, since clients read `isLockedOut`.
- **Cookie API mode** answers a bare 401 or 403. The guide points at `UseStatusCodePages()`.

### 5e. Start-up cost

- The FluentValidation guide shows explicit `IValidator<T>` registration beside the assembly scan, for serverless hosts.
- `EntityValidatorDiagnostics` caches `GetTypes()` per assembly rather than per entity. It runs only in Development by default.

## 6. Regular expressions, buffers and lookups

### 6.0. A benchmark project

`tools/Benchmarks` targets net10.0 and uses BenchmarkDotNet with `MemoryDiagnoser`. It is not packed and not part of the test run (decision 5). It covers:

- `DefaultNormalizer.Normalize`.
- `HtmlTemplateParser` and `StringUtility.Inject` over N rows × M keys.
- `IMemoryFile.GetBytes` / `GetStream` and `ZipManager.Unzip` at 100 KB and 10 MB, with LOH and gen-2 counts.
- `BotDetector.IsBot`.

*6b* and *6c* run it before and after, and each records the numbers in its *Outcome*.

### 6a. `[GeneratedRegex]`

The 60 constant patterns move to the source generator:

- Common's `RegexUtility`, `StringUtility`, `UriUtility` and `DefaultNormalizer`.
- `FileNameUtility` and `HtmlTemplateParser`.
- The Word backends and `TemplateScope`.
- `PgTools`, MySQL's `SQLDumpManager` and RazorEngineCore's parser.
- `AttachmentPrimer`.

How they move:

- **Partial methods, not partial properties:** the net8.0 generator has no partial properties. Adding `partial` to a public class breaks nothing.
- **`RegexUtility`'s `=>` pattern properties become `const`.** Its timeouts carry over through `matchTimeoutMilliseconds`.
- **`AttachmentPrimer`'s pattern moves to a non-generic internal helper**, because the generator cannot host a generic class.
- **The six `NonBacktracking` patterns in `TemplateBlocks` stay as they are.** The generator does not support that option and would only construct them at run time; `TemplateBlocksTests` keeps guarding them.

This step needs no benchmark. The compile-time check is the payoff, and it would have caught F3. It also stops the static regex cache (15 entries) from thrashing: `SQLDumpManager.Cleanup` alone uses 26 patterns, and evicts the normalizer's on every dump.

The 16 patterns built at run time stay.

### 6b. Buffers

These are measured before and after:

- **`MemoryFileExtensions.GetStream`** sizes its `MemoryStream` from a seekable source. **`GetBytes`** reads into one exact-size array rather than copying through `ToArray()`. These run on every download and every attachment upload. `FileUtility` gets the same change.
- **`FileUtility.GetBase64String(clean: true)`** returns early when there is nothing to clean. `Convert.ToBase64String` never emits the characters it strips.
- **`ZipManager`** hoists its per-entry 81,920-byte buffer, or rents it and returns it in `finally`, and sizes its `MemoryStream` from `entry.Size`.
- **`HtmlTemplateParser`** builds rows with a `StringBuilder` rather than `+=`, and `InjectSingleValue` stops building a regex per key.
- **Word.Mini's streams** start at the template's length.

`ArrayPool` is used only where a buffer stays inside the method. A buffer handed to the caller cannot be returned to the pool.

### 6c. The normalizer

- **`RemoveDiacritics`:** an ASCII fast path skips the lookup for `c < 0x80`, and the 430-entry map becomes a `FrozenDictionary`.
- **`QKeywordHelper`:** caches the `Options` and `Normalizer` it now builds on every access, and uses ordinal `StartsWith`.
- **A `Span`/`stackalloc` rewrite** of `Normalize` happens only if *6.0* shows the normalizer still dominates, and golden tests for its Unicode cases come first.

## 7. Side effects and their guarantees

### 7a. Say what is guaranteed

The Entities guides state that a reactor runs **at most once**. Two things lose a reaction:

- **A crash between the commit and the reaction.** This includes the enqueue to a job system, which the guides already recommend, since that enqueue also happens after the commit.
- **A reactor that throws.** The failure is logged, and nothing retries it.

Work that must happen writes its own row in the same `SaveChanges`, an outbox, and a hosted service delivers it at least once, with the row id as the receiver's deduplication key. The guide shows that shape in prose; the mail-in-a-reactor example stays as the best-effort case it is.

Security.Authentication.Web's guide states F15 and how an app resends a confirmation.

### 7b. Reads stop when the client leaves

Entities.Web passes `HttpContext.RequestAborted` to its read queries only: list, search, details and attachment file (decision 6). Commands keep passing no token, so a disconnect still cannot cut a write in half. **Behaviour change:** an aborted read ends with `OperationCanceledException`.

## Not proposed

- **`ValueTask` on public interfaces (1):** a breaking change for one small allocation.
- **Changes to `lock` (7):** no lock holds async work. `SyncOverAsync` in Entities.EFcore serves only the sync `SaveChanges` hooks, deliberately and with a guard.
- **Keyed overloads (17)** for mail senders and converters (`AddSendGrid(key, …)`): additive and medium-sized, but nobody has asked for two senders.
- **An `IExceptionHandler` adapter for Entities.Web (18):** its filters cover its own endpoints.
- **HybridCache (19):** no package caches a fetched result. `ICacheProvider` has no consumer in `src/`; whether to replace it with HybridCache at 7.0 is a separate question.
- **Runtime async (21):** it needs a net11.0 target, and .NET 11 is not released.
- **`<IsAotCompatible>` (22):** TreeList, Common.Security, Common.System and Common.IO.Storage would pass the analyzers on their own code. But each references `Regira.Common`, which uses unannotated reflection (`MakeGenericMethod`, `XmlSerializer`, `TypeDescriptor`), so the flag would promise more than an AOT app gets. Revisit when a consumer asks.
- **`[LoggerMessage]` (14):** see *5a*.
- **Durable reactors:** a built-in outbox needs a table in the app's context and a migration. *7a* documents the shape instead.
- **The licence banner:** `Common.Licensing` writes it with `Console.WriteLine` during registration. Moving it to a hosted logger would change what every app prints at start-up.

## Outcome

Built on `wip` at `3d3464a`, uncommitted. During the build the order of importance became: security, keep it simple, best practices. The scope was trimmed to match.

**Built**

- **Step 1, every defect, each with a failing test first.** F1, F2 (`QueuedHostedService` gained the logger constructor), F3, F4 and F5. F4 took decision 2(a), sequential on both targets, with net10.0's recursion: the absolute URI that the existing file-processor tests cover. Its test found one more defect on both targets: with a recursive search object and no `processRecursively`, a file was handled once for every folder above it. Folder recursion now happens only for `processRecursively: true`. F3 puts the phone pattern before the short-number one, so `allowShortNumbers` loses none of the full numbers.
- **2a, the shared pool alone.** ViaAdValvas, GitHub, Mollie, Pom, SendGrid and MailGun keep their one constructor and take a client per call on a static `SocketsHttpHandler` per package (two-minute connection lifetime, no cookies), so any lifetime is safe. A test points an internal `Handler` at a stub. No `HttpClient` overload and no `IHttpClientFactory` constructor: both were built and dropped. A timeout is the app's concern, retries would repeat creates, and the factory's default handler shares cookies per name. Pom's unpublished `HttpClient` constructor went too. GitHub's token and headers go on each request. SendGrid keeps refusing an empty key at construction, like Mollie. MailGun's handler copies RestSharp's settings (no cookies, no redirects, decompression, infinite client timeout, RestSharp's own 100 s).
- **2d.** Mollie `Save(payment, idempotencyKey)`; the docs carry the Billit and mail warnings.
- **2c, docs.** *Outbound HTTP Clients* in `shared.setup.md`, plus a short line in each family guide and README.
- **F14.** `IOptionsFactory<T>.Create` in place of `IOptionsSnapshot<T>`. Unlike `IOptions<T>`, it keeps the configure action's fresh read on every resolve.
- **3b/3c.** F8, F10, F11, F12 and F13. F13 uses a "downloaded" flag like Playwright's, not a `Lazy<Task>`, which would keep a failed download forever.
- **5a.** The 13 log templates.
- **5c.** The `[Obsolete]` messages name keyed services.
- **5b.** Office.Clients names a missing `BaseUrl` (`InvalidOperationException`) at the point it failed before; the mail guide shows `ValidateOnStart`.
- **7a and F15, docs.** Plus decision 3(a): the three `400` bodies are documented and pinned by a test.

**Left out**

- **2b, the timeout options.** An app sets the timeout on the named client.
- **Step 4 (`TimeProvider`)**, **6a (`[GeneratedRegex]`)**, and **6.0/6b/6c** (decision 5(b)).
- **Decision 4(b).** The queue stays unbounded; F9 (finished tasks kept until removed) is documented.
- **Decision 6(b).** Reads stay uncancelled.
- **3a**, **5d's 409 field**, and **5e's cache**.
- **The Office.Clients rename.** It would break an app that configures a client by its interface name; the names are documented instead.
- **5b's fail-at-registration.** It would stop an app from starting that starts today.

**Kept from the original:** ViaAdValvas's `ContentType` request header, so the request stays byte-identical.

**Tests**

- New: `tests/Hosting.Testing/BackgroundQueueTests`, `tests/Invoicing.Testing` (new project), `FsFileProcessorNestingTests`, `MollieClientTests`, `MailRegistrationTests`, `OfficeClientRegistrationTests`. Added cases in the Regex, StringUtility, GitHub/Azure containment, Pom, page-view writer and identity controller tests.
- F12 and F13 have no test of their own: the race needs an SFTP server, and the second only changes throughput.

## Decisions

1. **Settings validation (*5b*).**
   - (a) The library checks only what already fails at first resolve, and the guides show `ValidateOnStart` for the rest. *Recommended.*
   - (b) The mail packages also validate their keys on start. **Behaviour change:** an app that registers a sender with an empty key, say in development, no longer starts.
2. **`FileProcessor` (F4).**
   - (a) Sequential on both targets, which is net10.0's behaviour today. A caller that wants parallelism adds it inside `handleFile`. *Recommended:* simplest, and no remote store gets flooded.
   - (b) `Parallel.ForEachAsync` with a fixed cap.

   Both are a **behaviour change** on net8.0.
3. **Security.Authentication.Web's 400 (*5d*).** The fix is `ValidationProblem(ModelState)` everywhere, which matches Entities.Web; the question is when.
   - (a) At 7.0, documented now. *Recommended*, unless the client in Regira-Modules already reads both shapes.
   - (b) Now, with a **Behaviour change** clause.
4. **A bounded `BackgroundTaskQueue` (*3b*).**
   - (a) `Options.Capacity` is opt-in (unbounded by default, as today). When the queue is full, the sync `QueueBackgroundWorkItem` throws, and a new default interface method, `ValueTask QueueBackgroundWorkItemAsync(…, CancellationToken)`, waits for room. *Recommended:* the source-compatible shape.
   - (b) Leave it unbounded and document the risk.
5. **The benchmark project (*6.0*).**
   - (a) Add `tools/Benchmarks`. *Recommended.*
   - (b) No benchmark project; skip *6b* and *6c*.
6. **Read cancellation in Entities.Web (*7b*).**
   - (a) Reads take `RequestAborted`. *Recommended.*
   - (b) Leave reads uncancelled.

Versions stay your call. Every change here is a member, constructor, option or fix on an existing type, so each is a patch by `AGENTS.md`, or rides the unpublished 6.6.0.

## How every step is tested

- **Baseline first.** Run the step's test projects on net8.0 and net10.0 before touching code, and note anything already red. Mail tests reach live Mailgun, so run offline with `--filter "TestCategory!=Network"`.
- **Defects: a failing test first.** A finding whose test does not fail is dropped.
- **HTTP.** The new `HttpClient` overloads let each provider's tests run against a fake handler. They pin the request a call sends, the timeout it gets, and, for Mollie, a repeated idempotency key.
- **Clock.** `FakeTimeProvider` replaces the real-time waits listed in *4*, and the suite gets faster by that much.
- **Regex.** The existing Common, Word and DAL tests run unchanged. A pattern no test reaches gets one before it moves.
- **Performance.** *6b* and *6c* change no output, so the existing tests pass unchanged and the benchmark numbers go into the step's *Outcome*.
- **Docs compile.** Run `dotnet run --project tools/GuideVerifier -- . --group <group>` for every guide group a step edits.
- **Changelog.** Every step adds its packages' bullets under *Unreleased*, with **Behaviour change:** clauses for F2, F5, *2a* (Office.Clients names), *3c* (Azure), *7b* and the decisions' (b) options.

<!-- {% endraw %} -->
