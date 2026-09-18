# Regira Guide Verifier

Compiles the fenced ` ```csharp ` code blocks in the AI guides, package READMEs and Office topic docs
so a snippet that no longer binds against the real API is caught here rather than by a consumer.

## What it does

1. Reads `projects.json` — a list of **groups**. Each group pairs guide sources (`guideDirs` scanned
   for `*.md`, and/or individual `guideFiles`) with the src `projects` its snippets compile against,
   plus optional `usings` (prepended to every snippet in the group) and `frameworkReferences`
   (e.g. `Microsoft.AspNetCore.App`). Groups build independently, so doc families with unrelated —
   or conflicting — dependency sets stay isolated. Two more optional keys:
   - `packages` — NuGet packages as `{ "id": "version" }`, for what a *consumer* installs alongside the
     Regira projects (an EF Core provider, say). Project references alone cannot cover those.
   - ⚠️ Keep `usings` to what the reader's SDK supplies **implicitly**. A namespace listed here is
     prepended to every snippet in the group, so listing one the doc also declares makes the doc's own
     `using` line dead weight — and a guide that later loses it still compiles green. The `quickstart`
     group carries only the four Web SDK implicit usings its snippets rely on, for exactly that reason.
   - `sharedTypes` — `true` for a **narrative** guide whose blocks build on each other (a quickstart:
     §2 declares the entities, §3 registers them, §4 writes their controllers). The group's snippets then
     compile into one namespace instead of one each. Leave it off for reference guides, where the
     per-snippet isolation is what keeps two files' `Product` apart.
2. Pulls every ` ```csharp ` block from each guide file, skipping any preceded by a `<!-- no-compile -->` line.
3. Classifies each block with Roslyn:
   - **Declaration** blocks (types / namespaces / usings) are emitted at namespace scope, each in its own
     namespace to avoid cross-guide type collisions.
   - **Statement / expression** blocks are wrapped in an `async` method body. `sp` / `scope` (service
     providers, matching the guides' idiom) and `args` (what a top-level `Program.cs` receives) are
     ambient **fields**, so a snippet may declare its own `scope` — `using (var scope = …)` — without
     colliding. A statement block's own leading `using` **directives** are hoisted to file scope; a
     `using var x = …` declaration is a statement and stays put.
4. Per group: writes a throwaway project to a temp dir (outside the repo, so it inherits no
   `Directory.Build.props`) that references the group's src projects, runs `dotnet build`, and reports
   each failure as `file.md § <heading>` with the compiler error. Exits non-zero when any group fails.

## Run it

```bash
dotnet run --project tools/GuideVerifier
# or point it at a repo root explicitly:
dotnet run --project tools/GuideVerifier -- /path/to/Regira-Packages
# or run a subset of groups (comma-separated):
dotnet run --project tools/GuideVerifier -- . --group entities
dotnet run --project tools/GuideVerifier -- . --group office-pdf,office-excel
```

## The `no-compile` convention

Many guide snippets are **deliberate fragments** — an entity class that references types defined in a
neighbouring block, a signature reference, a method body shown without its class, a line with a `// …`
placeholder. These cannot compile standalone and are opted out of verification by a marker line placed
above the fence:

~~~markdown
<!-- no-compile -->
```csharp
// partial fragment — references types defined elsewhere in the guide
public class Product : IEntityWithSerial { /* … */ }
```
~~~

A `<!-- no-compile -->` line **directly above** the fence excludes the block. The rule of thumb: a block a
reader could paste into a project and expect to build stays a plain ` ```csharp ` block (and is therefore
verified); anything illustrative-only gets the marker.

> **The marker goes above the fence, never inside its info string.** ` ```csharp no-compile ` was the old
> form, and it broke the published docs. Kramdown — Jekyll's parser behind the GitHub Pages site — only
> accepts a **single-token** info string, so it does not read that line as a fence at all: the marker
> rendered as literal text on the page, and every unrecognised opener turned the following closing fence
> into an opener, swallowing the prose and headings after it into a code block. `entities.blueprints` lost
> five of its eight `##` sections that way. The guides read correctly over MCP throughout, which is why it
> went unnoticed — only the human-facing site was affected. Keep the marker on its own line and the fence a
> bare ` ```csharp `.

The old form now **fails loudly** rather than silently: the extractor no longer reads the info string, so
` ```csharp no-compile ` is collected as an ordinary C# block and the fragment breaks the build.

As the guides are cleaned up so that more blocks are self-contained, remove `no-compile` markers to bring
those snippets back under verification.

**Blind spot — blockquoted snippets.** A fence indented inside a blockquote (`> ```csharp `) is invisible to
the extractor, marker or not, so those blocks are never verified. Four exist today, in
`entities.instructions.md` and `entities.patterns.md`; they carry `> <!-- no-compile -->` for consistency,
but the marker is inert. Don't rely on a blockquoted block being checked.

## Scope and CI

- Intended to run in CI as a **separate, non-blocking job** (`continue-on-error`) once workflows are
  authored, while the guides still carry many `no-compile` fragments. Make it blocking once most snippets compile.
- **Test-CI only.** This project references src projects; it must never be pulled into the
  MCP deploy path or the knowledge-base builder (both outside this repository), which
  stay dependency-free of it.

## Extending coverage

Add a group (or extend an existing one) in `projects.json`: guide sources plus the src projects those
snippets need, and any group-wide `usings`. Keep a group's dependency set coherent — when two doc
families need conflicting implementation packages (as the Office backends do), give each its own group.
Start small — each new guide file usually needs a triage pass to mark its partial snippets `no-compile`
before the run is green.
