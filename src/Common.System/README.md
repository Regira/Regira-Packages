# Regira System

Regira System provides application hosting utilities, background task management, Windows Service support, and .csproj project parsing.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `System.Hosting` | `Regira.System.Hosting` | Host config, background queues, Windows Service |
| `System.Projects` | `Regira.System.Projects` | Parse and manage .csproj files |

See [Web](https://regira.github.io/Regira-Packages/src/Common.Web#systemhosting) for the full `System.Hosting` API reference and examples.

## Installation

```xml
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
<PackageReference Include="Regira.System.Projects" Version="6.*" />
```

---

## System.Hosting — quick reference

### WebHostOptions (appsettings `"Hosting"` section)

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

### Background task queue

```csharp
services.UseBackgroundQueue();
// or typed:
services.UseBackgroundQueue<MyTask>();
```

### Windows Service installer

```csharp
app.AddWindowsServiceInstaller(new WindowsServiceOptions
{
    ServiceName        = "MyApi",
    InstallFilename    = "install.bat",
    UninstallFilename  = "uninstall.bat"
});
```

Generates `install.bat` / `uninstall.bat` scripts using `sc.exe`.

---

## System.Projects

Parse, inspect, and update `.csproj` files programmatically. Useful for tooling, code-gen scripts, and build automation.

### ProjectParser

```csharp
var parser = new ProjectParser();

XDocument xml = XDocument.Load("MyLib.csproj");
Project proj  = parser.Parse(xml);

Console.WriteLine(proj.Id);               // PackageId
Console.WriteLine(proj.Version);          // "5.0.3"
Console.WriteLine(string.Join(", ", proj.TargetFrameworks!)); // "net8.0, net10.0"
```

Update and write back:

```csharp
proj.Version = new Version("5.1.0");
XDocument updated = parser.Update(xml, proj);
updated.Save("MyLib.csproj");
```

### ProjectService

```csharp
var service = new ProjectService(parser, textFileService);

Project       single  = await service.Details("src/MyLib/MyLib.csproj");
IEnumerable<Project> all = await service.List();     // scans root recursively

await service.Save(proj);   // writes changes back to disk
```

### ProjectManager + ProjectTree

Build a dependency tree from all projects in the solution:

```csharp
var manager = new ProjectManager(projectService);
ProjectTree tree = await manager.BuildTree();

// tree is a TreeList<Project> — see TreeList docs for navigation
var roots = tree.Roots;                              // projects with no dependencies
var leaves = tree.GetBottom().Select(n => n.Value.Id);  // projects nobody depends on
```

`ProjectTree` extends `TreeList<Project>` — see [TreeList docs](https://regira.github.io/Regira-Packages/src/TreeList) for the full navigation API.
