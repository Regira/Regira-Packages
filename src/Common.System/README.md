# Regira System

Regira System provides process execution helpers, application hosting utilities, background task management, Windows Service support, and .csproj project parsing.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.System` | `Regira.System` | Process execution helpers |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background queues, Windows Service |
| `System.Projects` | `Regira.System.Projects` | Parse and manage .csproj files |

## Installation

```xml
<PackageReference Include="Regira.System" Version="6.*" />
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
<PackageReference Include="Regira.System.Projects" Version="6.*" />
```

---

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.System/)** — Overview, projects, and installation
1. [Process Execution](https://regira.github.io/Regira-Packages/src/Common.System/docs/processes.html) — Running commands and executables, capturing their output
1. [Hosting](https://regira.github.io/Regira-Packages/src/Common.System/docs/hosting.html) — `WebHostOptions`, background task queues, Windows Service installer
1. [Project Files](https://regira.github.io/Regira-Packages/src/Common.System/docs/projects.html) — Parsing and managing `.csproj` files

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
