# Regira Codebase

Regira is a collection of .NET libraries providing unified abstractions for common application concerns. All packages follow the same pattern: a shared interface in a `Common.*` project, with one or more backend implementations as separate packages.

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
| [Drawing](src/Common.Media) | Image processing, format conversion, and layer composition |
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
| Claude Code (VS Code extension) | `.claude/settings.json` (project) | Tools appear automatically |
| Claude Desktop | `~/.claude/claude_desktop_config.json` | Restart Claude Desktop |
| GitHub Copilot (VS Code) | `.vscode/mcp.json` (project) | Rename the top key `mcpServers` → `servers`; then switch Copilot Chat to **Agent mode** |
| Cursor | — | No file: Settings → MCP Servers → Add server → paste `https://mcp.regira.com/mcp` |

> **Note:** Some tools don't auto-start the server. In VS Code, click the **Start** action shown inline above the server entry in the config file to make sure it's running.

#### Available tools

| Tool | What it does |
|------|-------------|
| `get_bootstrap_guide` | Consumer project setup guide (NuGet config, DI, workflow) |
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

AI guides are first-class artifacts embedded inside each NuGet package under `ai/`. When a Regira package is installed and you run `dotnet build`, its `.targets` file copies those guide files into `.regira/instructions/` at the solution root. The agent therefore sees only the guides relevant to the packages actually installed — detailed implementation instructions, code examples, and API signatures — not the full Regira source tree.

Install `Regira.Setup` to also extract the shared setup guides `project.setup.md` and `shared.setup.md`. Individual module packages extract their own guides the same way.

---

## Licensing

Some Regira packages require a license key at startup. Validation is fully offline using an RSA-signed token. Obtain a license at [https://regira.com/licensing](https://regira.com/licensing).

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

