# Regira System AI Agent Instructions

> Application hosting utilities, background task management, Windows Service support, and `.csproj` project parsing.

## Projects

| Project | Package | Purpose |
|---|---|---|
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
| `LocalPort` | `int?` | `null` | Override listening port |
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

```csharp
services.UseBackgroundQueue<ReportTask>();

var task = taskManager.Execute<string>(async (sp, t) =>
{
    t.SetProgress(0.5);
    return await GenerateReport(sp, t.Id);
});
```

---

### Windows Service Installer

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

```csharp
proj.Version  = new Version("5.1.0");
XDocument updated = parser.Update(xml, proj);
updated.Save("MyLib.csproj");
```

---

### `ProjectService`

```csharp
var service = new ProjectService(parser, textFileService);

Project               single = await service.Details("src/MyLib/MyLib.csproj");
IEnumerable<Project>  all    = await service.List();   // scans root recursively

await service.Save(proj);  // writes changes back to disk
```

---

### `ProjectManager` + `ProjectTree`

Build a dependency tree from all projects in the solution:

```csharp
var manager  = new ProjectManager(projectService);
ProjectTree  tree = await manager.BuildTree();

var roots  = tree.Roots;
var leaves = tree.GetBottom().Select(n => n.Value.Id);
```

`ProjectTree` extends `TreeList<Project>` — see [treelist.instructions.md](../../TreeList/ai/treelist.instructions.md) for the full navigation API.

---
