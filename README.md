# Regira Packages

[![NuGet](https://img.shields.io/nuget/v/Regira.Entities?logo=nuget&label=nuget)](https://www.nuget.org/packages?q=Regira.)
[![Downloads](https://img.shields.io/nuget/dt/Regira.Entities?label=downloads)](https://www.nuget.org/packages/Regira.Entities)
[![License](https://img.shields.io/badge/license-Apache--2.0%20%2B%20commercial-green)](licensing.md)
[![Docs](https://img.shields.io/badge/docs-regira.github.io-blue)](https://regira.github.io/Regira-Packages/)
[![MCP server](https://img.shields.io/badge/MCP-mcp.regira.com-8A2BE2)](https://mcp.regira.com/mcp)

This is the **public, source-available** repository for the Regira NuGet packages, published on [nuget.org](https://www.nuget.org/profiles/regira-bbv). Most packages are [Apache-2.0](LICENSE); the six license-validating packages ship the [Regira Commercial License](legal/REGIRA-COMMERCIAL-LICENSE.md) with a free tier — see [Licensing](#licensing). Homepage: [regira.com](https://regira.com) · Documentation: [regira.github.io/Regira-Packages](https://regira.github.io/Regira-Packages/).

Regira is a collection of .NET libraries providing unified abstractions for common application concerns. All packages follow the same pattern: a shared interface in a `Common.*` project, with one or more backend implementations as separate packages. This repository supersedes the former private Regira-Codebase: public history starts at the 6.0.0 release (2026-08-05), but the libraries were extracted from a longer-running private codebase that powers production systems such as the [live demos](#samples--demos) below.

---

## Core

| Module | Description |
|--------|-------------|
| [Common](src/Common) | Shared foundation — IO abstractions, utilities, normalizing, caching, serializing, DAL contracts |
| [Entities](src/Common.Entities) | Generic entity library for CRUD, filtering, sorting, and EF Core integration |
| [IO.Storage](src/Common.IO.Storage) | Unified file storage — local, Azure Blob, SFTP, GitHub, ZIP |

---

## DAL

| Module | Description |
|--------|-------------|
| [EF Core](src/DAL.EFcore) | Entity Framework Core extensions and utilities |
| [MongoDB](src/DAL.MongoDB) | MongoDB connectivity and backup/restore |
| [MySQL](src/DAL.MySQL) | MySQL/MariaDB connectivity and backup/restore |
| [PostgreSQL](src/DAL.PostgreSQL) | PostgreSQL connectivity and backup/restore |

---

## Media

| Module | Description |
|--------|-------------|
| [Media](src/Common.Media) | Image processing, format conversion, and layer composition |
| [Video](src/Common.Media/docs/video.md) | Video compression and snapshot extraction via FFMpeg |

---

## Office

| Module | Description |
|--------|-------------|
| [Office (overview)](src/Common.Office) | All Office submodule index |
| [Barcodes](src/Common.Office/docs/barcodes) | Barcode and QR code generation and scanning |
| [CSV](src/Common.Office/docs/csv) | CSV reading and writing |
| [Excel](src/Common.Office/docs/excel) | Excel workbook reading and writing |
| [Mail](src/Common.Office/docs/mail) | Email sending via SendGrid and Mailgun, `.msg`/`.eml` reading, and mail DTOs for HTTP endpoints |
| [OCR](src/Common.Office/docs/ocr) | Optical character recognition |
| [PDF](src/Common.Office/docs/pdf) | HTML→PDF, PDF operations, and printing |
| [VCards](src/Common.Office/docs/vcards) | vCard contact file reading and writing |
| [Word](src/Common.Office/docs/word) | Word document creation, conversion, merge, and extraction |

---

## Infrastructure

| Module | Description |
|--------|-------------|
| [Security](src/Common.Security) | Encryption, hashing, and authentication — JWT with refresh tokens, API keys, cookies, Entra ID, and OpenID Connect |
| [Serializing](src/Serializing.Newtonsoft) | JSON serialization via Newtonsoft.Json |
| [System](src/Common.System) | Process management, scheduling, and system utilities |

---

## Web

| Module | Description |
|--------|-------------|
| [Web / HTML](src/Common.Web) | Razor template rendering, middleware, Swagger, and background tasks |

---

## Utilities

| Module | Description |
|--------|-------------|
| [Globalization](src/Globalization.LibPhoneNumber) | Phone number parsing and formatting via libphonenumber |
| [Invoicing](src/Common.Invoicing) | Invoice models and structured number parsing |
| [Payments](src/Common.Payments) | Payment abstractions and structured reference numbers |
| [TreeList](src/TreeList) | Generic hierarchical tree structures with navigation extension methods |

---

## Install

It's just NuGet — every package installs straight from nuget.org, no custom feed:

```sh
dotnet add package Regira.Entities.Web      # entity CRUD/REST over EF Core
dotnet add package Regira.IO.Storage        # unified file storage
dotnet add package Regira.Office            # PDF/Excel/Word/mail contracts
```

All packages target `net8.0` and `net10.0`. See [docs/quickstart.md](docs/quickstart.md) for an end-to-end first project.

---

## Using Regira in your project

AI assistance for Regira centers on a hosted MCP server. **Connecting it is usually all you need** — the agent can both discover packages and fetch the same bootstrap guidance an `AGENTS.md` file provides, without installing anything first.

It's also lighter on tokens: the agent pulls only the guidance relevant to the task on demand, instead of loading full instruction files into its context window up front.

Two optional layers add repo-local context when you want it:

- **Bootstrap file** — committing an `AGENTS.md` file gives the agent the setup workflow even when the MCP server isn't connected.
- **Per-package guides** — guide files extracted locally on `dotnet build` give the agent detailed implementation instructions for the packages you've installed.

### Connect the MCP server (recommended)

The hosted server lives at `https://mcp.regira.com/mcp`. Most clients share the same config — add this block to the file your tool reads, then reload it:

```json
{
  "mcpServers": {
    "regira": {
      "url": "https://mcp.regira.com/mcp",
      "transport": "http"
    }
  }
}
```

| Client | Config file | Notes |
|--------|-------------|-------|
| Claude Code (VS Code extension) | `.mcp.json` (repo root) | Tools appear automatically |
| Claude Desktop | `claude_desktop_config.json` in the Claude app-data folder (Windows: `%APPDATA%\Claude`, macOS: `~/Library/Application Support/Claude`) | Restart Claude Desktop |
| GitHub Copilot (VS Code) | `.vscode/mcp.json` (project) | Rename the top key `mcpServers` → `servers`; then switch Copilot Chat to **Agent mode** |
| Cursor | — | No file: Settings → MCP Servers → Add server → paste `https://mcp.regira.com/mcp` |

> **Note:** Some tools don't auto-start the server. In VS Code, click the **Start** action shown inline above the server entry in the config file to make sure it's running.

#### Available tools

| Tool | What it does |
|------|-------------|
| `get_bootstrap_guide` | Consumer project setup guide (project template, DI, workflow) |
| `list_packages` | Browse all packages, optionally filtered by category |
| `search_packages` | Keyword/use-case search, returns ranked results |
| `search_docs` | Full-text search over every package's documentation content — returns ranked `(package, section, heading)` hits with a snippet and the `get_package(...)` call to read each. Optional `package` scope and `limit` |
| `recommend_packages` | Describe a feature, get package suggestions (optional `platform`: `backend`/`frontend`) |
| `get_package_card` | Compact "must-know in 10 bullets" index card for one package — the fastest orientation before drilling in |
| `get_package` | Full docs and setup instructions for one package. Optional `section` key and `heading` narrow the response; `maxChars`/`page` paginate |
| `get_package_toc` | List the documentation section keys available for a package |
| `get_section_toc` | List the headings within a specific section file, before fetching one with `get_package heading=<text>` |
| `get_example` | Search for code examples by topic keyword within a specific package — returns only matching sections, saving context. Accepts an optional `section` parameter (e.g. `examples`, `instructions`) to narrow the search. |
| `list_types` | List public types from a package source map, with optional namespace / kind / name filters |
| `get_type` | Source-map details for a type (namespace, kind, inheritance, members); searches sibling packages automatically |
| `how_to` | Task-oriented "how do I do X in code?" recipes for common Regira Entities tasks (attachments, seeding, back-dating, service resolution) |

### Optional — Commit a bootstrap file

If you're not connecting the MCP server, copy [ai/AGENTS.md](ai/AGENTS.md) into the root of your application repository as `AGENTS.md`. This gives the agent the full Regira project setup workflow: NuGet configuration, project templates, and which packages to install for common scenarios.

This works for both a new empty folder and an existing application that needs extra Regira features. With the MCP server connected, the agent retrieves this same guide via `get_bootstrap_guide`, so copying the file is unnecessary.

### Optional — Per-package guides (post-install)

AI guides are first-class artifacts embedded inside NuGet packages under `ai/`. Most guide-carrying packages also ship a `build/*.targets` file; when such a package is installed and you run `dotnet build`, that `.targets` file copies its guide files into `.regira/instructions/` at the solution root. (A few packages pack guides without a local-extraction `.targets` file — those guides are served via the MCP server only — and `Regira.Entities` extracts seven of its nine packed guides.) The agent therefore sees only the guides relevant to the packages actually installed — detailed implementation instructions, code examples, and API signatures — not the full Regira source tree.

Install `Regira.Setup` to also extract the shared setup guides `project.setup.md` and `shared.setup.md`. Individual module packages extract their own guides the same way.

---

## Samples & demos

- **[Regira-Samples](https://github.com/Regira/Regira-Samples)** — three self-contained ASP.NET Core sample APIs built on Regira Entities, generated end-to-end with the MCP server.
- **Runnable in this repo** — [`tests/Entities.TestApi`](tests/Entities.TestApi) is a complete Entities Web API (Sqlite, attachments, OpenAPI): `dotnet run --project tests/Entities.TestApi`, then open the Scalar UI it logs at startup.
- **Live demos** — [Fleet Manager](https://fleet-demo.regira.com/) and [PIM Manager](https://pim.regira.com/manager/) run on these packages, demo logins included. Sources: [Regira/RegiraFleet-Backend](https://github.com/Regira/RegiraFleet-Backend), [Regira/Regira-PIM-Backend](https://github.com/Regira/Regira-PIM-Backend).

---

## Licensing

At a glance:

| Packages | License | Key needed? |
|----------|---------|-------------|
| Everything except the seven below | [Apache-2.0](LICENSE) | Never — no license validation |
| `Regira.Licensing`, `Regira.Entities.EFcore`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Web`, `Regira.Entities.Mapping.Mapster`, `Regira.Entities.Mapping.AutoMapper`, `Regira.Office.Clients` | [Regira Commercial](legal/REGIRA-COMMERCIAL-LICENSE.md) — free tier included | Only beyond the free tier |

Full limits, definitions, and prices: [licensing.md](licensing.md).

The commercial packages validate an optional license key at startup. Validation is fully offline using an RSA-signed token. Obtain a license at [https://regira.com/licensing](https://regira.com/licensing).

Register each license **once**, before the corresponding module setup. Without a key the **free tier** applies automatically; a single key can cover multiple products.

### From configuration

Reads keys from `Regira:LicenseKeys`, ignoring blanks:

```csharp
services.UseRegira(configuration);
```

```json
{
  "Regira": {
    "LicenseKeys": [
      "<your-license-key>"
    ]
  }
}
```

### Explicit keys

Pass one or more keys directly:

```csharp
services.UseRegira(licenseKey);
```

When several keys are registered, the best license per product is selected (paid always wins over free).

