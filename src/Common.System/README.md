# Regira System

Regira System provides process execution helpers, application hosting utilities, background task management, Windows Service support, and .csproj project parsing.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.System` | `Regira.System` | Process execution helpers |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background queues, Windows Service |
| `System.Projects` | `Regira.System.Projects` | Parse and manage .csproj files |

See [Web](https://regira.github.io/Regira-Packages/src/Common.Web#systemhosting) for the full `System.Hosting` API reference and examples.

## Installation

```xml
<PackageReference Include="Regira.System" Version="6.*" />
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
<PackageReference Include="Regira.System.Projects" Version="6.*" />
```

---

## Process execution (`Regira.System`)

### IProcessHelper / ProcessHelper

Run shell commands or executables and capture their output.

```csharp
public interface IProcessHelper
{
    IProcessOutput ExecuteCommand(string command, bool waitForOutput = false);
    IProcessOutput ExecuteFile(string filename, bool waitForOutput = false, string? arguments = null);
}
```

`ProcessHelper` is the default implementation. `ExecuteCommand` writes the command to a temporary `.bat` file (in `Options.TempFolder`, or a generated temp directory) and executes it; `ExecuteFile` starts the given executable directly. Pass `waitForOutput: true` to capture stdout/stderr.

```csharp
IProcessHelper processHelper = new ProcessHelper(new ProcessHelper.Options
{
    TempFolder = @"C:\Temp"   // optional; holds the temporary .bat file
});

IProcessOutput result = processHelper.ExecuteCommand("dotnet --version", waitForOutput: true);
Console.WriteLine(result.Output);     // captured stdout
Console.WriteLine(result.ExitCode);   // process exit code
```

### IProcessOutput / ProcessOutput

```csharp
public interface IProcessOutput
{
    string? Output { get; set; }
    string? Error { get; set; }
    int ExitCode { get; set; }
}
```

`Output` and `Error` are only populated when `waitForOutput` is `true`.

### ProcessHelperExtensions

Open a path or an `IBinaryFile` with the OS default application (a file without a path is written to a temp file first):

```csharp
IProcessHelper processHelper = new ProcessHelper();
IBinaryFile binaryFile = new BinaryFileItem { FileName = "invoice.pdf" };

processHelper.OpenFileByOS(@"C:\docs\invoice.pdf");
processHelper.OpenFileByOS(binaryFile);
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
var builder = WebApplication.CreateBuilder();
builder.Host.UseWebHostOptions();
```

### Background task queue

```csharp no-compile
services.UseBackgroundQueue();
// or typed:
services.UseBackgroundQueue<MyTask>();
```

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

```csharp no-compile
proj.Version = new Version("5.1.0");
XDocument updated = parser.Update(xml, proj);
updated.Save("MyLib.csproj");
```

### ProjectService

```csharp no-compile
// ITextFileService comes from the Regira.IO.Storage package
var service = new ProjectService(parser, textFileService);

Project       single  = await service.Details("src/MyLib/MyLib.csproj");
IEnumerable<Project> all = await service.List();     // scans root recursively

await service.Save(proj);   // writes changes back to disk
```

### ProjectManager + ProjectTree

Build a dependency tree from all projects in the solution:

```csharp no-compile
var manager = new ProjectManager(projectService);
ProjectTree tree = await manager.BuildTree();

// tree is a TreeList<Project> — see TreeList docs for navigation
var roots = tree.Roots;                              // projects with no dependencies
var leaves = tree.GetBottom().Select(n => n.Value.Id);  // projects nobody depends on
```

`ProjectTree` extends `TreeList<Project>` — see [TreeList docs](https://regira.github.io/Regira-Packages/src/TreeList) for the full navigation API.

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
