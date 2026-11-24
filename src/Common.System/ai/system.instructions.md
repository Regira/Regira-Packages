# Regira System AI Agent Instructions

> Application hosting utilities, background task management, job scheduling (now, later, or on a cron schedule), Windows Service support, and `.csproj` project parsing.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.System` | `Regira.System` | Running external processes (`IProcessHelper`); the scheduling contract (`IJobHandler`, `IJobScheduler`) |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background queues, job scheduling with a built-in backend, Windows Service |
| `System.Scheduling.Quartz` | `Regira.System.Scheduling.Quartz` | Runs the scheduling contract on Quartz.NET 4 |
| `System.Scheduling.Hangfire` | `Regira.System.Scheduling.Hangfire` | Runs the scheduling contract on Hangfire |
| `System.Projects` | `Regira.System.Projects` | Parse and manage `.csproj` files |

---

## Installation

```bash
# Host config, background queues, job scheduling, Windows Service
dotnet add package Regira.System.Hosting

# Only for a persistent or multi-node scheduler: one of the two adapters
dotnet add package Regira.System.Scheduling.Quartz
dotnet add package Regira.System.Scheduling.Hangfire

# Parse and manage .csproj files
dotnet add package Regira.System.Projects
```

---

## System.Hosting

### `WebHostOptions` — `appsettings.json` `"Hosting"` section

| Property | Type | Default | Description |
|---|---|---|---|
| `ServiceName` | `string?` | `null` | App / Windows Service display name |
| `Mode` | `string` | `"Production"` | Hosting mode (inherited from `HostOptions`; see `HostingModes`) |
| `LocalPort` | `int?` | `null` | Override listening port |
| `SelfHosting` | `bool` | `false` | Flags the app as self-hosted (e.g. Kestrel / Windows Service) |
| `EnableSwagger` | `bool` | `true` | Toggle Swagger UI |
| `EnableCors` | `bool` | `false` | Toggle CORS |
| `EnableHttps` | `bool` | `false` | Toggle HTTPS redirect |
| `RoutePrefix` | `string?` | `null` | API route prefix |

```json
{
  "Hosting": {
    "ServiceName": "MyApi",
    "LocalPort": 5000,
    "EnableSwagger": true,
    "EnableCors": false,
    "RoutePrefix": "api/v1"
  }
}
```

```csharp
builder.Host.UseWebHostOptions();
```

---

### Background Task Queue

Queue and execute long-running work without blocking HTTP requests.

<!-- no-compile -->
```csharp
services.UseBackgroundQueue();

// Enqueue in a controller
public IActionResult StartExport(IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        await GenerateReport(token);
    });
    return Accepted();
}
```

Typed tasks with progress tracking:

<!-- no-compile -->
```csharp
services.UseBackgroundQueue<ReportTask>();

// Execute lives on IBackgroundQueueManager<TTask>; IBackgroundTaskManager<TTask> only lists/finds
// tasks (List, Find, Add, Remove, Clear) and has no Execute.
var queueManager = serviceProvider.GetRequiredService<IBackgroundQueueManager<ReportTask>>();
var task = queueManager.Execute<string>(async (sp, t) =>
{
    t.SetProgress(0.5);
    return await GenerateReport(sp, t.Id);
});
```

Work items run one at a time. One that throws is logged and the next one runs; a typed task records the exception and ends with status `Error`. The queue is unbounded and the task manager keeps every typed task until it is removed, so an app that queues per request removes finished tasks (`Remove(id)` after the result is read, or `Clear()`).

**Queue or scheduling?** The queue runs a closure in this process, with progress the caller polls. [Scheduling](#scheduling)
runs a named job class that may run later, on a schedule, after a restart or on another node. Both can be registered.

---

### Scheduling

Run a job class **now**, **once at a time** or **on a cron schedule**. The job is written against a small contract, and the
backend that runs it is one registration: the built-in one, Quartz.NET or Hangfire.

| Piece | Package | Namespace |
|---|---|---|
| `IJobHandler`, `IJobHandler<TInput>`, `JobContext`, `IJobScheduler`, `CronSchedule`, `RecurringJobOptions` | `Regira.System` (no dependencies: a domain library references only this) | `Regira.System.Scheduling.Abstractions` |
| `AddScheduling(...)`, the built-in backend | `Regira.System.Hosting` | `Regira.System.Hosting.Scheduling.DependencyInjection` |
| `.UseQuartz()` | `Regira.System.Scheduling.Quartz` (`net10.0` and later) | `Regira.System.Scheduling.Quartz` |
| `.UseHangfire()` | `Regira.System.Scheduling.Hangfire` | `Regira.System.Scheduling.Hangfire` |

> **The built-in backend keeps everything in memory.** A job that was accepted is lost on a restart, a failed run is logged
> and not retried, and a recurring job runs on **every** node that hosts the application. It fits development, tests and a
> single-node application. For anything else register an adapter: the jobs stay as they are.

#### A job

```csharp
using Regira.System.Scheduling.Abstractions;

public interface ICartStore
{
    Task ExpireOlderThan(TimeSpan age, CancellationToken token);
    Task Expire(int cartId, CancellationToken token);
}

// without input
public class ExpireStaleCarts(ICartStore carts) : IJobHandler
{
    public Task Execute(JobContext context, CancellationToken token)
        => carts.ExpireOlderThan(TimeSpan.FromHours(2), token);
}

// with input: a small value, never an entity
public record ExpireCartInput(int CartId);
public class ExpireCart(ICartStore carts) : IJobHandler<ExpireCartInput>
{
    public Task Execute(ExpireCartInput input, JobContext context, CancellationToken token)
        => carts.Expire(input.CartId, token);
}
```

What a job can rely on, whichever backend runs it:

1. **A job is a class**, resolved from a **new scope for every run**: it may inject a `DbContext`. A handler that is not
   registered is created with the scope's services.
2. **The input is a small JSON-serializable value** with plain properties (an id, a few fields). It is stored, and another
   node may read it. The built-in backend serializes it too, so an input that cannot be stored is refused in development.
3. **A run may happen more than once** (a retry, a recovery after a crash, a second node). Write the handler so that
   repeating it is safe.
4. **A schedule is a cron expression in an explicit time zone** (UTC unless set), in the subset below.
5. **A recurring job does not overlap itself** unless `AllowConcurrent` is set: a run that would start while the previous
   one is still going is skipped or held back, per backend.
6. **A missed occurrence runs once** when the scheduler is back (`MisfirePolicy.RunOnce`, the default) or is skipped
   (`MisfirePolicy.Skip`). It is never replayed occurrence by occurrence.
7. **Cancellation is cooperative**: the token is signalled on shutdown and by `Cancel(jobId)`. Pass it on.

`JobContext` carries `JobId` (the id `Enqueue` or `Schedule` returned; a new one for every recurring run), `RecurringId`
(`null` for a one-off job) and `ScheduledAt` (the time the run was due, when the backend knows it; on Hangfire a recurring
run carries the time Hangfire created it, up to its polling interval later).

#### Registration

<!-- no-compile -->
```csharp
using Regira.System.Hosting.Scheduling.DependencyInjection;

builder.Services.AddScheduling(s =>
{
    s.AddRecurring<ExpireStaleCarts>("expire-stale-carts", "*/15 * * * *");
    s.AddRecurring<SendDailyReport>("daily-report", "0 7 * * MON-FRI",
        o => o.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"));
    s.AddRecurring<ExpireCart, ExpireCartInput>("expire-demo-cart", new ExpireCartInput(1), "0 3 * * *");
});
```

- `AddScheduling` registers `IJobScheduler` and the built-in backend. `AddRecurring` registers its handler as scoped and
  declares the schedule; the declarations are applied through `AddOrUpdateRecurring` **when the host starts**, so a
  persistent backend holds the declared schedule after every deployment. A recurring job that is no longer declared stays
  in a persistent store until `RemoveRecurring(id)` is called.
- An expression outside the subset throws `FormatException` at registration, and a second declaration of the same id
  throws `ArgumentException`.
- A time zone id needs the machine's time-zone data: a container image without it (`tzdata`) knows only UTC.
- `s.InMemory.MaxConcurrency` (default: the processor count) caps how many runs the built-in backend executes at once;
  `s.InMemory.MisfireThreshold` (default one minute) is how late an occurrence may start before it counts as missed.

#### Enqueue, schedule, cancel — `IJobScheduler`

<!-- no-compile -->
```csharp
app.MapPost("/carts/{id:int}/expire", async (int id, IJobScheduler jobs) =>
{
    var jobId = await jobs.Enqueue<ExpireCart, ExpireCartInput>(new ExpireCartInput(id));
    return Results.Accepted(value: new { jobId });
});

var laterId = await jobs.Schedule<ExpireCart, ExpireCartInput>(new ExpireCartInput(id), DateTimeOffset.UtcNow.AddHours(2));
await jobs.Cancel(laterId);
```

| Member | Does |
|---|---|
| `Enqueue<TJob>()`, `Enqueue<TJob, TInput>(input)` | runs the job as soon as possible; returns its id |
| `Schedule<TJob, TInput>(input, at)` | runs it once, at `at`; returns its id |
| `Cancel(jobId)` | cancels a job that waits, or signals the token of one that runs (also a recurring run, by the `JobId` of its context); `false` for a job that already ran or does not exist |
| `AddOrUpdateRecurring<TJob>(id, schedule, options?)`, `AddOrUpdateRecurring<TJob, TInput>(id, input, schedule, options?)` | adds a recurring job, or replaces the one with the same id |
| `RemoveRecurring(id)` | removes it; `false` when no recurring job has the id |
| `Trigger(id)` | runs a recurring job now, outside its schedule; `false` when no recurring job has the id |

`RecurringJobOptions`: `TimeZone` (UTC), `Misfire` (`RunOnce`), `AllowConcurrent` (`false`). There is no status, history or
progress query: Quartz and Hangfire have a dashboard for that, and progress a caller polls is the
[background task queue](#background-task-queue).

#### Cron subset — `CronSchedule`

A string converts to a `CronSchedule` implicitly; `CronSchedule.Parse` / `TryParse` read one explicitly.

- **Five fields**: minute, hour, day of month, month, day of week. A sixth, **leading** field is seconds.
- Values, `*`, lists (`1,15`), ranges (`MON-FRI`), steps (`*/15`, `9-17/2`), and month and weekday names.
- Day of week is `0`-`6` with **Sunday as 0**; `7` is also Sunday.
- A value with a step runs to the end of its field: `5/20` in minutes is 5, 25 and 45. In day of week the field ends at
  `7`, so `1/2` is Monday, Wednesday, Friday **and Sunday**.
- A number is one or two digits, without a sign.
- **At most one of day-of-month and day-of-week is restricted**; the other stays `*`.
- **Refused**: `?`, `L`, `W`, `#`, a year field, macros such as `@daily`, and a range that runs backwards (`FRI-MON`:
  write `FRI-SAT,SUN-MON`).

#### Backends

| | Built-in | Quartz adapter | Hangfire adapter |
|---|---|---|---|
| Storage | none, in memory | the job store the app gives Quartz | the storage the app gives Hangfire |
| Survives a restart | no | with a persistent job store | yes |
| Several nodes | every node runs every recurring job | clustered on a database job store | any server takes any job |
| A run that throws | logged, not retried | not retried unless the app configures Quartz to | **retried, 10 times by default** |
| Overlap of a recurring job | the occurrence is skipped; a triggered run waits | the run is held until the previous one ends | the run is skipped |
| Time precision | the clock | the clock | whole seconds, polled every 15 s by default (`SchedulePollingInterval`) |
| Dashboard | none | Quartz's own | Hangfire's own |
| Targets | `net8.0`, `net10.0` | `net10.0` (Quartz 4) | `net8.0`, `net10.0` |
| Licence of the dependency | MIT (Cronos) | Apache-2.0 | **LGPL-3.0** or commercial (Hangfire.Core) |

An adapter replaces the backend and nothing else. The application configures Quartz or Hangfire itself — the store,
clustering, the dashboard — and everything the contract leaves out stays reachable through that library's own API.

<!-- no-compile -->
```csharp
using Quartz;
using Regira.System.Scheduling.Quartz;

builder.Services.AddQuartz(q => q.UsePersistentStore(/* the application's own Quartz setup */));
builder.Services.AddQuartzHostedService();
builder.Services.AddScheduling(s => { /* as above */ }).UseQuartz();
```

<!-- no-compile -->
```csharp
using Hangfire;
using Regira.System.Scheduling.Hangfire;

builder.Services.AddHangfire(c => c.UseSqlServerStorage(connectionString));   // the application's own Hangfire setup
builder.Services.AddHangfireServer();
builder.Services.AddScheduling(s => { /* as above */ }).UseHangfire();
```

- **Hangfire retries a failed run** as the application configured its retry filter; with the default, a handler that
  throws runs up to ten more times. Rule 3 is not optional there.
- **`Cancel` on Hangfire and on Quartz reaches the jobs this scheduler stored** and returns `false` for one the
  application gave Hangfire or Quartz itself. On Hangfire also for a stored job whose handler type no longer loads. Recurring ids are Hangfire's own: an id declared here and one passed to `RecurringJob.AddOrUpdate` are one job.
- **A job id grants nothing**: Hangfire's are sequential numbers. An endpoint that cancels by an id a client sends checks
  first that the job is that caller's.
- **Hangfire serializes the input with Newtonsoft.Json**, Quartz and the built-in backend with `System.Text.Json`. An
  input of plain properties reads the same in all three.
- **Stored jobs name the handler and input types.** Renaming or moving either breaks the jobs already stored in a
  persistent backend: let them drain first, or re-register the recurring ones (the declared ones are, at startup).
- **Quartz** stores a recurring job as a durable job with one cron trigger, both named by the job's id in group
  `regira.recurring`, and a one-off job as a trigger in group `regira.jobs`. The cron expression is translated to the
  Quartz dialect (`QuartzCron.Translate`).
- **`Regira.System.Scheduling.Hangfire` references `Hangfire.Core` only**: the application adds `Hangfire.AspNetCore` or
  `Hangfire.NetCore` and a storage package. Some policies refuse LGPL dependencies; the contract and the built-in backend
  carry none.
- **Another scheduler** is one more `IJobScheduler`: `AddScheduling(...).UseScheduler<TScheduler>()`, running each
  handler with `JobHandlerInvoker.Run<TJob>(scopedServices, context, token)`. `CronSchedule.GetDaysOfWeek()` gives the
  days an expression fires on, for a backend that numbers or names them differently.
- **The built-in backend refuses a job once it has stopped** (`InvalidOperationException`): nothing would run it. A
  hosted service that still enqueues while the host shuts down sees that exception.

---

### Windows Service Installer

```csharp
using Microsoft.Extensions.Hosting.WindowsServices;   // WindowsServiceHelpers

if (WindowsServiceHelpers.IsWindowsService()) builder.Host.UseWindowsService();

var app = builder.Build();
app.AddWindowsServiceInstaller(new WindowsServiceOptions
{
    ServiceName       = "MyApi",
    InstallFilename   = "install.bat",
    UninstallFilename = "uninstall.bat"
});
```

Generates `install.bat` / `uninstall.bat` scripts using `sc.exe`. The installer only writes the scripts: the host must
also call `UseWindowsService()` to run as a service. `Microsoft.Extensions.Hosting.WindowsServices` comes with
`Regira.System.Hosting`.

---

## Processes — `IProcessHelper`

`ProcessHelper` (`Regira.System`) implements `IProcessHelper` (`Regira.System.Abstractions`).

| Method | Runs |
|---|---|
| `ExecuteFile(filename, waitForOutput, arguments)` | the executable itself, no shell — on every platform |
| `ExecuteFile(filename, environment, waitForOutput, arguments)` | the same, with extra environment variables on the process |
| `ExecuteCommand(command, waitForOutput)` | the command as a temporary `.bat` file — Windows only |
| `ExecuteCommand(command, environment, waitForOutput)` | the same, with extra environment variables |

- **Pass a secret through `environment`,** never in the arguments or the command: a command line is readable by
  every process on the machine, and `ExecuteCommand` writes its command to disk. A tool that reads its password
  from a variable (`PGPASSWORD`) gets it that way.
- **Prefer `ExecuteFile` with arguments** over `ExecuteCommand`: no shell reads them, so `%` and `&` reach the tool
  as written. Quote a value that can hold spaces or quotes, escaping a `"` as `\"`.
- `waitForOutput: true` fills `IProcessOutput.Output` and `Error`; both pipes are drained at the same time, so a
  tool that logs to stderr cannot stall the call. Without it neither is kept.
- A custom `IProcessHelper` that does not override the `environment` overloads: both throw
  `NotSupportedException` when given variables, rather than drop them or write them into a command.
- There is no timeout: the call returns when the process exits.

<!-- no-compile -->
```csharp
IProcessHelper processes = new ProcessHelper();
var output = processes.ExecuteFile("/usr/bin/pg_dump",
    new Dictionary<string, string> { ["PGPASSWORD"] = password },
    waitForOutput: true,
    arguments: "--host \"db01\" --username \"app\" --no-password --file \"/tmp/shop.dump\" \"shop\"");
if (output.ExitCode != 0)
{
    throw new InvalidOperationException(output.Error);
}
```

---

## System.Projects — `.csproj` Parsing

### `ProjectParser`

```csharp
var parser = new ProjectParser();

XDocument xml  = XDocument.Load("MyLib.csproj");
Project   proj = parser.Parse(xml);

Console.WriteLine(proj.Id);                                    // PackageId
Console.WriteLine(proj.Version);                               // "5.0.3"
Console.WriteLine(string.Join(", ", proj.TargetFrameworks!));  // "net8.0, net10.0"
```

Update and write back:

<!-- no-compile -->
```csharp
proj.Version  = new Version("5.1.0");
XDocument updated = parser.Update(xml, proj);
updated.Save("MyLib.csproj");
```

---

### `ProjectService`

<!-- no-compile -->
```csharp
var service = new ProjectService(parser, textFileService);

Project               single = await service.Details("src/MyLib/MyLib.csproj");
IEnumerable<Project>  all    = await service.List();   // scans root recursively

await service.Save(proj);  // writes changes back to disk
```

---

### `ProjectManager` + `ProjectTree`

Build a dependency tree from all projects in the solution:

<!-- no-compile -->
```csharp
var manager  = new ProjectManager(projectService);
ProjectTree  tree = await manager.BuildTree();

var roots  = tree.Roots;
var leaves = tree.GetBottom().Select(n => n.Value.Id);
```

`ProjectTree` extends `TreeList<Project>` — see `get_package(id: "Regira.TreeList", section: "treelist.instructions")`,
or `treelist.instructions.md` locally, for the full navigation API.

---
