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
2. Pulls every ` ```csharp ` block from each guide file (skipping `no-compile` blocks).
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
placeholder. These cannot compile standalone and must be opted out of verification by tagging the fence:

~~~markdown
```csharp no-compile
// partial fragment — references types defined elsewhere in the guide
public class Product : IEntityWithSerial { /* … */ }
```
~~~

The token `no-compile` anywhere in the fence info string (after `csharp`) excludes the block. The rule of
thumb: a block that a reader could paste into a project and expect to build should stay a plain
` ```csharp ` block (and therefore be verified); anything that is illustrative-only gets `no-compile`.

As the guides are cleaned up so that more blocks are self-contained, remove `no-compile` tags to bring
those snippets back under verification.

## Scope and CI

- Intended to run in CI as a **separate, non-blocking job** (`continue-on-error`) once workflows are
  authored, while the guides still carry many `no-compile` fragments. Make it blocking once most snippets compile.
- **Test-CI only.** This project references src projects; it must never be pulled into the
  MCP deploy path or the knowledge-base builder (both in the private Regira-Tools repo), which
  stay dependency-free of it.

## Extending coverage

Add a group (or extend an existing one) in `projects.json`: guide sources plus the src projects those
snippets need, and any group-wide `usings`. Keep a group's dependency set coherent — when two doc
families need conflicting implementation packages (as the Office backends do), give each its own group.
Start small — each new guide file usually needs a triage pass to mark its partial snippets `no-compile`
before the run is green.
