# Regira Source Repository — Agent Guide

This file is for AI agents working **on** the Regira source codebase (adding modules, updating guides, fixing bugs, writing tests). It is not for consumer projects — see `ai/AGENTS.md` for the consumer bootstrap.

---

## What this repository is

A collection of .NET NuGet packages published to nuget.org. Each package:
- Contains source code under `src/{ModuleName}/`
- Embeds AI instruction files in `src/{ModuleName}/ai/`
- Ships an MSBuild `.props` and `.targets` file in `src/{ModuleName}/build/` that extracts those AI files into consumer projects on `dotnet build`

The `ai/` folder at the repo root holds two distinct documents: `ai/AGENTS.md`, the consumer-facing bootstrap guide the MCP server serves verbatim to downstream projects (not a source-repo contributor guide), and `ai/learnings.md`, the contributor memory log for durable lessons from working on this repo.

---

## Source layout

Project folders follow a two-tier naming convention: `Common.{Hub}/` for the shared hub projects that hold the abstractions (`Common.Entities`, `Common.Office`, `Common.Media`, `Common.Security`, ...), and `{Family}.{Provider}/` for the ~60 backend implementation packages (`Entities.EFcore`, `PDF.Spire`, `Excel.MiniExcel`, `Barcodes.ZXing`, `Mail.SendGrid`, `DAL.EFcore`, ...).

```
src/
  Common.Setup/          # Shared project templates and setup guides
    ai/                  # project.setup.md, shared.setup.md, CLAUDE.md, copilot-instructions.md (consumer-facing)
    build/               # Regira.Setup.props, Regira.Setup.targets
  Common.Entities/       # CRUD entity framework (hub)
    ai/                  # entities.instructions.md, entities.signatures.md, ...
    build/               # Regira.Entities.props, Regira.Entities.targets
  Common.Office/         # Office operations, PDF, Excel, Word, Mail, ... (hub)
    ai/                  # office.instructions.md, per-submodule guides, ...
    build/               # Regira.Office.props, Regira.Office.targets
  Entities.EFcore/       # Provider package — same ai/ + build/ pattern
  PDF.Spire/             # Provider package — same ai/ + build/ pattern
ai/
  AGENTS.md              # Consumer bootstrap guide, served by the MCP server (not for source work)
  learnings.md           # Contributor memory log — read this before starting
src/Common.Setup/
  ai/
    commands/            # Slash commands (/new-entity, /new-project, /sync-guides, /update-guide, /evaluate)
.claude/
  settings.json
```

---

## Working on a module

### Reading the right guides

Each module's `src/{Module}/ai/` folder contains the authoritative reference for that module's design. Read the relevant `*.instructions.md` before touching a module's source. Use `*.signatures.md` and `*.examples.md` for exact API detail.

Read `ai/learnings.md` before starting any substantial work. Update it when a task reveals a durable lesson.

### Adding a new module

1. Create `src/{ModuleName}/` with a `.csproj`, source files, `build/`, and `ai/`
2. Write the AI guides in `src/{ModuleName}/ai/` — at minimum `{module}.instructions.md` and `{module}.examples.md`
3. Create `src/{ModuleName}/build/Regira.{ModuleName}.targets` following the pattern in any existing `.targets` file
4. Create `src/{ModuleName}/build/Regira.{ModuleName}.props` following the pattern in any existing `.props` file (sets `DefaultItemExcludes` to prevent `.regira\**` and `.claude\**` from appearing as project items)
5. Add the props file, targets file, and AI files to the `.csproj` under `buildTransitive\` and `ai\` respectively
6. Add the module to the routing tables in `ai/AGENTS.md` and `src/Common.Setup/ai/copilot-instructions.md`

### Updating AI guides

Use the `/update-guide` slash command to identify what changed and propose a guide patch. For small notes and pitfalls that don't warrant a guide section, add a row to `ai/learnings.md`.

### Documentation

When adding or updating features, make sure to update the documentation as well.
ai/ -> documentation for AI agents
README.md + src/{ModuleName}/docs/ -> documentation for developers
The documents for AI agents and the documents for developers should not refer to each other.

Write docs as if authored correctly from scratch — no correction notes or change history.

---

## Versioning & releases

Every package owns its own `<Version>` in its `.csproj` (SemVer). Published versions are **immutable on nuget.org** — a version can never be overwritten or reused.

- **Any change that ships** — source, the packed `ai/` guides, `build/` props/targets — must leave the changed package's `<Version>` **higher than its last published version**: patch for fixes and guide-only changes, minor for backward-compatible features, major for breaking changes. If the version was already bumped since the last publish, several edits may share that bump.
- Do not bump packages you did not change. Dependent packages are re-versioned by the release tooling (ProjectFilesProcessor in the private Regira-Tools repo) when it publishes to nuget.org.
- **Record every shipped change in [CHANGELOG.md](CHANGELOG.md) in the same change**: one bullet under the `## Unreleased` heading — `` `PackageId` x.y.z — one-line summary``. At publish time the Unreleased block becomes a dated release heading.

---

## Git

Commit or push only when the user explicitly asks. Leave finished work in the working tree and report
what is ready — a review verdict ("ready to commit") or a checklist step is not that ask.

---

## Slash commands

Source lives in `src/Common.Setup/ai/commands/`.

| Command | Purpose |
|---|---|
| `/new-entity` | Scaffold a full Regira entity in a consumer project |
| `/new-project` | Bootstrap a new consumer project |
| `/sync-guides` | Refresh stale extracted guides in a consumer project |
| `/update-guide` | Propose a guide patch after a source code change |
| `/evaluate` | Run a structured quality evaluation on a module |

`/update-guide` is a source-repo workflow only — it is **not** extracted into consumer projects (their guide copies are overwritten on each extraction). The other four are packed in `Regira.Setup` and extracted to a consumer's `.claude/commands/` on build.

---

## Key conventions

- Guides travel with packages — every public API change that affects usage patterns needs a corresponding guide update
- Every shipped change bumps the changed package's version and adds a `CHANGELOG.md` bullet — see *Versioning & releases*
- Never add consumer-scaffolding content to this file; it belongs in `ai/AGENTS.md`
- Keep `Program.cs` thin and use `IServiceCollection` extension methods
- Prefer abstractions over concrete types in cross-module dependencies
