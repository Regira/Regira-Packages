# Regira Source Repository — Agent Guide

For AI agents working **on** the Regira source (modules, guides, bugs, tests). Consumer projects use
`ai/AGENTS.md` instead — never add consumer-scaffolding content here.

---

## Keep things in sync (first priority)

Edits here reach further than this repo. Change one of these and update its counterparts in the same
turn; if a counterpart edit is out of scope, say so explicitly rather than leaving things inconsistent.

| You change… | Also update… |
|---|---|
| a public signature or behaviour | the module's `ai/*.md` and `README.md`. The MCP knowledge base is built from `ai/`, so a stale guide ships to every agent that asks |
| a package version | `CHANGELOG.md`; Regira-Website's `packages.json` is regenerated from this repo's `src/` by `npm run packages` over there |
| the path or filename of a doc page under `src/*/` | the links into it — regira.com's views and Regira-Blog post bodies point at this repo's Pages site with absolute URLs |

**Write docs in final state, not as a diff.** No change history, "previously…/now…", "fixed", "updated"
or migration notes — every document reads as if it had just been authored cleanly.

---

## Repository layout

.NET NuGet packages published to nuget.org, in two tiers: `Common.{Hub}/` projects hold a family's
abstractions (`Common.Entities`, `Common.Office`, `Common.Media`, `Common.Security`, ...), and ~60
`{Family}.{Provider}/` projects implement backends (`Entities.EFcore`, `PDF.Spire`, `Mail.SendGrid`, ...).
A hub ships its AI guides in `ai/` plus a `build/` `.props`/`.targets` pair that extracts them into
consumer projects on `dotnet build`.

```
src/
  Common.Setup/
    ai/                  # project.setup.md, shared.setup.md, CLAUDE.md, copilot-instructions.md (consumer-facing)
      commands/          # slash commands
    build/               # Regira.Setup.props, Regira.Setup.targets
  Common.Entities/       # hub — ai/ (entities.instructions.md, entities.signatures.md, ...) + build/
  Common.Office/         # hub for PDF, Excel, Word, Mail, ... — ai/ + build/
  PDF.Spire/             # provider — usually no ai/ (see Adding a module)
ai/
  AGENTS.md              # consumer bootstrap guide, served verbatim by the MCP server
  learnings.md           # contributor memory log
tools/GuideVerifier/     # compiles the csharp snippets in guides and docs
```

---

## Working on a module

Read the hub's `*.instructions.md` before touching a module's source; `*.signatures.md` and
`*.examples.md` carry exact API detail. Read `ai/learnings.md` before substantial work, and add a row
when a task reveals a durable lesson or a pitfall too small for a guide section. After a source change,
`/update-guide` proposes the guide patch.

### Adding a module

**Guides live on the hub, not on every package.** A hub's guide documents every provider behind it —
`office.pdf.instructions.md` covers all seven PDF backends — so most providers have no `ai/` folder.

**A new provider in an existing family** (`PDF.NewBackend`, `Mail.NewSender`):

1. Create `src/{Family}.{Provider}/` with a `.csproj` and source files
2. Document it **in the hub's guide** — the provider table in `{family}.instructions.md`, its
   registration call, and anything that behaves unlike its siblings. Do not start a second guide
3. Add it to the `Main packages and defaults` column of both routing tables (`ai/AGENTS.md`,
   `src/Common.Setup/ai/copilot-instructions.md`), saying when to pick it
4. Give it its own `ai/` only for what the hub guide cannot carry — the `using` set of a provider-only
   namespace (`Entities.EFcore`, `Entities.Web` ship just a `namespaces.md`) or a package card
   (`Security.Authentication*`). A provider `build/` folder is for unrelated MSBuild work, such as
   carrying a native companion file into the output (`PDF.SelectPdf`, `OCR.Tesseract`)

**A new hub** (a new family, or a standalone package like `TreeList`):

1. Create `src/{ModuleName}/` with a `.csproj`, source files, `ai/` and `build/`
2. Write at least `{module}.instructions.md` and `{module}.examples.md` in `ai/`
3. Add `build/Regira.{ModuleName}.props` and `.targets`, copying an existing pair (the props sets
   `DefaultItemExcludes` so `.regira\**` and `.claude\**` don't appear as project items)
4. Pack the props/targets under `buildTransitive\` and the AI files under `ai\` in the `.csproj`
5. Add the module to both routing tables
6. Add a snippet group to `tools/GuideVerifier/projects.json` listing the guide files and the src
   projects they compile against

### Documentation

Two layers that never refer to each other: `ai/` for AI agents, `README.md` + `docs/` for developers.

- **The README is an index, not the manual** — projects table, installation, a short "which one do I
  want", the `## Overview` link list and the licence; each subject gets its own `docs/` page
  (`Common.Entities` and `Common.Security` are the shape to copy). The README is also the nuget.org
  package page (`PackageReadmeFile`), so length there costs every listing.
- **Every project folder has a `README.md`** — Pages serves a folder only when it holds one, so a link to
  a README-less folder 404s.
- **Links:** the README's `## Overview` uses absolute `https://regira.github.io/Regira-Packages/…` URLs;
  a `docs/` page repeats the list with relative links (`../README.md`, `jwt.md`) and bolds itself. A
  cross-module link from any README is absolute — nuget.org resolves relative paths to nothing.
- **Snippets in both layers are compiled** by `tools/GuideVerifier`; register new guide files in
  `projects.json`. Mark a genuine fragment with a `<!-- no-compile -->` line directly above its fence,
  never in the info string: Kramdown (the Pages parser) only accepts a single-token info string, so
  ```` ```csharp no-compile ```` stops being a fence and swallows the prose after it.
- **Liquid:** the Pages site is a legacy Jekyll build of `main` that runs Liquid over every `.md` file
  before Kramdown, even inside backticks or a fence. A Liquid opener — two opening curly braces, or an
  opening curly brace followed by `%` — makes a template placeholder vanish, and an unknown tag fails the
  whole site build. A page that shows either wraps everything below its H1 in a `raw` block hidden in
  HTML comments — copy the second and the last line of `src/Common.Web/README.md`; this file cannot spell
  them out for the same reason. The H1 stays the file's first line (no BOM): Pages takes the title from it.

### Ship the shape, not the case that reported it

Work arrives concrete — a consumer report, one app's schema, a failing endpoint. What ships is the
general shape behind it.

- **Name the mechanism, never the reporting domain.** An XML doc, validator message, exception or public
  API name that says *"featured attachment"* is wrong for every other shape it covers; *"an entity
  referencing one of its own children"* is right for all of them.
- **Prose generic and short; examples concrete.** One worked example, not three — one that drags in no
  unrelated subsystem.
- **One home per explanation.** Everything else links to it; a second copy drifts. The general rule goes
  in the general guide, with a short pointer where readers arrive from.
- **Test fixtures are examples too** — reuse the guide's example names so the two read as one thing.

---

## Versioning & releases

Every package owns its `<Version>` in its `.csproj` (SemVer). Published versions are **immutable on
nuget.org** — never overwritten or reused.

- **Any change that ships** — source, packed `ai/` guides, `build/` props/targets — leaves the changed
  package's `<Version>` **above its last published version**: patch for fixes and guide-only changes,
  minor for backward-compatible features, major for breaking changes. Edits since the last publish may
  share one bump.
- **Members added to an existing public type are a patch** — a model property, an overload beside an
  existing one. Minor is for something a consumer has to go and adopt: a new type, registration or
  extension point. A member that completes an existing shape (`QKeyword`'s `Trimmed*` family beside
  `Trimmed`) doesn't move the family's version line; the family publishes on one aligned number.
- **Bump only packages you changed.** The number you write is provisional — the release tooling
  re-versions dependents at publish (run from outside this repo) — but follow the rules above so a
  changed package is publishable at any moment.
- **Record every shipped change in [CHANGELOG.md](CHANGELOG.md)** in the same change: one bullet under
  `## Unreleased` — `` `PackageId` x.y.z — one-line summary``. Don't date the heading; publishing does.
- **The tag and GitHub release come last, from this repo.** `.github/workflows/release.yml` tags a `main`
  commit and publishes the release page with notes from that version's `CHANGELOG.md` block. It publishes
  nothing to nuget.org, so it refuses a version the registry lacks, an undated changelog heading, and a
  commit not on `main`.

---

## Git

Commit or push only when the user explicitly asks. Leave finished work in the working tree and report
what is ready — a review verdict ("ready to commit") or a checklist step is not that ask.

---

## Slash commands

Source in `src/Common.Setup/ai/commands/`. All but `/update-guide` are packed in `Regira.Setup` and
extracted to a consumer's `.claude/commands/` on build; `/update-guide` is source-repo only (consumer
guide copies are overwritten on each extraction).

| Command | Purpose |
|---|---|
| `/new-entity` | Scaffold a full Regira entity in a consumer project |
| `/new-project` | Bootstrap a new consumer project |
| `/sync-guides` | Refresh stale extracted guides in a consumer project |
| `/update-guide` | Propose a guide patch after a source code change |
| `/evaluate` | Run a structured quality evaluation on a module |

---

## Code conventions

- Keep `Program.cs` thin and use `IServiceCollection` extension methods
- Prefer abstractions over concrete types in cross-module dependencies
