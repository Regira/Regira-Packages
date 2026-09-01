# Regira Consumer Bootstrap

Use this file as the authoritative downstream bootstrap for choosing the project template, selecting Regira packages, and generating code inside a consumer repository.

In a consumer repository, use this file plus any local `.regira/instructions/*.md` guides as the available sources of truth.

> **Building a browser front-end (Vue 3 SPA)?** That is a separate **npm / TypeScript** family
> published from `Regira-Modules`. Load the **front-end consumer bootstrap** —
> `get_bootstrap_guide(platform: "frontend")` — **before** choosing a UI framework, package manager or
> project structure. Do not route SPA work to the .NET packages below.

## Golden path — .NET API + Vue SPA

The shortest route to a running full-stack app. Each phase has a checkpoint; don't start the next one
until it passes. Everything below this table is reference — follow a link only when a phase needs more
than its one-liner.

| # | Phase | Do | Checkpoint |
|---|-------|----|------------|
| 0 | Classify | List top-level entities, their owned collections, and the auth mode. Owned rows are configured through the parent's `Related(...)` — they are not entities, cost no registration slot, and get no controller. | Free tier fits: 5 simple + 2 complex registrations |
| 1 | Probe | `dotnet --version`, `node -v`, `npm -v`, `git --version` | All four resolve |
| 2 | API host | `project.setup` (a section of `Regira.Setup` — `get_package(id: "Regira.Setup", section: "project.setup")`) → host, `DbContext`, `UseEntities<TContext>(e => e.UseDefaults())`, `ConfigureDefaultJsonOptions()`. **Settle the route prefix now** (`opts.UseCentralRoutePrefix(new RouteAttribute("api"))` from `Regira.Web.Routing`, or none): the SPA's config, its axios base and the dev proxy all have to agree with it, and changing it after phase 4 re-verifies every URL | `dotnet build` clean; `/openapi/v1.json` served |
| 3 | Register | One `.For<>()` per top-level entity; one `e.Related(...)` per owned collection | No startup **warnings** — an ignored `?q=`, a dual write path or an out-of-scope global filter is a defect, not noise (informational lines are expected). One exception on `net10.0`: EF's `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning`, one per required dependent of an `IArchivable` principal, is the intended behaviour of soft delete — see `entities.instructions` Step 11 |
| 4 | Prove the API | create → update → **update again** → re-read | The second save is idempotent and owned rows survive it |
| 5 | SPA shell | `get_bootstrap_guide(platform: "frontend")`, then the shell generator (`--no-auth` when there is no auth) | `npm run dev` serves the shell; `index.html` has the `#modals` host; a no-auth app reaches `AppStatus.Ready` |
| 6 | Slices | One generator run per entity (`--rel`, `--owns` as needed); register each in `src/entities/index.ts` | Every list, details view and selector renders against the live API |
| 7 | Prove it end to end | List, details, create, two consecutive updates, reopen, marked deletion, narrow viewport | Each passes in the browser, not just in tests |

**Generate before you write.** The shell and every entity slice come from the generators; hand-authoring
them is the exception, and reviewing a generated file is far cheaper than reconstructing one.

## MCP server

A Regira MCP server is available at `https://mcp.regira.com/mcp`. It provides full knowledge of all Regira packages — including packages that are not yet installed locally.

**Use it for:**
- Discovering which packages to install before writing any code (`recommend_packages`, `search_packages`, `list_packages`)
- Reading full setup guides and code examples without running `dotnet build` first
- Any AI tool that supports MCP (Claude Desktop, VS Code, Cursor, Copilot)

**Configure once** in your AI tool, then use these tools roughly in this order:

Canonical parameter names are **not** uniform across these tools — the search term is `query` on one,
`pattern` on another and `task` on a third, and a package is `id` everywhere except `search_docs`, where
`package` scopes the search. Copy the call form from this table rather than inferring it from a neighbouring
tool; where a package is taken, `id`, `pkg` and `package` all resolve, so a wrong guess costs nothing.

| Tool | Call | Purpose |
|---|---|---|
| `get_bootstrap_guide` | `get_bootstrap_guide(heading: "toc")`, then `heading: "<one heading>"`; `platform: "frontend"` for the Vue guide | Start here. `heading` also takes a comma-separated list. |
| `recommend_packages` | `recommend_packages(feature: "shopping list API with QR codes")` | First-pass package suggestions from a feature description. `platform` is optional. |
| `search_packages` | `search_packages(query: "PDF generation")` | Refine package discovery when you already have keywords or a concrete use case. |
| `list_packages` | `list_packages(category: "entities")` | Browse the full package catalog when the fit is still broad or category-driven. |
| `get_package_card` | `get_package_card(id: "Regira.Entities")` | **Orient here before loading sections** — the 10-bullet must-know card per package; it often suffices for a task and tells you which section to drill into. |
| `get_package_toc` | `get_package_toc(id: "Regira.Entities")` | After picking a package, list the available documentation sections such as `instructions`, `examples`, `setup`, or `signatures`. |
| `get_section_toc` | `get_section_toc(id: "Regira.Entities", section: "entities.examples")` | After picking a section, list its headings before loading content. |
| `get_package` | `get_package(id: "Regira.Entities", section: "entities.examples", heading: "Order + OrderLine entities")` | Read the actual package guidance, optionally narrowed to one section or heading. `heading` takes a comma-separated list; supports `maxChars` and `page`. |
| `search_docs` | `search_docs(query: "soft delete", package: "Regira.Entities")` | **When you don't know where a topic lives** — searches every package's documentation at once and returns the matching headings with a snippet and the exact `get_package(...)` call to read each. Reach for this before guessing a section, and to confirm that something genuinely isn't documented. An unscoped search keeps at most 2 hits per package; raise `hitsPerPackage` (or pass `package`) when the answer is a third mention inside one guide. |
| `get_example` | `get_example(id: "Regira.Entities", pattern: "many-to-many join")` | Pull only the examples that match a topic once you know what you want to see. Use `section` to scope to `examples` or `instructions`. |
| `how_to` | `how_to(task: "seed data")`, or no argument to list recipes | Answer "how do I do X in code?" for common Regira Entities tasks — the registered service, a minimal snippet, and a doc pointer. |
| `list_types` | `list_types(id: "Regira.Entities", nameFilter: "EntityService")` | Optional branch: inspect the public API surface from the source map without loading docs. |
| `get_type` | `get_type(id: "Regira.Entities", typeName: "IEntityService")` | Optional branch: inspect one type and its member signatures in detail. |

**Configuration snippets:**

Claude Desktop (`claude_desktop_config.json` in the Claude app-data folder — Windows: `%APPDATA%\Claude`, macOS: `~/Library/Application Support/Claude`):
```json
{ "mcpServers": { "regira": { "url": "https://mcp.regira.com/mcp" } } }
```

VS Code / Claude Code (`.mcp.json` at your repo root):
```json
{ "mcpServers": { "regira": { "url": "https://mcp.regira.com/mcp" } } }
```

Cursor: Settings → MCP Servers → add `https://mcp.regira.com/mcp`.

The local file-extraction system (`dotnet build` → `.regira/instructions/`) remains available as an offline fallback and for IDE-local use. Prefer MCP-based discovery for the initial "what should I install?" phase.

## Pre-flight checklist

Run this checklist before any code generation:

- [ ] **If using licensed packages (e.g. `Regira.Entities.DependencyInjection`, `Regira.Office.Clients`):** on the **free tier, no `UseRegira()` call is needed** — free limits apply automatically. With license keys, store them under `Regira:LicenseKeys` and call `services.UseRegira(configuration)` **before** any module setup calls (e.g. `UseEntities()`). A single key can cover multiple products; add more keys to the array to combine them — the system picks the best per product (paid always wins over free).
- [ ] **If MCP is configured:** used `get_package_toc` to discover section keys, `get_section_toc` to list headings, and `get_package` (with `section=` and optional `heading=`) to read the relevant guides for each installed Regira module — no build step required
- [ ] **If MCP is not configured:** `dotnet restore` and `dotnet build` succeeded so installed Regira packages could extract their embedded `ai/*.md` files into `.regira/instructions/`, and that folder was checked for `*.instructions.md` files and relevant setup references at the solution root (or, for most packages, the project root when building standalone — see *Default workflow* below)
- [ ] Primary guides relevant to the selected modules (`project.setup.md`, `shared.setup.md`, matching `*.instructions.md`) had their core sections read before writing application code in that area, and deep references were consulted on demand — following the **minimum viable read** in *Guide loading rules* below

Only proceed to project scaffolding, infrastructure changes, or domain code once all applicable checks are satisfied.

## Default workflow

Ask the user what they're building if it isn't already clear, then follow the **Code generation workflow** below. For an existing project, inspect current `*.csproj` files before choosing packages or scaffolding. Prefer project-local instructions over shared Regira guidance when both exist, and ask for feedback rather than guessing missing APIs or conventions.

Read module guides before writing code: if MCP is configured, call `get_package` for each installed Regira module; otherwise check `.regira/instructions/` for extracted guides. Regira packages that carry AI files embed them inside the NuGet package under `ai/`. During `dotnet build`, their bundled `.props` and `.targets` files copy those files into `.regira/instructions/` at the solution root (`$(SolutionDir)`); most fall back to the project root when building standalone, but `Regira.Entities`, `Regira.Setup`, and `Regira.Office` extract only when building in a solution context. Use `Regira.Setup` when the consumer needs local copies of `project.setup.md` and `shared.setup.md` through the package-extraction workflow.

## Guide loading rules

Use the narrowest relevant guidance instead of loading broad instruction sets up front.

Primary guides (`project.setup.md`, `shared.setup.md`, matching `*.instructions.md`) follow a **minimum viable read**: the package card first (often sufficient to orient), then the guide's core sections — decision material, workflow steps, and patterns — before generating code in that area. Troubleshooting tables and quick-reference sections exist for lookup: fetch a row when you have the symptom, not as pre-reading. Deep references (`*.setup.md`, `*.examples.md`, `*.signatures.md`, `*.namespaces.md`) should be consulted surgically by section/heading when the current task needs them; prefer `get_type` / installed `.d.ts` files over doc sections for exact signatures.

1. Never load the whole `ai/` folder when a narrower guide exists.
2. For project scaffolding or app-shape changes, load the project setup guide — `get_package(id: "Regira.Setup", section: "project.setup")` (or `.regira/instructions/project.setup.md` locally).
3. For shared setup concerns reused across modules, load `shared.setup.md`.
4. For module-specific work, load the matching `*.instructions.md` guide before writing code.
5. Load deep references such as `*.setup.md`, `*.examples.md`, `*.signatures.md`, and `*.namespaces.md` only when the current task needs them.
6. When details are missing, do not guess syntax or signatures. Stop, describe the gap, and ask the user for confirmation.

## Project template selection

Pick a `projectTemplate` as the starting point before selecting modules when the user is creating a new project or requesting major scaffolding changes. The table maps common shapes to their nearest template; mix elements from several templates, or deviate, when the app's requirements call for it.

| Requirement | `projectTemplate` |
|-------------|-------------------|
| Script, batch job, or CLI utility | `ConsoleWithLogging` |
| Standard hosted API, Minimal API and Controllers, no auth | `BasicApi` |
| Standard hosted API (IIS / Azure / Docker) **with** auth — incl. an authenticated Entities API | `BasicApi` + the auth registrations from `SelfHostingApiWithAuth` |
| Lightweight self-hosted internal API, no auth | `SelfHostingApi` |
| Must be deployable as a Windows Service | `SelfHostingApi` |
| Self-hosted API protected by API key and/or JWT Bearer | `SelfHostingApiWithAuth` |
| Self-hosted, controller-based routing with enforced authorization | `SelfHostingApiWithAuth` |
| Minimal API endpoints with authentication | `SelfHostingApiWithAuth` |
| Users sign in with a work Microsoft account (Entra ID), or any OpenID Connect provider | `SelfHostingApiWithAuth` + `AddEntraIdSignIn` / `AddOidcAuthentication` |
| API called with tokens Entra ID (or Auth0 / Keycloak / Okta) issued | `SelfHostingApiWithAuth` + `AddEntraIdBearer` / `AddBearerAuthentication` |
| Browser session rather than a bearer token (server-rendered, Blazor Server, same-site SPA) | `SelfHostingApiWithAuth` + `AddCookieAuthentication` |

`SelfHostingApiWithAuth` is the usual starting point for an authenticated app — the scheme is a registration choice
on top of it, so every scheme fits the same scaffold. Read `Regira.Security` → `security.instructions` →
*Choosing a scheme* before wiring one.
Role-gated features (admin screens, approval workflows) follow *Roles end-to-end: Identity → JWT → SPA* in the same
guide (`how_to` key `roles-end-to-end`) — the default Identity wiring emits **no** role claims.

For a new project, start from the nearest template before creating files. For an existing project, infer the nearest
matching template from the current app structure and stay consistent with it. Note a deliberate deviation briefly so
a reviewer can follow the choice.

## You are the .NET expert

These guides cover **Regira-specific** behavior only. General .NET / EF Core / C# correctness is assumed
knowledge and is not re-explained here — own it before the first build, including:

- **C# name resolution** (e.g. a type name colliding with a namespace segment).
- **NuGet version selection** — confirm a version exists; never guess a patch number. Pin from a module's known-good list (e.g. `Regira.Entities` → `entities.setup`) rather than inventing one.
- **Package vulnerability hygiene** — `dotnet list package --vulnerable --include-transitive`.
- **EF Core provider major ↔ TFM alignment** — a mismatch builds clean but fails on the first query.

## Setup baseline

Keep setup aligned with the selected `projectTemplate`. This file must remain enough for the normal one-file consumer flow even when no extracted local guides exist yet.

- Use the latest stable .NET framework and latest C# features unless the consumer project already targets something else. The LTS version is **.NET 10**.
- When adding a NuGet package, install the latest stable version rather than pinning an older one — outdated versions may carry known vulnerabilities (e.g. avoid `Microsoft.EntityFrameworkCore.Sqlite` 10.0.0; use the latest patched release). After restoring, run `dotnet list package --vulnerable --include-transitive` and upgrade any flagged package to a patched version.
- Keep `Program.cs` thin and move service registration or middleware setup into extension methods.
- Prefer `Microsoft.Extensions.DependencyInjection` and depend on abstractions instead of concrete implementations.
- Use file-scoped namespaces.
- For standard Web APIs, OpenAPI plus Scalar is the default API surface: use `app.MapOpenApi()` plus `app.MapScalarApiReference()`, and do not add `Swashbuckle.AspNetCore` or call `UseSwaggerUI()` on the standard Regira API path. Add Swagger only when the user explicitly requests it.
- Ask for feedback instead of guessing missing APIs, namespaces, signatures, or project-specific conventions.

Template consequences:

- `ConsoleWithLogging`: use a host-based console or CLI setup with configuration and structured logging.
- `BasicApi`: use an ASP.NET Core Web API baseline without authentication unless the user explicitly asks for auth.
- `SelfHostingApi`: use a self-hosted internal API baseline and keep it compatible with Windows Service deployment when requested.
- `SelfHostingApiWithAuth`: use self-hosted API scaffolding with API key and/or JWT Bearer authentication, and keep application endpoints protected by default.

## Code generation workflow

1. Choose or confirm the `projectTemplate`.
2. Choose the smallest Regira module set that covers the user's request.
3. Add the matching packages.
4. Inspect existing `PackageReference` items when the installed Regira package set is part of the decision.
5. Read the guidance for each installed Regira module — cheapest first: if MCP is configured, orient with `get_package_card` (often enough on its own), then `get_package_toc` to list section keys, `get_section_toc` to inspect headings, and `get_package` with `section=` / `heading=` to fetch targeted content without loading unnecessary context. Use `list_types` / `get_type` (or local sources/`.d.ts` when installed) to check API surface instead of loading doc-heavy sections. Otherwise run `dotnet restore` and `dotnet build` to extract embedded `ai/*.md` files into `.regira/instructions/`.
6. Before generating entity models, services, controllers, DI registrations, or infrastructure code, read the applicable primary guides (`*.instructions.md`, `project.setup.md`, `shared.setup.md`) following the **minimum viable read** in *Guide loading rules* — either via MCP or from `.regira/instructions/`. Skipping the relevant primary guides is a workflow violation.
7. If guides are unavailable both via MCP and locally, verify the restore/build succeeded, then continue with the setup baseline, package routing table, and general engineering rules in this file.
8. Generate code that stays consistent with the selected `projectTemplate`, installed Regira packages, any extracted local guides, and local project conventions.

## Regira package routing

When the consumer project already contains Regira packages, inspect the project's `PackageReference` items and map them back to the nearest module family before generating code. Use the `Guidance` column to decide whether to load a dedicated guide first or rely on this file plus general project conventions. When a dedicated guide names a default or recommendation, that package is labeled directly in the table.

| Installed package pattern | Module or family | Guidance | Use when | Main packages and defaults |
|---------------------------|------------------|----------|----------|----------------------------|
| `Regira.Entities*` | `Entities` | Dedicated module guides | CRUD APIs, entity services, DTO mapping, EF Core repositories, and generated endpoints | `Regira.Entities`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Mapping.Mapster` (default mapping), `Regira.Entities.Mapping.AutoMapper`, `Regira.Entities.EFcore`, `Regira.Entities.Web` |
| `Regira.IO.Storage*` | `IO.Storage` | Dedicated module guides | File storage, uploads, Azure Blob, SFTP, ZIP, or SimpleTCP file transfer | `Regira.IO.Storage`, `Regira.IO.Storage.Azure`, `Regira.IO.Storage.SSH`, `Regira.IO.Storage.GitHub`, `Regira.IO.Storage.SimpleTCP` |
| `Regira.IO.Compression.SharpZipLib` | `IO.Compression` | No dedicated family guide | ZIP archive creation and extraction, especially password-protected ZIP files | `Regira.IO.Compression.SharpZipLib` |
| `Regira.Office` | `Office` | Dedicated family overview | Office family overview or when the user still needs to choose between PDF, Excel, Word, Mail, OCR, and related submodules | `Regira.Office` |
| `Regira.Office.Clients` | `Office.Clients` | No dedicated family guide | HTTP client extensions for consuming Regira Office services remotely | `Regira.Office.Clients` |
| `Regira.Office.PDF*` | `Office.PDF` | Dedicated module guides | HTML to PDF, PDF operations, printing | `Regira.Office.PDF.SelectPdf` (preferred for HTML to PDF), `Regira.Office.PDF.DocNET` (preferred for PDF operations), `Regira.Office.PDF.Spire` (preferred when print and PDF ops are both needed); also `Regira.Office.PDF.Puppeteer`, `Regira.Office.PDF.MsPlaywright`, `Regira.Office.PDF.PDFtoPrinter`, `Regira.Office.PDF.PockyBum522` |
| `Regira.Office.Excel*` | `Office.Excel` | Dedicated module guides | Excel read and write | `Regira.Office.Excel.MiniExcel` (preferred), `Regira.Office.Excel.ClosedXML`, `Regira.Office.Excel.EPPlus`, `Regira.Office.Excel.NpoiMapper` |
| `Regira.Office.Word*` | `Office.Word` | Dedicated module guides | Word document generation | `Regira.Office.Word.Spire` (preferred), `Regira.Office.Word.Mini` |
| `Regira.Office.Mail.*` | `Office.Mail` | Dedicated module guides | Email sending, mail DTOs for HTTP endpoints, or reading `.msg` and `.eml` files | `Regira.Office.Mail.SendGrid`, `Regira.Office.Mail.MailGun`, `Regira.Office.Mail.Web`, `Regira.Office.Mail.MSGReader` |
| `Regira.Office.Csv*` | `Office.CSV` | Dedicated module guides | CSV read and write | `Regira.Office.Csv.CsvHelper` |
| `Regira.Office.Barcodes*` | `Office.Barcodes` | Dedicated module guides | Barcode or QR code generation | `Regira.Office.Barcodes.ZXing` (preferred), `Regira.Office.Barcodes.Spire`, `Regira.Office.Barcodes.QRCoder`, `Regira.Office.Barcodes.UziGranot` |
| `Regira.Office.OCR*` | `Office.OCR` | Dedicated module guides | OCR text extraction | `Regira.Office.OCR.Tesseract`, `Regira.Office.OCR.PaddleOCR` |
| `Regira.Office.VCards*` | `Office.VCards` | Dedicated module guides | vCard contact files | `Regira.Office.VCards.FolkerKinzel` |
| `Regira.Media*`, `Regira.Drawing.*` | `Media` | Dedicated module guides | Image processing, resize, crop, rotate, FFmpeg workflows | `Regira.Media`, `Regira.Drawing.SkiaSharp` (preferred image backend), `Regira.Drawing.GDI`, `Regira.Media.FFMpeg` |
| `Regira.Printing.GDI` | `Printing` | No dedicated family guide | GDI-based document printing utilities on Windows | `Regira.Printing.GDI` |
| `Regira.Security*` | `Security` | Dedicated module guides | Hashing, cryptography, and every authentication scheme — self-issued JWT (+ refresh tokens), API keys, cookie sessions, Microsoft Entra ID, OpenID Connect sign-in, multi-scheme selection | `Regira.Security`, `Regira.Security.Hashing.BCryptNet` (preferred for passwords), `Regira.Security.Authentication`, `Regira.Security.Authentication.Web` |
| `Regira.Web*` | `Web` | Dedicated module guides | Razor rendering, middleware, visit/page-view analytics, and optional Swagger/OpenAPI auth helpers | `Regira.Web`, `Regira.Web.Analytics`, `Regira.Web.Analytics.GeoIP2`, `Regira.Web.HTML.RazorEngineCore`, `Regira.Web.HTML.RazorLight`, `Regira.Web.Swagger` |
| `Regira.System*` | `System` | Dedicated module guides | Windows Service hosting, background tasks, and `.csproj` project tooling | `Regira.System`, `Regira.System.Hosting`, `Regira.System.Projects` |
| `Regira.Invoicing*` | `Invoicing` | Dedicated module guides | Invoice models, UBL, Peppol, accounting integration, and AP gateway transmission | `Regira.Invoicing`, `Regira.Invoicing.Billit`, `Regira.Invoicing.UblSharp`, `Regira.Invoicing.ViaAdValvas` |
| `Regira.Payments*` | `Payments` | Dedicated module guides | Payment providers, payment links, webhooks | `Regira.Payments`, `Regira.Payments.Mollie`, `Regira.Payments.Pom` |
| `Regira.TreeList` | `TreeList` | Dedicated module guides | Hierarchical tree structures | `Regira.TreeList` |
| `Regira.Common` | `Common` | No dedicated family guide | Shared abstractions, utilities, normalizing helpers, base contracts | `Regira.Common` |
| `Regira.Caching.Runtime` | `Caching` | No dedicated family guide | Runtime caching on top of the common abstractions | `Regira.Caching.Runtime` |
| `Regira.DAL.EFcore` | `DAL.EFcore` | No dedicated family guide | EF Core extensions and repository utilities | `Regira.DAL.EFcore` |
| `Regira.DAL.MongoDB` | `DAL.MongoDB` | No dedicated family guide | MongoDB connectivity and backup or restore workflows | `Regira.DAL.MongoDB` |
| `Regira.DAL.MySQL*` | `DAL.MySQL` | No dedicated family guide | MySQL or MariaDB connectivity and backup workflows | `Regira.DAL.MySQL`, `Regira.DAL.MySQL.MySqlBackup` |
| `Regira.DAL.PostgreSQL` | `DAL.PostgreSQL` | No dedicated family guide | PostgreSQL connectivity | `Regira.DAL.PostgreSQL` |
| `Regira.Globalization.LibPhoneNumber` | `Globalization` | No dedicated family guide | Phone number parsing and formatting | `Regira.Globalization.LibPhoneNumber` |
| `Regira.Licensing` | `Licensing` | No dedicated family guide | License key registration and offline validation — `UseRegira(configuration)` or `UseRegira(licenseKey)` before any module setup calls (see the pre-flight checklist) | `Regira.Licensing` |
| `Regira.Setup` | `Setup` | Shared setup guides | Shared project-template and setup-guide extraction for local AI guidance | `Regira.Setup` |
| `Regira.Serializing.Newtonsoft` | `Serializing` | No dedicated family guide | Newtonsoft.Json-based serialization | `Regira.Serializing.Newtonsoft` |

For Web APIs, the default API surface is OpenAPI plus Scalar — see the setup baseline above.

Use `Office` for the family overview or shared Office conventions. Add one or more concrete `Office.*` modules when the requested capability is already clear.

Modules with multiple provider packages, such as `Office.PDF` or `Office.Excel`, require a deliberate provider choice. Do not guess when the requested behavior is still ambiguous.

## Front-end (Vue 3 SPA) routing

Regira's browser front-end is a separate **npm / TypeScript** family published from the `Regira-Modules`
repo (ids like `regira_modules.vue.entities`) — not the .NET packages above. For a **Vue 3 SPA / admin
UI / CRUD client**, load the **front-end consumer bootstrap** and follow its reading order:
`get_package id="regira_modules" section="frontend.bootstrap"` (or `list_packages category="vue"`).

The front-end **default is a full, scalable SPA** using the complete `regira` package (full plugin
stack + per-entity slice + app shell); only build a headless/lean/demo variant when the user explicitly asks
— see the front-end bootstrap for details.

## Optional local cache

When the MCP is configured, guides are fetched on-demand via `get_package` and no local extraction is needed. The `.regira/instructions/` folder is then a convenience cache — useful for offline work or IDE-local reading — not a prerequisite for the AI workflow.

If the MCP is not configured or unavailable:
- `.regira/instructions/*.md` provides shared setup and module-specific guidance. Installed Regira packages that ship AI files extract them there from their packaged `ai/` content on build via their package props and targets.
- `Regira.Setup` can be installed when the consumer needs `project.setup.md` and `shared.setup.md` extracted locally through the package-based guide flow.

## General engineering rules

Apply these conventions when no narrower module guide exists, or as a supplement when the module guide does not cover the topic. Reuse the setup baseline above for framework, namespace, and web-API defaults instead of re-stating them elsewhere.

### Following conventions

Follow the prescribed conventions by default; deviate deliberately, not by defaulting to a remembered pattern, and declare any **intended deviations** and why. This applies especially to the **Serilog template** (`project.setup` → *Logging*) and the **per-entity file-per-class layout** (`entities.setup` → *Project Structure*).

### Project conventions

Unless the project already constrains you, prefer the latest stable .NET (.NET 10) and C# features that fit the local code style.

### Naming

- Follow normal C# naming conventions.
- Keep names descriptive but concise.
- Prefer meaningful generic type names such as `TEntity`, `TKey`, and `TDto` over bare single-letter names when context allows.
- Use generic names like `item` when the surrounding type already makes the meaning obvious.

### Dependency injection

Prefer `Microsoft.Extensions.DependencyInjection` with feature-focused `IServiceCollection` extension methods.

### Testing

- Choose the smallest suitable test surface for the task.
- Keep tests focused and behavior-oriented.

### SOLID and simplicity

- Default to SOLID design principles, but do not introduce abstractions that the current task does not need.
- Prefer the simplest solution that correctly solves the current problem.
- Avoid speculative flexibility and premature indirection.
- Depend on abstractions instead of concrete implementations when defining business logic.
