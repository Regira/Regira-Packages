# Regira Consumer Project — Copilot Instructions

> **Source of truth:** `ai/AGENTS.md` is the authoritative routing guide for this project. This file is a self-contained subset of that guide optimised for GitHub Copilot. If `ai/AGENTS.md` is available in your context, it takes precedence over anything here.

This file covers the essential rules, templates, and package tables needed before generating code in a project that uses Regira packages.

---

## MCP server (preferred)

A Regira MCP server is available at `https://mcp.regira.com/mcp`. When configured, it provides the full package catalog and all AI guides without requiring a build step.

**Configuration:** add `{ "mcpServers": { "regira": { "url": "https://mcp.regira.com/mcp" } } }` to your AI tool's settings.

**Tools:** `recommend_packages`, `search_packages`, `list_packages`, `get_package`, `get_example`, `get_bootstrap_guide`

---

## Pre-flight checklist

Run this before generating any code:

- [ ] `NuGet.Config` includes the Regira feed `https://packages.regira.com/v3/index.json` alongside `nuget.org`
- [ ] **If MCP is configured:** used `get_package` for each installed Regira module to read full guides — no build step required
- [ ] **If MCP is not configured:** `dotnet restore` and `dotnet build` succeeded so installed Regira packages could extract their embedded `ai/*.md` files into `.regira/instructions/`; that folder was checked for `*.instructions.md` files relevant to the current task
- [ ] Every extracted primary guide relevant to the current task was read before writing application code

---

## Guide loading rules

Use the narrowest relevant guidance. Never load every guide up front.

1. For project scaffolding or app-shape changes → read the project setup guide (`get_package("Regira.Setup", "project.setup")`, or `project.setup.md` locally) in full
2. For shared setup concerns (NuGet feed, logging, OpenAPI) → read `shared.setup.md` in full
3. For module-specific work → read the matching `*.instructions.md` in full before writing code
4. For exact method signatures, namespaces, or examples → consult `*.signatures.md`, `*.namespaces.md`, `*.examples.md` by section on demand
5. **Never guess** a namespace, method name, or package name — look it up or ask

---

## Project template selection

Choose one template before creating any files. For an existing project, infer the nearest match and stay consistent.

| Requirement | Template |
|---|---|
| Script, batch job, or CLI utility | `ConsoleWithLogging` |
| Standard hosted API, no auth | `BasicApi` |
| Lightweight internal API, no auth | `SelfHostingApi` |
| Must be deployable as a Windows Service | `SelfHostingApi` |
| API protected by API key and/or JWT Bearer | `SelfHostingApiWithAuth` |
| Controller-based routing with enforced authorization | `SelfHostingApiWithAuth` |

Template consequences:
- `ConsoleWithLogging`: host-based console setup with configuration and structured logging
- `BasicApi`: ASP.NET Core Web API without authentication
- `SelfHostingApi`: self-hosted baseline, compatible with Windows Service deployment
- `SelfHostingApiWithAuth`: self-hosted with API key and/or JWT Bearer; keep endpoints protected by default

---

## Code generation workflow

1. Choose or confirm the `projectTemplate`
2. Choose the smallest Regira module set that covers the request
3. Ensure `NuGet.Config` includes the Regira feed; add matching packages
4. **If MCP is configured:** call `get_package` for each Regira module in use to read its guides. **Otherwise:** run `dotnet restore` and `dotnet build` to extract embedded guide files, then check `.regira/instructions/` for extracted guides
5. Read all applicable primary guides in full before writing entity models, services, controllers, DI registrations, or infrastructure code
6. Generate code consistent with the template, installed packages, extracted guides, and local conventions

---

## Primary Regira package families

Defaults or recommendations from the dedicated module guides are labeled directly in the table.

| Module | Use when | Main packages and defaults |
|---|---|---|
| Entities | CRUD APIs, entity services, DTO mapping, EF Core repositories, and generated endpoints | `Regira.Entities`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Mapping.Mapster` (default mapping), `Regira.Entities.Mapping.AutoMapper`, `Regira.Entities.EFcore`, `Regira.Entities.Web`, `Regira.Entities.Web.FastEndpoints` |
| IO.Storage | File storage, uploads, Azure Blob, SFTP, ZIP, or SimpleTCP file transfer | `Regira.IO.Storage`, `Regira.IO.Storage.Azure`, `Regira.IO.Storage.SSH`, `Regira.IO.Storage.GitHub`, `Regira.IO.Storage.SimpleTCP` |
| Office.PDF | HTML to PDF, PDF operations, printing | `Regira.Office.PDF.SelectPdf` (preferred for HTML to PDF), `Regira.Office.PDF.DocNET` (preferred for PDF operations), `Regira.Office.PDF.Spire` (preferred when print and PDF ops are both needed); also `Regira.Office.PDF.Puppeteer`, `Regira.Office.PDF.MsPlaywright`, `Regira.Office.PDF.PDFtoPrinter`, `Regira.Office.PDF.PockyBum522` |
| Office.Excel | Excel read and write | `Regira.Office.Excel.MiniExcel` (preferred), `Regira.Office.Excel.ClosedXML`, `Regira.Office.Excel.EPPlus`, `Regira.Office.Excel.NpoiMapper` |
| Office.Word | Word document generation | `Regira.Office.Word.Spire` (preferred), `Regira.Office.Word.Mini` |
| Office.Mail | Email sending, mail DTOs for HTTP endpoints, or reading `.msg` and `.eml` files | `Regira.Office.Mail.SendGrid`, `Regira.Office.Mail.MailGun`, `Regira.Office.Mail.Web`, `Regira.Office.Mail.MSGReader` |
| Office.CSV | CSV read and write | `Regira.Office.Csv.CsvHelper` |
| Office.Barcodes | Barcode or QR code generation | `Regira.Office.Barcodes.ZXing` (preferred), `Regira.Office.Barcodes.Spire`, `Regira.Office.Barcodes.QRCoder`, `Regira.Office.Barcodes.UziGranot` |
| Office.OCR | OCR text extraction | `Regira.Office.OCR.Tesseract`, `Regira.Office.OCR.PaddleOCR` |
| Office.VCards | vCard contact files | `Regira.Office.VCards.FolkerKinzel` |
| Media | Image processing, resize, crop, FFmpeg | `Regira.Media`, `Regira.Drawing.SkiaSharp` (preferred image backend), `Regira.Drawing.GDI`, `Regira.Media.FFMpeg` |
| Security | Hashing, cryptography, and every authentication scheme — self-issued JWT (+ refresh tokens), API keys, cookie sessions, Microsoft Entra ID, OpenID Connect sign-in, multi-scheme selection | `Regira.Security`, `Regira.Security.Hashing.BCryptNet` (preferred for passwords), `Regira.Security.Authentication`, `Regira.Security.Authentication.Web` |
| Web | Razor rendering, middleware, OpenAPI helpers | `Regira.Web`, `Regira.Web.HTML.RazorEngineCore`, `Regira.Web.HTML.RazorLight`, `Regira.Web.Swagger` |
| System | Windows Service hosting, background tasks, and `.csproj` tooling | `Regira.System`, `Regira.System.Hosting`, `Regira.System.Projects` |
| Invoicing | Invoice models, UBL, Peppol, accounting integration, and AP gateway transmission | `Regira.Invoicing`, `Regira.Invoicing.Billit`, `Regira.Invoicing.UblSharp`, `Regira.Invoicing.ViaAdValvas` |
| Payments | Payment providers, payment links, webhooks | `Regira.Payments`, `Regira.Payments.Mollie`, `Regira.Payments.Pom` |
| TreeList | Hierarchical tree structures | `Regira.TreeList` |

Additional packages without dedicated family guides include `Regira.Office.Clients`, `Regira.IO.Compression.SharpZipLib`, and `Regira.Printing.GDI`.

For modules with multiple provider packages (PDF, Excel, etc.), do not guess — ask the user to choose a provider when the request is ambiguous.

Web APIs use `app.MapOpenApi()` plus `app.MapScalarApiReference()` as the standard API surface. Do not add Swashbuckle unless explicitly requested.

---

## General engineering rules

- Use the latest stable .NET and C# features unless the project already constrains otherwise
- Add the Regira feed to `NuGet.Config` before restoring Regira packages
- Keep `Program.cs` thin; move service registration into `IServiceCollection` extension methods
- Prefer `Microsoft.Extensions.DependencyInjection`; depend on abstractions, not concrete implementations
- Use file-scoped namespaces
- Follow standard C# naming: descriptive but concise; prefer `TEntity`, `TKey`, `TDto` for generic parameters
- Default to SOLID principles; do not introduce abstractions the current task does not need
- Prefer the simplest solution that correctly solves the current problem
- Only validate at system boundaries (user input, external APIs); trust internal code and framework guarantees
- Ask rather than guess when a required API, namespace, or convention is not covered by the loaded guides
