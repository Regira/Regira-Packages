# Scheduled and deferred jobs in System.Hosting

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `60b3414`; the nuget.org nuspecs of Quartz 4.8.1 and 3.22.11, Quartz.Extensions.Hosting 4.8.1, Hangfire.Core 1.8.25, Hangfire.NetCore 1.8.25, Hangfire.InMemory 1.0.0 and Cronos 0.13.0, read on 2026-11-23; and `IJob.cs`, the migration guide and the Microsoft DI and hosted-service pages on the `main` branch of quartznet, read the same day. `main` describes 4.9 in places and can be ahead of 4.8.1. Hangfire's behaviour (its retry default, misfire modes, lock attribute, polling interval and licence) and Quartz's cron dialect are taken from their documentation as remembered and were not measured here. Nothing on this page has been built.

**Status: proposal, not scheduled. Eight decisions are open (see *Decisions*).**

## Recommendation

Add a small scheduling contract to the System family, with three things an application can ask for: run a job **now** in the background, run it **once at a time**, and run it **on a cron schedule**. A job is a class resolved from the container, with an optional input.

Ship it in three layers:

1. **The contract**, in `Regira.System`: `IJobHandler`, `IJobHandler<TInput>`, `IJobScheduler`, and the cron and option types. No dependency beyond the base class library, so a class library that holds job classes references nothing else.
2. **A built-in backend**, in `Regira.System.Hosting`: in-process and in-memory, for development, tests and a single-node application.
3. **Two adapter packages**, `Regira.System.Scheduling.Quartz` and `Regira.System.Scheduling.Hangfire`. Each bridges the contract to a Quartz.NET or Hangfire installation the application configures itself: its store, its clustering, its dashboard. The adapters wrap none of that.

The contract is the intersection of what Quartz and Hangfire both do, stated as rules a job can rely on whichever backend runs it (*The portable contract*). What only one of them does stays reachable through that library's own API, beside the contract.

The gap this closes: `System.Hosting` has an in-memory queue that runs delegates one at a time. Nothing in the packages runs work at a time or on a schedule, so every application that sends a nightly report or expires stale rows picks a scheduler and wires it by hand, and job code written against one scheduler does not move to the other.

The steps, in order:

1. A spike: one job on Quartz 4 and on Hangfire, and the cron translation.
2. The contract and the cron type.
3. The built-in backend.
4. The contract test suite.
5. The Hangfire adapter.
6. The Quartz adapter.
7. Guides, docs, routing table and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| A queue of delegates | `IBackgroundTaskQueue` and `BackgroundTaskQueue` in `src/System.Hosting/Background`: `QueueBackgroundWorkItem(Func<CancellationToken, Task>)` | Work is a closure. It cannot be stored, sent to another node or run later |
| One worker | `QueuedHostedService` runs the work items one after the other; one that throws is logged and the next runs | No concurrency, no retry |
| Tracked tasks with progress | `IBackgroundQueueManager<TTask>.Execute` returns an `IBackgroundTask` with `Status`, `Progress`, `Result` and `Error`; `BackgroundTaskManager<TTask>` keeps every task in a static dictionary until it is removed | "Start an export, poll its progress" is covered, in one process |
| A scope per work item | `BackgroundQueueManager` creates a scope from the root `IServiceScopeFactory` for each run | The built-in backend resolves handlers the same way |
| No time or schedule anywhere | No cron parser, timer or `TimeProvider` use in `src/System.Hosting` | Everything time-based is new |
| `Regira.System` has no package references | `src/Common.System`: process helpers, the family's guides and build targets | The contract can live there without adding a dependency to anyone |
| `Regira.System.Hosting` references the generic host | `Microsoft.Extensions.Hosting` and `Microsoft.Extensions.Hosting.WindowsServices` | The right home for a hosted service, the wrong one for an interface a domain library implements |
| The Entities guides point at a hosted service for delivery | `entities.instructions.md`, *Reactors — after the commit*: a reactor writes a row, "and a hosted service delivers each row at least once" | That hosted service is hand-written today. It is the first consumer in view |
| Tests | `tests/Hosting.Testing/BackgroundQueueTests.cs` | The new suite sits beside it |

## What the two schedulers share, and where they differ

| | Quartz.NET 4 | Hangfire 1.8 |
|---|---|---|
| A job is | A class implementing `IJob`, or `IJob<TInput>` with a typed input | A method call captured as an expression; the type, method and arguments are stored |
| Resolved from | The container, in a scope per run (`AddJob` registers the job scoped) | The container, in a scope per run |
| Input | `IJob<TInput>`, or strings in a `JobDataMap` | The call's arguments, serialized as JSON with Newtonsoft.Json |
| Run now | A trigger that starts now | `Enqueue` |
| Run once later | `ScheduleJob<TJob, TInput>(input, at)` | `Schedule` |
| Recurring | A cron trigger | `IRecurringJobManager.AddOrUpdate` |
| Cron dialect | Six or seven fields starting with seconds; `?` required in day-of-month or day-of-week; Sunday is 1 | Five fields, or six starting with seconds, parsed by Cronos; Sunday is 0 |
| Time zone | Per trigger; the machine's zone when none is given | Per recurring job; UTC when none is given |
| Storage | In memory, or a database | Required: a database, or the in-memory package |
| Several nodes | Clustered on the database store | By design: any server takes any job |
| A run that fails | Not retried unless the job asks | Retried, ten times by default |
| A missed occurrence | A misfire instruction per trigger | A misfire mode per recurring job |
| Overlapping runs | An attribute on the job class forbids them | An attribute takes a distributed lock |
| Licence | Apache-2.0 | LGPL-3.0 for the core packages |
| Target frameworks | 4.x: `net10.0` and, from 4.8, `net11.0`. 3.22: `netstandard2.0`, `net8.0`, `net9.0`, `net10.0` | `netstandard2.0` |

## The portable contract

A handler written to these rules runs unchanged on the built-in backend, Quartz and Hangfire.

1. **A job is a class.** It implements `IJobHandler` or `IJobHandler<TInput>` and is resolved from a new scope for every run, so it may inject a `DbContext`.
2. **The input is a small JSON-serializable value**: an id or a few fields, not an entity graph. A persistent backend stores it, and another node may read it.
3. **A run may happen more than once.** A retry, a recovery after a crash or a second node can repeat it. A handler is written to be repeated safely.
4. **A schedule is a five-field cron expression in an explicit time zone**, with an optional leading seconds field, in the subset every backend reads the same way (*Cron*).
5. **A recurring job does not overlap itself** unless the application asks for that.
6. **A missed occurrence runs once** when the scheduler is back, or is skipped. It is never replayed occurrence by occurrence.
7. **Cancellation is cooperative.** The token is signalled on shutdown, and on `Cancel` where the backend supports cancelling a running job.

What the contract leaves to the backend, and the guide states per backend: whether a failed run is retried, whether a job survives a restart, how many nodes run it, and what a dashboard shows.

## Design

### The contract

Namespace `Regira.System.Scheduling.Abstractions`, in `Regira.System`.

<!-- no-compile -->
```csharp
public interface IJobHandler
{
    Task Execute(JobContext context, CancellationToken token);
}
public interface IJobHandler<in TInput>
{
    Task Execute(TInput input, JobContext context, CancellationToken token);
}

/// <summary>What a run knows about itself.</summary>
public sealed record JobContext(string JobId, string? RecurringId, DateTimeOffset? ScheduledAt);

public interface IJobScheduler
{
    // now
    Task<string> Enqueue<TJob>(CancellationToken token = default) where TJob : IJobHandler;
    Task<string> Enqueue<TJob, TInput>(TInput input, CancellationToken token = default) where TJob : IJobHandler<TInput>;

    // once, later
    Task<string> Schedule<TJob, TInput>(TInput input, DateTimeOffset at, CancellationToken token = default) where TJob : IJobHandler<TInput>;
    Task<bool> Cancel(string jobId, CancellationToken token = default);

    // recurring
    Task AddOrUpdateRecurring<TJob>(string id, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default) where TJob : IJobHandler;
    Task AddOrUpdateRecurring<TJob, TInput>(string id, TInput input, CronSchedule schedule, RecurringJobOptions? options = null, CancellationToken token = default) where TJob : IJobHandler<TInput>;
    Task<bool> RemoveRecurring(string id, CancellationToken token = default);
    Task Trigger(string id, CancellationToken token = default);
}

public sealed class RecurringJobOptions
{
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;
    public MisfirePolicy Misfire { get; set; } = MisfirePolicy.RunOnce;   // or Skip
    public bool AllowConcurrent { get; set; }
}
```

- The interface is named `IJobHandler` so that it collides with neither `Quartz.IJob` nor Hangfire's `Job`.
- `Enqueue` and `Schedule` return an id that `Cancel` takes. `Cancel` returns `false` for a job that already ran or does not exist.
- A recurring job is identified by a string id the application chooses, and `AddOrUpdateRecurring` with the same id replaces it. Both schedulers work this way.
- There is no status, history or progress query. Both schedulers have a dashboard for that, and in-process progress is what `IBackgroundTask` already does.

### Cron

`CronSchedule` is a value type with a `Parse` that accepts the portable subset and throws `FormatException` for the rest, so an expression fails the same way on every backend, at registration.

- Five fields: minute, hour, day of month, month, day of week. A sixth, leading field is seconds.
- Values, `*`, lists (`,`), ranges (`-`), steps (`/`), and month and weekday names.
- Day of week is 0 to 6 with Sunday as 0; 7 is also Sunday.
- At most one of day-of-month and day-of-week is restricted. Quartz refuses an expression that restricts both, and cron dialects disagree on whether both means either or both.
- Not in the subset: `?`, `L`, `W`, `#`, year fields, and macros such as `@daily`.

The built-in backend and Hangfire read the expression as it is. The Quartz adapter translates it: a leading `0` for seconds when there are five fields, `?` in whichever day field is `*`, and weekday numbers shifted by one.

### Registration

<!-- no-compile -->
```csharp
services.AddScheduling(s =>
{
    s.AddRecurring<ExpireStaleCarts>("expire-stale-carts", "*/15 * * * *");
    s.AddRecurring<SendDailyReport>("daily-report", "0 7 * * MON-FRI",
        o => o.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"));
});
```

- `AddScheduling` (namespace `Regira.System.Hosting.Scheduling.DependencyInjection`) registers `IJobScheduler`, each handler as scoped, and the built-in backend.
- `AddRecurring` declares a schedule at startup. The declarations are applied through `AddOrUpdateRecurring` when the host starts, on whichever backend is registered, so a persistent backend ends up with the declared schedule after every deployment.
- An adapter replaces the backend and nothing else:

<!-- no-compile -->
```csharp
builder.Services.AddQuartz(q => q.UsePersistentStore(/* the application's own Quartz setup */));
builder.Services.AddQuartzHostedService();
builder.Services.AddScheduling(s => { /* as above */ }).UseQuartz();
```

<!-- no-compile -->
```csharp
builder.Services.AddHangfire(c => c.UseSqlServerStorage(connectionString));   // the application's own Hangfire setup
builder.Services.AddHangfireServer();
builder.Services.AddScheduling(s => { /* as above */ }).UseHangfire();
```

### The built-in backend

- One `BackgroundService`. It keeps the recurring jobs with their next occurrence and the one-off jobs with their time, sleeps until the earliest, and runs what is due.
- The clock is an injected `TimeProvider`, so a test advances time instead of waiting for it.
- Each run gets a scope, a handler from it, and a token linked to shutdown. An exception is logged with the job's id; nothing is retried.
- Runs execute concurrently up to a `MaxConcurrency` option. A recurring job whose previous run is still going is skipped unless `AllowConcurrent` is set.
- The input is serialized to JSON when the job is accepted and deserialized when it runs (decision 5).
- Nothing is stored. A one-off job accepted before a restart is lost, and a recurring job runs on every node that hosts the application. The guide says both in its first paragraph, and names the adapters as the answer.
- A missed occurrence only exists here when a run was held back by `MaxConcurrency` or the machine slept. `RunOnce` runs it once it can; `Skip` waits for the next.

### The Hangfire adapter

- `src/System.Scheduling.Hangfire/`, package `Regira.System.Scheduling.Hangfire`. It references `Hangfire.Core` only: the application chooses `Hangfire.AspNetCore` or `Hangfire.NetCore` and a storage package.
- `IJobScheduler` is implemented over `IBackgroundJobClient` and `IRecurringJobManager`, which Hangfire registers.
- A job is stored as a call to a generic runner in the adapter, `HangfireJobRunner<TJob, TInput>.Run(input, …)`. Hangfire stores the closed generic type and the input, and resolves the runner from its scope; the runner resolves `TJob` and calls `Execute`.
- `RecurringJobOptions` maps onto Hangfire's recurring options: the time zone, and the misfire mode. `AllowConcurrent = false` puts Hangfire's distributed lock on the runner. Each closed runner type is a lock of its own, so two different jobs never block each other.
- Hangfire's retry filter applies as the application configured it. With Hangfire's default, a handler that throws runs again up to ten times: rule 3 is not optional on this backend.

### The Quartz adapter

- `src/System.Scheduling.Quartz/`, package `Regira.System.Scheduling.Quartz`, on Quartz 4 (decision 4).
- `IJobScheduler` is implemented over the `IScheduler` that `AddQuartz` registers.
- A job is a generic runner in the adapter, `QuartzJobRunner<TJob, TInput> : IJob<TInput>`, which resolves `TJob` from the job's scope and calls `Execute`.
- `Enqueue` is a trigger that starts now; `Schedule` is Quartz 4's one-off `ScheduleJob<TJob, TInput>(input, at)`; `Cancel` unschedules the trigger it returned.
- A recurring job is a durable job with one cron trigger. The trigger always carries the option's time zone, because Quartz would otherwise use the machine's. The misfire policy maps onto the cron trigger's misfire instruction, and `AllowConcurrent = false` onto Quartz's own rule against concurrent execution.

## Dependencies

- **Cronos** 0.13.0, MIT, no dependencies, assets from `netstandard1.0` to `net6.0`. It is the parser Hangfire uses, so the built-in backend and Hangfire compute the same occurrences, daylight-saving changes included. Referenced by `Regira.System.Hosting` (decision 3).
- **Quartz** 4.8.1, Apache-2.0. `net10.0` and `net11.0` only. Its own migration guide tells a library that still serves `net8.0` to reference Quartz 3.x for that target and Quartz 4 for `net10.0`, "only while the difference stays inside the library".
- **Hangfire.Core** 1.8.25, `netstandard2.0`, depends on Newtonsoft.Json. Its nuspec names a licence file; the core packages are LGPL-3.0, to be read from the package in step 1.
- **Hangfire.InMemory** 1.0.0 and Quartz's in-memory store, in the test project only.

## Considered and left out

- **Status, history and progress in the contract.** A dashboard does it better, and the two schedulers' models of a job's life do not line up.
- **Continuations, batches and workflows.** Hangfire has continuations, with batches in its paid edition; Quartz has listeners. No common shape.
- **Named queues and priorities.** Both have them, with different meanings.
- **Persistence or a distributed lock in the built-in backend.** That is writing a third scheduler. An application that needs either registers an adapter.
- **Retry in the contract.** See decision 6.
- **An outbox for Entities reactors.** A reactor that enqueues a job after a commit is the obvious use, and a durable one needs the enqueue and the save to share a transaction. That is its own proposal, on top of this one.
- **Replacing `UseBackgroundQueue`.** It runs closures and tracks progress in process, which the contract deliberately does not. The two stay side by side, and the guide says which to pick: a closure with progress the caller polls, or a named job that may run later or elsewhere.
- **Adapters for other schedulers** (TickerQ, Coravel, NCronJob, a cloud timer). The contract is public, so one more adapter is one more `IJobScheduler`.

## Decisions

1. **Where the contract lives.**
   - Option 1: `Regira.System` (`src/Common.System`), which has no package references.
   - Option 2: `Regira.System.Hosting`, beside the built-in backend.

   Recommendation: option 1. A handler usually lives in the application's domain or service library, which should not take the generic host and the Windows-service package to implement an interface.
2. **Ship a built-in backend at all?** Without one, the contract needs Quartz or Hangfire, and a storage decision, before the first job runs. With one, development, tests and a single-node deployment need nothing else, and the move to an adapter is one registration. Recommendation: ship it, and keep it in memory.
3. **The cron parser of the built-in backend:** Cronos (recommended: maintained by Hangfire's authors, correct across daylight-saving changes, no dependencies), NCrontab 3.4.0, or a parser written here. The subset in *Cron* is small enough to write, but the occurrence arithmetic across time zones is not worth owning.
4. **Quartz 4 only, or Quartz 3 on `net8.0` as well?**
   - Option 1: the adapter targets `net10.0`, and `net11.0` once the family has it, on Quartz 4. It is then the one package in the family without a `net8.0` asset: `proposals/dotnet-11.md` keeps `net8.0` through 6.x and drops it in a later 7.0. A `net8.0` application has the built-in backend and the Hangfire adapter, and a reference to the Quartz adapter fails at restore with a message that names the frameworks.
   - Option 2: both, with a conditional reference and `#if`. On Quartz 3 the input travels as a JSON string in the `JobDataMap`, `Execute` returns `Task`, and one-off scheduling is written by hand.

   Recommendation: option 1. .NET 8 support ended on 2026-11-10 and Quartz itself has left it. Option 2 is a second adapter in one assembly, written for a target the family already plans to drop.
5. **Does the built-in backend serialize the input?** In memory it could pass the object along. Then a job that takes an entity with a navigation cycle works in development and fails on the first deployment with an adapter. Recommendation: serialize with `System.Text.Json`, always, so the built-in backend refuses what a persistent one would. Hangfire serializes with Newtonsoft.Json: the guide tells a job to keep its input to plain properties, which both read alike.
6. **Retry.** Quartz does not retry and Hangfire retries ten times.
   - Option 1: the contract says nothing; each backend keeps its own policy, the guide's table states it, and rule 3 covers both.
   - Option 2: a `MaxAttempts` option the adapters enforce: a retry filter on Hangfire, a refire on Quartz, a loop in the built-in backend.

   Recommendation: option 1 for the first version. Option 2 means agreeing on backoff, and on what counts as an attempt after a crash, across three engines.
7. **`AllowConcurrent` defaults to `false`.** It is the safe reading of "run this every five minutes", and both schedulers can enforce it. On Hangfire it costs a distributed lock per run. Recommendation: `false`.
8. **Package names.** `Regira.System.Scheduling.Quartz` and `Regira.System.Scheduling.Hangfire` follow the family (`Regira.System.Hosting`, `Regira.System.Projects`). The alternative, `Regira.Scheduling.*`, starts a new family for two adapters. Recommendation: the first.

## Steps

1. **Spike.** A throwaway project with Quartz 4.8.1 (in-memory store) and Hangfire 1.8.25 (Hangfire.InMemory):
   - a generic runner on each, resolved from a scope, with a typed input;
   - Hangfire's recurring options: the time zone, the misfire modes, and what its lock attribute keys on for a closed generic type;
   - for a table of cron expressions in the subset, the next occurrences from Cronos and from Quartz's translated expression, in UTC and in a zone with daylight saving;
   - the licence text in the Hangfire.Core package.
2. **The contract.** The interfaces, `JobContext`, `RecurringJobOptions`, `MisfirePolicy` and `CronSchedule` in `src/Common.System`, with tests for the subset: what parses, what is refused, and the message.
3. **The built-in backend.** `AddScheduling`, `AddRecurring` and the hosted service in `src/System.Hosting`, with the Cronos reference. Tests on a fake `TimeProvider`: a recurring job fires on its occurrences in its time zone and across a daylight-saving change; a one-off job fires once; `Cancel`, `RemoveRecurring` and `Trigger`; no overlap; a handler that throws does not stop the scheduler; shutdown cancels the token.
4. **The contract suite.** An abstract fixture in `tests/Hosting.Testing` that states the portable contract as tests against an `IJobScheduler`. The built-in backend is its first subclass; each adapter adds one. It includes a handler with a scoped dependency, an input that round-trips, and a recurring job replaced under the same id.
5. **The Hangfire adapter.** The package, `UseHangfire`, the runner. The contract suite on Hangfire.InMemory.
6. **The Quartz adapter.** The package, `UseQuartz`, the runner, the cron translation with the spike's table as a test. The contract suite on Quartz's in-memory store.
7. **Docs.**
   - `system.instructions.md`: a `### Scheduling` section under *System.Hosting* with the portable contract, the cron subset, registration, and a backend table (storage, nodes, retry, misfire, licence); the *Background Task Queue* section gains the sentence that says which of the two to pick.
   - `system.examples.md`: a recurring job, a job enqueued from an endpoint, and the same application moved to an adapter.
   - `src/Common.System/README.md` and `docs/hosting.md`, a new `docs/scheduling.md`, and a `README.md` for each adapter project.
   - The `System` row of the routing table in `ai/AGENTS.md`, saying when to pick an adapter.
   - The `system` group in `tools/GuideVerifier/projects.json`, and a group for the adapters, whose dependencies do not belong in the first.
   - The pointer in `entities.instructions.md`, *Reactors — after the commit*, from "a hosted service" to this.
   - CHANGELOG bullets for `Regira.System`, `Regira.System.Hosting` and the two new packages. The contract and `AddScheduling` are something a consumer adopts, so both existing packages take a minor.

## Risks

- **The abstraction leaks at the edges.** Retry, persistence and multi-node behaviour differ per backend and the contract does not hide that. The mitigation is the rules: a handler written for "may run more than once, on any node, after a restart" is correct on all three. The guide leads with them.
- **Cron translation.** Weekday numbering and the `?` rule are where a schedule silently fires on the wrong day. The subset is narrow so that the translation is mechanical, and step 6 tests it against Cronos as the oracle.
- **Time zones on Linux and Windows.** `TimeZoneInfo.FindSystemTimeZoneById` takes IANA and Windows ids on current .NET, but a container image without time-zone data has only UTC. The guide's example carries that note.
- **Stored type names.** Both schedulers store the runner's closed generic type, which includes the handler's and the input's full names. Renaming or moving either breaks the jobs already stored. The guide says to drain or re-register on such a change; Quartz 4 has a type-rename map, which the adapter leaves to the application's own Quartz configuration.
- **Hangfire's licence.** LGPL-3.0 is compatible with an Apache-2.0 adapter that references it as a package, but some consumers' policies refuse LGPL dependencies outright. It is the adapter's dependency alone; the contract and the built-in backend carry none of it. The routing table says so.
- **Two schedulers' release cadences.** Quartz 4 reached 4.8 with a migration note per minor. The adapter touches a small surface (`IScheduler`, `IJob<TInput>`, the cron builder), and the contract suite runs on each dependency bump.
- **Timing tests.** A scheduler test that waits on the wall clock is slow and flaky. Every built-in test runs on a fake `TimeProvider`; the adapter suites trigger jobs rather than wait for a cron occurrence wherever the contract allows it.
- **The Quartz pages read here are from `main`.** Step 1 confirms each API named above against the 4.8.1 package.
