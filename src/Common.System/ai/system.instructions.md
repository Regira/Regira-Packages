# Regira System AI Agent Instructions

> Application hosting utilities, background task management, Windows Service support, and `.csproj` project parsing.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.System` | `Regira.System` | Running external processes (`IProcessHelper`) |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background queues, Windows Service |
| `System.Projects` | `Regira.System.Projects` | Parse and manage `.csproj` files |

---

## Installation

```xml
<!-- Host config, background queues, Windows Service -->
<PackageReference Include="Regira.System.Hosting" Version="6.*" />

<!-- Parse and manage .csproj files -->
<PackageReference Include="Regira.System.Projects" Version="6.*" />
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

<!-- no-compile -->
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

---

### Windows Service Installer

<!-- no-compile -->
```csharp
app.AddWindowsServiceInstaller(new WindowsServiceOptions
{
    ServiceName       = "MyApi",
    InstallFilename   = "install.bat",
    UninstallFilename = "uninstall.bat"
});
```

Generates `install.bat` / `uninstall.bat` scripts using `sc.exe`.

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
- A custom `IProcessHelper` that does not override the `environment` overloads: `ExecuteCommand` sets the
  variables in its script (batch syntax), and `ExecuteFile` throws `NotSupportedException` rather than drop them.
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
