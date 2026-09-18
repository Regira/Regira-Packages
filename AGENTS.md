# Regira Source Repository — Agent Guide

This file is for AI agents working **on** the Regira source codebase (adding modules, updating guides, fixing bugs, writing tests). It is not for consumer projects — see `ai/AGENTS.md` for the consumer bootstrap.

---

## Keep things in sync (first priority)

Edits here reach further than this repo. Change one of these and update its counterparts in the same
turn; if a counterpart edit is out of scope, say so explicitly rather than leaving things inconsistent.

| You change… | Also update… |
|---|---|
| a public signature or behaviour | the module's `ai/*.md` and `README.md`. The MCP knowledge base is built from `ai/`, so a stale guide ships to every agent that asks |
| a package version | `CHANGELOG.md`; Regira-Website's `packages.json` is regenerated from this repo's `src/` by `npm run packages` over there |
| the path or filename of a doc page under `src/*/` | the links into it — regira.com's views and Regira-Blog post bodies point at this repo's Pages site with absolute URLs |

**Write docs in final state, not as a diff.** Don't narrate a history of changes — no changelogs,
"previously…/now…", "fixed", "updated", or migration notes. Update each document as if it had just
been authored cleanly, with no record of prior errors or revisions.

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

**Guides live on the hub, not on every package.** A family's `ai/` folder sits in its `Common.{Hub}/`
project and documents every provider behind it — `office.pdf.instructions.md` covers all seven PDF
backends. That is why most of the ~60 `{Family}.{Provider}/` projects carry no `ai/` folder at all, and
why adding one to a provider is the exception rather than step 2. Which path you are on decides the work:

**A new provider in an existing family** (`PDF.NewBackend`, `Mail.NewSender`):

1. Create `src/{Family}.{Provider}/` with a `.csproj` and source files
2. Document it **in the hub's guide** — the provider table in `{family}.instructions.md`, plus its
   registration call and anything that behaves unlike its siblings. Do not start a second guide for it
3. Add it to the `Main packages and defaults` column of both routing tables (below), saying when to pick it
4. A provider needs its own `ai/` only for something the hub guide genuinely cannot carry — the exact
   `using` set of a provider-only namespace (`Entities.EFcore`, `Entities.Web` ship a `namespaces.md`
   and nothing else) or a package card (`Security.Authentication*`). A provider `build/` folder is for
   MSBuild work unrelated to guides, such as carrying a native companion file into the output
   (`PDF.SelectPdf`, `OCR.Tesseract`) — it is not the guide-extraction pattern below

**A new hub** (a family that does not exist yet, or a standalone package like `TreeList`):

1. Create `src/{ModuleName}/` with a `.csproj`, source files, `build/`, and `ai/`
2. Write the AI guides in `src/{ModuleName}/ai/` — at minimum `{module}.instructions.md` and `{module}.examples.md`
3. Create `src/{ModuleName}/build/Regira.{ModuleName}.targets` following the pattern in any existing `.targets` file
4. Create `src/{ModuleName}/build/Regira.{ModuleName}.props` following the pattern in any existing `.props` file (sets `DefaultItemExcludes` to prevent `.regira\**` and `.claude\**` from appearing as project items)
5. Add the props file, targets file, and AI files to the `.csproj` under `buildTransitive\` and `ai\` respectively
6. Add the module to the routing tables in `ai/AGENTS.md` and `src/Common.Setup/ai/copilot-instructions.md`
7. Add a snippet group to `tools/GuideVerifier/projects.json` so the guide's ```` ```csharp ```` blocks are
   compiled — list the new guide files and the src projects they compile against

### Updating AI guides

Use the `/update-guide` slash command to identify what changed and propose a guide patch. For small notes and pitfalls that don't warrant a guide section, add a row to `ai/learnings.md`.

### Documentation

When adding or updating features, make sure to update the documentation as well.
ai/ -> documentation for AI agents
README.md + src/{ModuleName}/docs/ -> documentation for developers
The documents for AI agents and the documents for developers should not refer to each other.

**The developer README is an index, not the manual.** It carries the projects table, installation, a
short "which one do I want" orientation, the `## Overview` link list and the licence — then each subject
gets its own page under `docs/`. `Common.Entities` and `Common.Security` are the shape to copy. A README
that grows a full API reference is the thing to split, because it is also the nuget.org package page
(`PackageReadmeFile`), so length there is a cost on every package listing.

Link convention: the README's `## Overview` uses absolute `https://regira.github.io/Regira-Packages/…`
URLs; a `docs/` page repeats the same list with **relative** links (`../README.md`, `jwt.md`) and bolds
itself. A cross-module link from any README uses the absolute form, since READMEs are also served from
nuget.org where a relative path resolves to nothing.

Snippets in both layers are compiled by `tools/GuideVerifier` — add new guide files to the matching group
in `tools/GuideVerifier/projects.json`, and mark a genuine fragment with a `<!-- no-compile -->` line
directly above its fence. The marker sits there rather than in the fence's info string because
Kramdown — Jekyll's parser behind the Pages site — only accepts a single-token info string: it does not
read ```` ```csharp no-compile ```` as a fence at all, so the marker rendered as literal text on the
published page and the mis-paired fences swallowed the prose and headings after them into code blocks.

Write docs as if authored correctly from scratch — no correction notes or change history.

### Ship the shape, not the case that reported it

Work arrives concrete: a consumer report, one app's schema, a single failing endpoint. What ships is the
general shape behind it.

- **Name the mechanism, never the reporting domain.** An XML doc, a validator message or an exception that
  says *"featured attachment"* is wrong for every other shape it covers; *"an entity referencing one of its
  own children"* is right for all of them. Same for public API names.
- **Prose generic and short; examples concrete.** One worked example, not three — and pick one that drags in
  no unrelated subsystem.
- **One home per explanation.** Everything else links to it. A second copy drifts.
- **General rule in the general guide.** Put a short pointer in the specific place readers arrive from, not a
  second version of the rule.
- **Test fixtures are examples too** — reuse the guide's example names so the two read as one thing.

---

## Versioning & releases

Every package owns its own `<Version>` in its `.csproj` (SemVer). Published versions are **immutable on nuget.org** — a version can never be overwritten or reused.

- **Any change that ships** — source, the packed `ai/` guides, `build/` props/targets — must leave the changed package's `<Version>` **higher than its last published version**: patch for fixes and guide-only changes, minor for backward-compatible features, major for breaking changes. If the version was already bumped since the last publish, several edits may share that bump.
- **Members added to an existing public type are a patch**, not a minor: a new property on a model, a new
  overload beside an existing one. The minor is for a package gaining something a consumer has to go and
  adopt — a new type, a new registration, a new extension point. A member that only completes a shape a
  consumer already has (`QKeyword`'s `Trimmed*` family beside `Trimmed`) does not move the family's version
  line, and the whole family publishes on one aligned number.
- Do not bump packages you did not change. Dependent packages are re-versioned by the release tooling when it publishes to nuget.org.
- **Record every shipped change in [CHANGELOG.md](CHANGELOG.md) in the same change**: one bullet under the `## Unreleased` heading — `` `PackageId` x.y.z — one-line summary``. At publish time the Unreleased block becomes a dated release heading.
- **The number you write is provisional; the deploy phase settles the final one.** The rules above are what
  keep a changed package publishable at any moment — write them as stated. At release time the tooling
  re-versions dependents on top of that, and only then does the `## Unreleased` block become a dated
  heading, so do not date it yourself. The deploy itself runs from outside this repository.
- **The tag and the GitHub release come last, from this repo.** `.github/workflows/release.yml` tags a
  `main` commit and publishes the release page, with the notes taken from that version's `CHANGELOG.md`
  block. It publishes nothing to nuget.org — that already happened — so it refuses a version the registry
  does not have, an undated changelog heading, and a commit that is not on `main`.

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
- A concrete report ships as the general shape: mechanism-named APIs and messages, generic prose, concrete examples — see *Ship the shape, not the case that reported it*
- Every shipped change bumps the changed package's version and adds a `CHANGELOG.md` bullet — see *Versioning & releases*
- Never add consumer-scaffolding content to this file; it belongs in `ai/AGENTS.md`
- Keep `Program.cs` thin and use `IServiceCollection` extension methods
- Prefer abstractions over concrete types in cross-module dependencies
