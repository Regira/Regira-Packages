# Regira System — Hosting

Host configuration, background work and Windows Service deployment from `Regira.System.Hosting`. These apply to any host, including an ASP.NET Core application.

---

### WebHostOptions

Bind from `appsettings.json` under `"Hosting"`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
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
var builder = WebApplication.CreateBuilder();
builder.Host.UseWebHostOptions();
```

### Background task queue

Queue and execute long-running work without blocking requests.

<!-- no-compile -->
```csharp
services.UseBackgroundQueue();

// In a controller
public IActionResult StartExport([FromServices] IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        await GenerateReport(token);
    });
    return Accepted();
}
```

Typed tasks carry progress and a status you can poll:

<!-- no-compile -->
```csharp
services.UseBackgroundQueue<ReportTask>();

// inject IBackgroundQueueManager<ReportTask>
var task = queueManager.Execute<string>(async (sp, t) =>
{
    t.SetProgress(0.5);
    return await GenerateReport(sp, t.Id);
});
```

A worked end-to-end example — enqueue from a controller, then poll the task's status — is in the
[Web examples](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html).

### Windows Service installer

```csharp
var app = WebApplication.Create();
app.AddWindowsServiceInstaller(new WindowsServiceOptions
{
    ServiceName        = "MyApi",
    InstallFilename    = "install.bat",
    UninstallFilename  = "uninstall.bat"
});
```

Generates `install.bat` / `uninstall.bat` scripts using `sc.exe`.

---

## Overview

1. [Index](../README.md) — Overview, projects, and installation
1. [Process Execution](processes.md) — Running commands and executables, capturing their output
1. **[Hosting](hosting.md)** — `WebHostOptions`, background task queues, Windows Service installer
1. [Project Files](projects.md) — Parsing and managing `.csproj` files

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
