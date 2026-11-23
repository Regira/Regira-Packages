# System — Example: Report API with Background Processing

> Context: An analytics API runs as a Windows Service, queues long-running report generation in the background, runs jobs on a schedule, and reads its own `.csproj` metadata to expose version info.

## appsettings.json

```json
{
  "Hosting": {
    "ServiceName": "Analytics API",
    "LocalPort": 5100,
    "EnableSwagger": true,
    "RoutePrefix": "api/v1"
  }
}
```

## Program.cs

```csharp
using Microsoft.Extensions.Hosting.WindowsServices;   // WindowsServiceHelpers

builder.Host.UseWebHostOptions();
if (WindowsServiceHelpers.IsWindowsService()) builder.Host.UseWindowsService();

builder.Services.UseBackgroundQueue();

var app = builder.Build();
app.AddWindowsServiceInstaller(new WindowsServiceOptions
{
    ServiceName       = "AnalyticsApi",
    InstallFilename   = "install.bat",
    UninstallFilename = "uninstall.bat"
});
```

## Queue a report export

<!-- no-compile -->
```csharp
[HttpPost("reports")]
public IActionResult StartExport([FromServices] IBackgroundQueueManager queue)
{
    // The work runs after this request ends, so it resolves its services from the scope the queue gives it (sp), never
    // from the controller's fields: a DbContext behind them is disposed by then.
    var task = queue.Execute<string>(async (sp, t) =>
    {
        var data = await sp.GetRequiredService<IReportDataService>().QueryAsync(t.Token);
        using var excel = await sp.GetRequiredService<IExcelService>()
            .Create([new ExcelSheet { Name = "Report", Data = data.Cast<object>().ToList() }], t.Token);
        return await sp.GetRequiredService<IFileService>().Save("reports/latest.xlsx", excel.GetBytes()!);
    });
    return Accepted(new { TaskId = task.Id });
}
```

## A recurring job

The nightly clean-up is a job class in the domain library, which references `Regira.System` only.

```csharp
using Regira.System.Scheduling.Abstractions;

public interface IReportStore
{
    Task DeleteOlderThan(TimeSpan age, CancellationToken token);
    Task Generate(int reportId, CancellationToken token);
}

public class PurgeOldReports(IReportStore reports) : IJobHandler
{
    // a run can be repeated: deleting what is already gone changes nothing
    public Task Execute(JobContext context, CancellationToken token)
        => reports.DeleteOlderThan(TimeSpan.FromDays(30), token);
}

public record GenerateReportInput(int ReportId);
public class GenerateReport(IReportStore reports) : IJobHandler<GenerateReportInput>
{
    public Task Execute(GenerateReportInput input, JobContext context, CancellationToken token)
        => reports.Generate(input.ReportId, token);
}
```

<!-- no-compile -->
```csharp
using Regira.System.Hosting.Scheduling.DependencyInjection;

builder.Services.AddScheduling(s =>
{
    s.AddRecurring<PurgeOldReports>("purge-old-reports", "0 3 * * *",
        o => o.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"));
});
```

## Enqueue a job from an endpoint

<!-- no-compile -->
```csharp
[HttpPost("reports/{id:int}/generate")]
public async Task<IActionResult> Generate(int id, [FromServices] IJobScheduler jobs)
{
    // the input is the id, not the report: the job loads what it needs in its own scope
    var jobId = await jobs.Enqueue<GenerateReport, GenerateReportInput>(new GenerateReportInput(id));
    return Accepted(new { JobId = jobId });
}
```

## Move the jobs to a persistent scheduler

The job classes and the endpoint stay as they are. The application adds Hangfire (or Quartz) with the storage it chooses,
and one call on the scheduling builder.

<!-- no-compile -->
```csharp
using Hangfire;
using Regira.System.Scheduling.Hangfire;

builder.Services.AddHangfire(c => c.UseSqlServerStorage(builder.Configuration.GetConnectionString("Jobs")));
builder.Services.AddHangfireServer();

builder.Services.AddScheduling(s =>
{
    s.AddRecurring<PurgeOldReports>("purge-old-reports", "0 3 * * *",
        o => o.TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels"));
}).UseHangfire();
```

## Expose API version from .csproj

<!-- no-compile -->
```csharp
[HttpGet("version")]
public async Task<IActionResult> Version([FromServices] ProjectService projects)
{
    var proj = await projects.Details("src/Analytics.API/Analytics.API.csproj");
    return Ok(new { proj.Id, Version = proj.Version?.ToString() });
}
```

## Parse and bump version in a publish script

```csharp
var parser  = new ProjectParser();
var xml     = XDocument.Load("src/Analytics.API/Analytics.API.csproj");
var proj    = parser.Parse(xml);
proj.Version = new Version(proj.Version!.Major, proj.Version.Minor, proj.Version.Build + 1);
parser.Update(xml, proj).Save("src/Analytics.API/Analytics.API.csproj");
```
