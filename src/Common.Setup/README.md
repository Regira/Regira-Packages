# Regira.Setup

Shared setup guides and slash commands for AI coding agents working in a Regira consumer solution. The package carries no code: on `dotnet build`, its build targets copy these files from the package to the solution root:

- the shared setup guides `project.setup.md` and `shared.setup.md` → `.regira/instructions/`
- `copilot-instructions.md` → `.github/instructions/`
- the slash commands `new-project`, `new-entity`, `sync-guides` and `evaluate` → `.claude/commands/`

A file that already exists is never overwritten, and a file added by a package upgrade is copied on the next build. The `.regira` and `.claude` folders are excluded from the project's default items.

## Installation

```xml
<PackageReference Include="Regira.Setup" Version="6.*" />
```

Extraction runs only when the project is built as part of a solution (`$(SolutionDir)` is set); building the project on its own copies nothing.

## Documentation

- [Using Regira in your project](https://regira.github.io/Regira-Packages/#using-regira-in-your-project) — connecting the MCP server, the bootstrap file, and the per-package guides these files complement

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
