# System — Example: Report API with Background Processing

> Context: An analytics API runs as a Windows Service, queues long-running report generation in the background, and reads its own `.csproj` metadata to expose version info.

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
