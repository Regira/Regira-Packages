# Regira Consumer Project — Copilot Instructions

> **Source of truth:** the Regira consumer bootstrap — `get_bootstrap_guide(heading: "toc")` over the MCP server, or `./AGENTS.md` at the repo root. This file is a self-contained subset of that guide optimised for GitHub Copilot. When the bootstrap is available in your context, it takes precedence over anything here.

This file covers the essential rules, templates, and package tables needed before generating code in a project that uses Regira packages.

---

## MCP server (preferred)

A Regira MCP server is available at `https://mcp.regira.com/mcp`. When configured, it provides the full package catalog and all AI guides without requiring a build step.

**Configuration (VS Code / Copilot):** add `{ "servers": { "regira": { "type": "http", "url": "https://mcp.regira.com/mcp" } } }` to `.vscode/mcp.json`, click **Start** above the entry, and use Agent mode. Claude Code takes the same entry under `mcpServers` in `.mcp.json`; the `"type": "http"` line is required.

**Tools, cheapest first.** Package arguments are forgiving (`id`, `pkg` and `package` all resolve); the search term is not — it is `query` on `search_packages` / `search_docs`, `pattern` on `get_example`, `feature` on `recommend_packages`, and `task` on `how_to`. A wrong guess is dropped silently rather than rejected.

| Tool | Call | Purpose |
|---|---|---|
| `get_bootstrap_guide` | `get_bootstrap_guide(heading: "toc")` | Start here; `platform: "frontend"` for the Vue SPA guide |
| `recommend_packages` | `recommend_packages(feature: "shopping list API with QR codes")` | First-pass package suggestions from a feature description |
| `search_packages` | `search_packages(query: "PDF generation")` | Package discovery from keywords |
| `list_packages` | `list_packages(category: "entities")` | Browse the catalog when the fit is still broad |
| `get_package_card` | `get_package_card(id: "Regira.Entities")` | **Orient here before loading sections** — the must-know card per package; often enough on its own |
| `get_package_toc` | `get_package_toc(id: "Regira.Entities")` | List a package's section keys |
| `get_section_toc` | `get_section_toc(id: "Regira.Entities", section: "entities.examples")` | List a section's headings before loading content |
| `get_package` | `get_package(id: "Regira.Entities", section: "entities.examples", heading: "Order + OrderLine entities")` | Read the guidance, narrowed to a section or heading |
| `search_docs` | `search_docs(query: "soft delete", package: "Regira.Entities")` | **When you don't know where a topic lives** — searches every package at once and returns the exact `get_package(...)` call to read each hit |
| `get_example` | `get_example(id: "Regira.Entities", pattern: "many-to-many join")` | Pull matching examples once you know what you want |
| `how_to` | `how_to(task: "seed data")` | "How do I do X in code?" for common Entities tasks |
| `list_types` / `get_type` | `get_type(id: "Regira.Entities", typeName: "IEntityService")` | Exact signatures without loading doc sections |
| `get_license_status` | `get_license_status()` | When calls come back rate-limited unexpectedly, or to check a key's expiry — reports what the server made of your `X-License-Key`. Works without a key and with an expired one |

---

## Pre-flight checklist

Run this before generating any code:

- [ ] **If using licensed packages (e.g. `Regira.Entities.DependencyInjection`, `Regira.Office.Clients`):** on the **free tier no `UseRegira()` call is needed** — free limits apply automatically. With license keys, store them under `Regira:LicenseKeys` and call `services.UseRegira(configuration)` **before** any module setup call (e.g. `UseEntities()`). A single key can cover multiple products; add more keys to the array to combine them, and the system picks the best per product (paid always wins over free)
- [ ] **Entities free tier counted:** 5 simple + 2 complex `.For<>()` registrations per application. Owned collections configured through a parent's `Related(...)` are not entities and cost no slot. Count top-level entities before designing, and tell the user if the design won't fit
- [ ] **If MCP is configured:** oriented with `get_package_card`, then used `get_package_toc` / `get_section_toc` / `get_package` to read the relevant sections for each installed Regira module — no build step required
- [ ] **If MCP is not configured:** `dotnet restore` and `dotnet build` succeeded so installed Regira packages could extract their embedded `ai/*.md` files into `.regira/instructions/` at the solution root; that folder was checked for `*.instructions.md` files relevant to the current task
- [ ] The core sections of every primary guide relevant to the current task were read before writing application code

---

## Guide loading rules

Use the narrowest relevant guidance. Never load every guide up front.

Primary guides follow a **minimum viable read**: the package card first (often sufficient to orient), then the guide's core sections — decision material, workflow steps, patterns — before generating code in that area. Troubleshooting tables and quick-reference sections are for lookup: fetch a row when you have the symptom, not as pre-reading.

1. For project scaffolding or app-shape changes → the project setup guide (`get_package(id: "Regira.Setup", section: "project.setup")`, or `project.setup.md` locally)
2. For shared setup concerns (logging, OpenAPI) → `shared.setup.md`
3. For module-specific work → the matching `*.instructions.md` before writing code
4. For exact signatures, namespaces, or examples → `get_type`, or `*.signatures.md` / `*.namespaces.md` / `*.examples.md` by section on demand
5. **Never guess** a namespace, method name, or package name — look it up or ask

---

## Project template selection

Choose one template before creating any files. For an existing project, infer the nearest match and stay consistent.

| Requirement | Template |
|---|---|
| Script, batch job, or CLI utility | `ConsoleWithLogging` |
| Standard hosted API, no auth | `BasicApi` |
| Standard hosted API **with** auth (incl. an authenticated Entities API) | `BasicApi` + the auth registrations from `SelfHostingApiWithAuth` |
| Lightweight internal API, no auth | `SelfHostingApi` |
| Must be deployable as a Windows Service | `SelfHostingApi` |
| Self-hosted API protected by API key and/or JWT Bearer | `SelfHostingApiWithAuth` |
| Self-hosted, controller-based routing with enforced authorization | `SelfHostingApiWithAuth` |

Template consequences:
- `ConsoleWithLogging`: host-based console setup with configuration and structured logging
- `BasicApi`: ASP.NET Core Web API, hosted on IIS/Azure/Docker; no auth by default, and the auth registrations from `SelfHostingApiWithAuth` layer onto it unchanged
- `SelfHostingApi`: self-hosted baseline, compatible with Windows Service deployment
- `SelfHostingApiWithAuth`: self-hosted with API key and/or JWT Bearer; keep endpoints protected by default

---

## Code generation workflow

1. Choose or confirm the `projectTemplate`
2. Choose the smallest Regira module set that covers the request
3. Add matching packages
4. **If MCP is configured:** orient with `get_package_card`, then read the sections you need for each Regira module in use. **Otherwise:** run `dotnet restore` and `dotnet build` to extract embedded guide files, then check `.regira/instructions/`
5. Read the core sections of every applicable primary guide before writing entity models, services, controllers, DI registrations, or infrastructure code
6. Generate code consistent with the template, installed packages, extracted guides, and local conventions

---

## Primary Regira package families

Defaults or recommendations from the dedicated module guides are labeled directly in the table.

| Module | Use when | Main packages and defaults |
|---|---|---|
| Entities | CRUD APIs, entity services, DTO mapping, EF Core repositories, and generated endpoints | `Regira.Entities`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Mapping.Mapster` (default mapping), `Regira.Entities.Mapping.AutoMapper`, `Regira.Entities.EFcore`, `Regira.Entities.Web` |
| IO.Storage | File storage, uploads, Azure Blob, SFTP, ZIP, or SimpleTCP file transfer | `Regira.IO.Storage`, `Regira.IO.Storage.Azure`, `Regira.IO.Storage.SSH`, `Regira.IO.Storage.GitHub`, `Regira.IO.Storage.SimpleTCP` |
| Office | Family overview, or when the user still needs to choose between PDF, Excel, Word, Mail, OCR, and related submodules | `Regira.Office` |
| Office.PDF | HTML to PDF, PDF operations, printing | `Regira.Office.PDF.SelectPdf` (preferred for HTML to PDF), `Regira.Office.PDF.DocNET` (preferred for PDF operations), `Regira.Office.PDF.Spire` (preferred when print and PDF ops are both needed); also `Regira.Office.PDF.Puppeteer`, `Regira.Office.PDF.MsPlaywright`, `Regira.Office.PDF.PDFtoPrinter`, `Regira.Office.PDF.PockyBum522` |
| Office.Excel | Excel read and write | `Regira.Office.Excel.MiniExcel` (preferred), `Regira.Office.Excel.ClosedXML`, `Regira.Office.Excel.EPPlus`, `Regira.Office.Excel.NpoiMapper` |
| Office.Word | Word document generation | `Regira.Office.Word.Spire` (preferred), `Regira.Office.Word.Syncfusion` (licence key, no size cap), `Regira.Office.Word.Aspose` (licence, loads ODT and writes EPUB), `Regira.Office.Word.Mini`, `Regira.Office.Word.Gotenberg` (PDF and page images through a Gotenberg server) |
| Office.Mail | Email sending, mail DTOs for HTTP endpoints, or reading `.msg` and `.eml` files | `Regira.Office.Mail.SendGrid`, `Regira.Office.Mail.MailGun`, `Regira.Office.Mail.Web`, `Regira.Office.Mail.MSGReader` |
| Office.CSV | CSV read and write | `Regira.Office.Csv.CsvHelper` |
| Office.Barcodes | Barcode or QR code generation | `Regira.Office.Barcodes.ZXing` (preferred), `Regira.Office.Barcodes.Spire`, `Regira.Office.Barcodes.QRCoder`, `Regira.Office.Barcodes.UziGranot` |
| Office.OCR | OCR text extraction | `Regira.Office.OCR.Tesseract`, `Regira.Office.OCR.PaddleOCR` |
| Office.VCards | vCard contact files | `Regira.Office.VCards.FolkerKinzel` |
| Media | Image processing, resize, crop, rotate, FFmpeg workflows | `Regira.Media`, `Regira.Drawing.SkiaSharp` (preferred image backend), `Regira.Drawing.GDI`, `Regira.Media.FFMpeg` |
| Security | Hashing, cryptography, and every authentication scheme — self-issued JWT (+ refresh tokens), API keys, cookie sessions, Microsoft Entra ID, OpenID Connect sign-in, multi-scheme selection | `Regira.Security`, `Regira.Security.Hashing.BCryptNet` (preferred for passwords), `Regira.Security.Authentication`, `Regira.Security.Authentication.Web` |
| Web | Razor rendering, middleware, visit/page-view analytics, and optional Swagger/OpenAPI auth helpers | `Regira.Web`, `Regira.Web.Analytics`, `Regira.Web.Analytics.GeoIP2`, `Regira.Web.HTML.RazorEngineCore`, `Regira.Web.HTML.RazorLight`, `Regira.Web.Swagger` |
| System | Windows Service hosting, background tasks, and `.csproj` project tooling | `Regira.System`, `Regira.System.Hosting`, `Regira.System.Projects` |
| Invoicing | Invoice models, UBL, Peppol, accounting integration, and AP gateway transmission | `Regira.Invoicing`, `Regira.Invoicing.Billit`, `Regira.Invoicing.UblSharp`, `Regira.Invoicing.ViaAdValvas` |
| Payments | Payment providers, payment links, webhooks | `Regira.Payments`, `Regira.Payments.Mollie`, `Regira.Payments.Pom` |
| TreeList | Hierarchical tree structures | `Regira.TreeList` |
| Setup | Shared project-template and setup-guide extraction for local AI guidance | `Regira.Setup` |

### Packages without a dedicated family guide

Rely on this file plus local project conventions for these; there is no `*.instructions.md` to load.

| Module | Use when | Packages |
|---|---|---|
| Common | Shared abstractions, utilities, normalizing helpers, base contracts | `Regira.Common` |
| Caching | Runtime caching on top of the common abstractions | `Regira.Caching.Runtime` |
| Licensing | License key registration and offline validation — `UseRegira(configuration)` or `UseRegira(licenseKey)` before any module setup call (see the pre-flight checklist) | `Regira.Licensing` |
| DAL.EFcore | EF Core extensions and repository utilities | `Regira.DAL.EFcore` |
| DAL.MongoDB | MongoDB connectivity and backup or restore workflows | `Regira.DAL.MongoDB` |
| DAL.MySQL | MySQL or MariaDB connectivity and backup workflows | `Regira.DAL.MySQL`, `Regira.DAL.MySQL.MySqlBackup` |
| DAL.PostgreSQL | PostgreSQL connectivity | `Regira.DAL.PostgreSQL` |
| DAL.SqlServer | SQL Server backup and restore (native `.bak`) | `Regira.DAL.SqlServer` |
| Globalization | Phone number parsing and formatting | `Regira.Globalization.LibPhoneNumber` |
| Serializing | Newtonsoft.Json-based serialization | `Regira.Serializing.Newtonsoft` |
| IO.Compression | ZIP archive creation and extraction, especially password-protected ZIP files | `Regira.IO.Compression.SharpZipLib` |
| Office.Clients | HTTP client extensions for consuming Regira Office services remotely | `Regira.Office.Clients` |
| Printing | GDI-based document printing utilities on Windows | `Regira.Printing.GDI` |

For modules with multiple provider packages (PDF, Excel, etc.), do not guess — ask the user to choose a provider when the request is ambiguous.

---

## Front-end (Vue 3 SPA) routing

Regira's browser front-end is a separate **npm / TypeScript** family published from the `Regira-Modules` repo (ids like `regira_modules.vue.entities`) — **not** the .NET packages above. For a Vue 3 SPA, admin UI, or CRUD client, load the front-end consumer bootstrap and follow its reading order before choosing a UI framework, package manager, or project structure:

```
get_bootstrap_guide(platform: "frontend")
```

or `get_package(id: "regira_modules", section: "frontend.bootstrap")`. Do not route SPA work to the .NET packages.

The front-end default is a **full, scalable SPA** using the complete `regira` package (full plugin stack + per-entity slice + app shell); build a headless or lean variant only when the user explicitly asks.

---

## Setup baseline

Keep setup aligned with the selected `projectTemplate`.

- Use the latest stable .NET framework and C# features unless the project already targets something else. The LTS version is **.NET 10**
- When adding a NuGet package, install the latest stable version rather than pinning an older one — outdated versions may carry known vulnerabilities. After restoring, run `dotnet list package --vulnerable --include-transitive` and upgrade anything flagged
- Keep `Program.cs` thin; move service registration and middleware setup into `IServiceCollection` extension methods
- Prefer `Microsoft.Extensions.DependencyInjection` and depend on abstractions instead of concrete implementations
- Use file-scoped namespaces
- Web APIs use `app.MapOpenApi()` plus `app.MapScalarApiReference()` as the standard API surface. Do not add `Swashbuckle.AspNetCore` or call `UseSwaggerUI()` on the standard Regira API path unless the user explicitly asks for Swagger
- Prefer meaningful generic type names such as `TEntity`, `TKey`, and `TDto`, matching the Regira API's own generic parameters
- Ask rather than guess when a required API, namespace, or convention is not covered by the loaded guides

General .NET, EF Core, and C# correctness is assumed knowledge and is not restated here. Own it before the first build — including C# name resolution when a type name collides with a namespace segment, NuGet version selection (confirm a version exists; never guess a patch number), and EF Core provider major ↔ TFM alignment, which builds clean and fails on the first query.
