# Regira.System.Projects

Reads and updates `.csproj` files, part of [Regira System](https://regira.github.io/Regira-Packages/src/Common.System/). `ProjectParser` turns a project file's XML into a `Project` — package id, version, target frameworks, project references — and writes changes back; `ProjectService` lists, loads and saves project files through an `ITextFileService` from [Regira IO.Storage](https://regira.github.io/Regira-Packages/src/Common.IO.Storage/); `ProjectManager` builds a `ProjectTree`, a `TreeList<Project>` of the projects' dependencies.

## Installation

```xml
<PackageReference Include="Regira.System.Projects" Version="6.*" />
```

## Documentation

- [Project Files](https://regira.github.io/Regira-Packages/src/Common.System/docs/projects.html) — parsing, updating and saving project files, and building the dependency tree
- [TreeList](https://regira.github.io/Regira-Packages/src/TreeList/) — navigating a `ProjectTree`

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
