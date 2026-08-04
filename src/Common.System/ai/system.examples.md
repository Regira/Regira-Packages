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
builder.Host.UseWebHostOptions();

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

```csharp
[HttpPost("reports")]
public IActionResult StartExport([FromServices] IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        var data   = await _dataService.QueryAsync(token);
        var excel  = _excelService.Export(data);
        await _fileService.Save("reports/latest.xlsx", excel.Bytes!);
    });
    return Accepted();
}
```

## Expose API version from .csproj

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
