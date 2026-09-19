# Regira System — Project Files

Reading and manipulating `.csproj` files with `Regira.System.Projects`.

---

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

<!-- no-compile -->
```csharp
proj.Version = new Version("5.1.0");
XDocument updated = parser.Update(xml, proj);
updated.Save("MyLib.csproj");
```

### ProjectService

<!-- no-compile -->
```csharp
// ITextFileService comes from the Regira.IO.Storage package
var service = new ProjectService(parser, textFileService);

Project       single  = await service.Details("src/MyLib/MyLib.csproj");
IEnumerable<Project> all = await service.List();     // scans root recursively

await service.Save(proj);   // writes changes back to disk
```

### ProjectManager + ProjectTree

Build a dependency tree from all projects in the solution:

<!-- no-compile -->
```csharp
var manager = new ProjectManager(projectService);
ProjectTree tree = await manager.BuildTree();

// tree is a TreeList<Project> — see TreeList docs for navigation
var roots = tree.Roots;                              // projects with no dependencies
var leaves = tree.GetBottom().Select(n => n.Value.Id);  // projects nobody depends on
```

`ProjectTree` extends `TreeList<Project>` — see [TreeList docs](https://regira.github.io/Regira-Packages/src/TreeList) for the full navigation API.

---

## Overview

1. [Index](../README.md) — Overview, projects, and installation
1. [Process Execution](processes.md) — Running commands and executables, capturing their output
1. [Hosting](hosting.md) — `WebHostOptions`, background task queues, Windows Service installer
1. **[Project Files](projects.md)** — Parsing and managing `.csproj` files

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
