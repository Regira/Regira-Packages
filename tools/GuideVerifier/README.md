# Regira Guide Verifier

Compiles the fenced ` ```csharp ` code blocks in the Entities-family AI guides so a snippet that no
longer binds against the real API is caught here rather than by a consumer.

## What it does

1. Reads `projects.json` — the `projects` it ProjectReferences and the `guideDirs` (ai/ folders) it scans.
2. Pulls every ` ```csharp ` block from each `*.md` in those folders (skipping `no-compile` blocks).
3. Classifies each block with Roslyn:
   - **Declaration** blocks (types / namespaces / usings) are emitted at namespace scope, each in its own
     namespace to avoid cross-guide type collisions.
   - **Statement / expression** blocks are wrapped in an `async` method body (with `sp` / `scope`
     service-provider locals, matching the guides' idiom).
4. Writes a throwaway project to a temp dir (outside the repo, so it inherits no `Directory.Build.props`)
   that references the manifest's src projects, runs `dotnet build`, and reports each failure as
   `file.md § <heading>` with the compiler error. Exits non-zero on any failure.

## Run it

```bash
dotnet run --project tools/GuideVerifier
# or point it at a repo root explicitly:
dotnet run --project tools/GuideVerifier -- /path/to/Regira-Packages
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
- **Test-CI only.** This project references the src Entities projects; it must never be pulled into the
  MCP deploy path or the knowledge-base builder (both in the private Regira-Tools repo), which
  stay dependency-free of it.

## Extending coverage

Add a src project (and, optionally, its ai/ folder) to `projects.json`. Start small — each new guide
folder usually needs a triage pass to mark its partial snippets `no-compile` before the run is green.
