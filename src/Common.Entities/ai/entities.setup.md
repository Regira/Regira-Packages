# Regira Entities — Project Setup

> **AI Agent Rule**: Follow this guide to scaffold a new Regira Entities API project from scratch.
> Start from the **`BasicApi`** template in the shared project setup guide — `get_package(id: "Regira.Setup", section: "project.setup")` — and apply the Entities-specific additions below.
> In consumer repositories, prefer extracted `.regira/instructions/project.setup.md` when it exists locally. If it is not available yet, use the fallback baseline in this guide and keep the API surface aligned with `app.MapOpenApi()` plus `app.MapScalarApiReference()`.
> When available, combine with [`entities.namespaces.md`](./entities.namespaces.md) for exact `using` directives
> and [`entities.examples.md`](./entities.examples.md) for complete working code.
>
> The numbered **P-steps** below (P1–P4) are the one-time project bootstrap — distinct from the per-entity **Steps 1–14** in [`entities.instructions.md`](./entities.instructions.md#entity-implementation-workflow).

---

## Defaults

**Defaults (unless instructed otherwise):**
- **Database**: SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Database initialization**: prefer `Database.EnsureCreated()` for the default SQLite starter/test setup; keep the local database disposable and do not scaffold an initial migration unless the user explicitly asks for migrations or chooses a more mature database
- **Mapping**: Mapster (`Regira.Entities.Mapping.Mapster`)
- **Project structure**: Per-entity folder structure
- **Service layer**: Default `EntityRepository` (unless complex logic requires wrapping)
- **Many-to-many relationships**: prefer option A
- **Web endpoints**: Controllers inheriting from `EntityControllerBase`

---

## Project Structure

The recommended **per-entity folder structure** — one folder per entity under `Entities/`, keeping its model, DTOs, search object, processor/manager, and service configuration together. Controllers live under `Controllers/` (one per entity), the DbContext under `Data/`, and DI wiring under `Extensions/`:

```
Webshop.API/
├── Controllers/
│   ├── CategoryController.cs
│   ├── CustomerController.cs
│   ├── OrderController.cs
│   └── ProductController.cs
├── Data/
│   └── WebshopDbContext.cs
├── Entities/
│   ├── Categories/
│   │   ├── Category.cs
│   │   ├── CategoryDto.cs
│   │   ├── CategoryInputDto.cs
│   │   ├── CategoryProcessor.cs
│   │   ├── CategorySearchObject.cs
│   │   ├── CategoryServiceConfiguration.cs
│   │   ├── RelatedCategory.cs
│   │   ├── RelatedCategoryDto.cs
│   │   └── RelatedCategoryInputDto.cs
│   ├── Customers/
│   │   ├── Customer.cs
│   │   ├── CustomerDto.cs
│   │   ├── CustomerInputDto.cs
│   │   └── CustomerServiceConfiguration.cs
│   ├── Orders/
│   │   ├── Order.cs
│   │   ├── OrderDto.cs
│   │   ├── OrderIncludes.cs
│   │   ├── OrderInputDto.cs
│   │   ├── OrderLine.cs
│   │   ├── OrderLineDto.cs
│   │   ├── OrderLineInputDto.cs
│   │   ├── OrderManager.cs
│   │   ├── OrderNormalizer.cs
│   │   ├── OrderQueryBuilder.cs
│   │   ├── OrderSearchObject.cs
│   │   ├── OrderServiceConfiguration.cs
│   │   └── OrderStatus.cs
│   └── Products/
│       ├── Product.cs
│       ├── ProductCategory.cs
│       ├── ProductCategoryDto.cs
│       ├── ProductCategoryInputDto.cs
│       ├── ProductDto.cs
│       ├── ProductInputDto.cs
│       ├── ProductQueryBuilder.cs
│       ├── ProductSearchObject.cs
│       ├── ProductServiceConfiguration.cs
│       └── ProductSortBy.cs
├── Extensions/
│   └── ServiceCollectionExtensions.cs
└── Program.cs
```

---

## Checklist

0. **Plan the simple/complex split first.** Mark every entity *simple* or *complex* **before** scaffolding — it sets each entity's endpoints and controller generics, and the free-tier budget. Definitions, the decision table, and a worked budget example: [entities.instructions §Step 0](./entities.instructions.md#step-0--classify-every-entity-before-scaffolding).
0.5. **Pin your EF Core provider to your target framework.** Regira's EF Core packages multi-target, so the provider you add (`Microsoft.EntityFrameworkCore.Sqlite`/`.SqlServer`/`Npgsql.EntityFrameworkCore.PostgreSQL`/…) **must match the EF Core major for your TFM**: **`net8.0`/`net9.0` → 9.x**, **`net10.0` → 10.x**. A mismatch builds cleanly but crashes on the first query — see [Troubleshooting](./entities.instructions.md#troubleshooting).
1. Create an ASP.NET Core Web API project — use the **`BasicApi`** template in the shared project setup guide (`get_package(id: "Regira.Setup", section: "project.setup")`) as the starting point.
2. Add required packages to `.csproj`.
3. Create `YourDbContext` deriving from `DbContext`.
4. Configure `Program.cs`.
5. Create the DI extension method (`AddEntityServices`).
6. Add entities — see [Entity Implementation Workflow](./entities.instructions.md#entity-implementation-workflow).

---

## Packages

Each package references the one below it, so installing the top of the stack pulls the rest in
transitively. **A Web API needs only two references: `Regira.Entities.Web` + a mapper.**

```
Regira.Entities.Web                       ← Web API entry point — EntityControllerBase + HTTP endpoints
└─ Regira.Entities.DependencyInjection    ← non-web entry point — UseEntities() / .For<>()
   └─ Regira.Entities.EFcore              EntityRepository (EF Core)
      └─ Regira.Entities                  abstractions / interfaces

Regira.Entities.Mapping.Mapster           ← add separately — DTO mapping (NOT pulled transitively)
```

| Host | Install |
|---|---|
| **Web API** (default) | `Regira.Entities.Web` + `Regira.Entities.Mapping.Mapster` |
| **Console / worker** (no HTTP) | `Regira.Entities.DependencyInjection` + `Regira.Entities.Mapping.Mapster` |

> `Regira.Entities.Mapping.AutoMapper` is an alternative to Mapster (deprecated).

> **EF Core provider** (e.g. `Microsoft.EntityFrameworkCore.Sqlite`): pin its major to your TFM — see **Checklist 0.5**.
>
> | Your TFM | EF Core provider major |
> |---|---|
> | `net8.0` / `net9.0` | **9.x** |
> | `net10.0` | **10.x** |
>
> A mismatch (e.g. an EF Core 10 provider on `net8.0`) **restores and builds cleanly, then throws on the first
> query at runtime** — the hardest variant to diagnose. Pin the provider major explicitly.

**Optional — entity attachments:** `Regira.IO.Storage` (local file system / SFTP) or
`Regira.IO.Storage.Azure` (Azure Blob Storage).

> **NuGet advisory with no patched version (NU1903 / NU1902).** A transitive dependency can raise a security
> advisory for which no fixed version exists yet (commonly seen via `SQLitePCLRaw.bundle_e_sqlite3` pulled in by
> the SQLite provider). Options:
> - **Float the transitive up** by adding an explicit higher version as a direct reference, lifting the resolved
>   version above the advisory. The advisory affects the whole `2.1.x` line — only the `3.x` line clears it, so
>   float to the latest `3.x`:
>   ```xml
>   <PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.3" />
>   ```
>   Then verify the advisory is gone: `dotnet list package --vulnerable --include-transitive`.
> - **Or accept/suppress it** when no fixed version is available and the advisory does not affect your usage:
>   ```xml
>   <PropertyGroup>
>     <NoWarn>$(NoWarn);NU1903</NoWarn>
>   </PropertyGroup>
>   ```
>   Re-check on each restore and drop the suppression once a patched version ships.

**Known-good package versions.** What these guides assume, so nothing resolves from `*`. Two kinds of row: an **exact pin** you copy verbatim, and a **major constraint** where the patch is whatever `dotnet add package` resolves today — never invent a patch number for those.

| Package | Version |
|---|---|
| `Regira.Entities.*` | major **6**; resolve the patch at add time. The whole family ships one version at a time — keep every `Regira.*` reference on the same one, or restore reports NU1605 |
| `Microsoft.OpenApi` (direct reference; also transitive via `Microsoft.AspNetCore.OpenApi`) | pin **2.11.0** — clears the advisory on 2.0.0 and matches the floor `Regira.Security.Authentication.Web` sets (a lower pin fails restore with NU1605 when that package is referenced). **Stay on 2.x**: 3.x breaks the .NET 10 OpenAPI source generator |
| `SQLitePCLRaw.bundle_e_sqlite3` | pin **3.0.3** |
| `Microsoft.EntityFrameworkCore.*` (+ provider) | major must equal the TFM's EF Core major (`net10.0` → **10.x**, see Checklist 0.5); resolve the patch at add time |
| `Microsoft.AspNetCore.OpenApi` | major must equal your TFM (`net10.0` → **10.x**); resolve the patch at add time |
| `Scalar.AspNetCore` | major **2**; resolve the patch at add time |
| `Serilog.AspNetCore`, `Serilog.Settings.Configuration`, `Serilog.Sinks.Console` | latest **stable** major — never a preview; resolve the patch at add time. The console/file sinks arrive transitively with `Serilog.AspNetCore`, so add them explicitly only if you pin them |

**Add them as commands, not as hand-written XML.** Which rows you may type by hand is then structural rather than a rule to remember: only the two exact pins below are XML, and every major-constraint row resolves its own patch.

```bash
dotnet add package Regira.Entities.Web --version "6.*"            # newest 6.x; keep both Regira rows on the same version
dotnet add package Regira.Entities.Mapping.Mapster --version "6.*"
dotnet add package Microsoft.EntityFrameworkCore.Sqlite     # patch resolved at add time; check the major against your TFM
dotnet add package Microsoft.AspNetCore.OpenApi
dotnet add package Scalar.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Settings.Configuration
```
```xml
<!-- the two rows that are pinned rather than resolved -->
<PackageReference Include="Microsoft.OpenApi" Version="2.11.0" />
<PackageReference Include="SQLitePCLRaw.bundle_e_sqlite3" Version="3.0.3" />
```

⚠️ A patch number you invent restores with **NU1603** ("*x.y.z was not found; x.y.z+n was resolved instead*") — or **NU1605** when a sibling package pulls a higher version of the same dependency. Both are warnings, so a hand-authored `.csproj` can carry a package version that does not exist and still build.

---

## P1: Project Files

> Start from the **`BasicApi`** template in the shared project setup guide and apply the Entities-specific additions below.
> **Needs sign-in?** Stay on `BasicApi` and layer `SelfHostingApiWithAuth`'s auth registrations onto it — the
> selection guide's *Standard hosted API with auth* row spells out which parts to take. Hosting and auth are
> separate choices; this combination is ordinary, not a deviation.
> - **Via MCP:** call `get_package(id: "Regira.Setup", section: "project.setup")` to read the template.
> - **Via local extraction:** read `.regira/instructions/project.setup.md` if it exists.
> - **Fallback (if neither is available):** ASP.NET Core Web API project, thin `Program.cs`, DI via extension methods, `app.MapOpenApi()` (requires `Microsoft.AspNetCore.OpenApi`), `app.MapScalarApiReference()` (requires `Scalar.AspNetCore` + `using Scalar.AspNetCore;`), and no `UseSwaggerUI()`.

> **⚠️ Source encoding — keep `.cs` files ASCII-only.** A `.cs` file saved as UTF-8 *without* a BOM that
> contains non-ASCII characters (`→`, `×`, `é`, an em dash, smart quotes — in comments, seed data or string
> literals) is misread by the C# compiler/tooling on Windows as Windows-1252, producing mojibake
> (`Lille Studio â€” Meeting rooms`) that survives into the database and out through the API.
>
> **Write plain ASCII and use `\uXXXX` escapes for any non-ASCII literal.** That is the only remedy that
> holds when files are written by a tool: an `.editorconfig` configures *editors*, so a process writing bytes
> directly ignores it and every file it creates is BOM-less regardless. Non-ASCII prose in comments is the
> norm, so this bites early and the fix is a full reseed.
>
> Audit and escape an existing file without improvising a script — this reads first and writes once, so a
> failure mid-way cannot truncate the source:
> ```bash
> # report every non-ASCII character, with its line number
> grep -nP "[^\x00-\x7F]" Data/SeedCatalog.cs
> # rewrite the file with \uXXXX escapes in place of them
> python -c "import sys,io; p=sys.argv[1]; s=io.open(p,encoding='utf-8').read(); io.open(p,'w',encoding='ascii').write(''.join(c if ord(c)<128 else '\\\\u%04x'%ord(c) for c in s))" Data/SeedCatalog.cs
> ```
>
> If you do write non-ASCII, the file must carry a UTF-8 BOM — verify it after writing, per file, and pin the
> editor default with an `.editorconfig` at the repo root:
>
> ```ini
> root = true
>
> [*.cs]
> charset = utf-8-bom
> indent_style = space
> indent_size = 4
> ```

---

## P2: Create DbContext

> **→ See:** [`entities.examples.md`](./entities.examples.md) — DbContext

- Call `modelBuilder.SetDecimalPrecisionConvention()` in `OnModelCreating` for global decimal precision (default `(18, 4)`)
- UTC dates need no configuration here: the UTC date convention is auto-wired by `UseEntities(e => e.UseDefaults())` (see P3), so all `DateTime` values round-trip as UTC and JSON gets the ISO 8601 `Z` suffix. (Standalone EF without the entities stack: `configurationBuilder.SetUtcDateTimeConvention()` in `ConfigureConventions`, or `.AddUtcDateTimeConvention()` in `AddDbContext`.)
- Soft delete needs no configuration here either: the archived query filter (`e => !e.IsArchived` on every `IArchivable` entity) is auto-wired by `UseEntities<TContext>(e => e.UseDefaults())` (see P3) and applied after everything `OnModelCreating` configured. ⚠️ A `DbContext` you construct yourself — `new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()…Options)` in tests, a design-time factory, a seeding tool — bypasses that wiring: add `.AddArchivedQueryFilter()` to those options, or call `modelBuilder.SetArchivedQueryFilter()` at the end of `OnModelCreating` (after your own `HasQueryFilter(...)` calls, exactly once). Startup validation errors out on a model that ends up without it. Round-trip: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete.
- **Consequence (`net10.0` only, where a real query filter is installed):** EF then logs one `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` per relationship whose required principal is `IArchivable`. ⚠️ **Check what the principal is before suppressing it.** For an aggregate parent (`Order` → its lines) it is the intent, and it is the one startup warning the golden path's "no warnings" checkpoint excuses. For **reference data** — a category/status/type that separately-registered entities point at through a required FK — it is a silent data bug: the filter propagates into `Include(...)` as an inner join, so those rows vanish from `items` while `/search` still counts them. Such an entity should not be `IArchivable` at all (real `DELETE` + `OnDelete(Restrict)` → 409), or its FK should be optional; startup validation warns on the shape. See [`entities.patterns.md`](./entities.patterns.md) → Soft Delete. Once confirmed benign, ignore it on the **`AddDbContext` options builder** (P3 below, not here in `OnModelCreating`) with `using Microsoft.EntityFrameworkCore.Diagnostics;` + `.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))`, and add a matching `HasQueryFilter(x => !x.Parent!.IsArchived)` for any dependent you query *directly* rather than through its parent.

```csharp
using Regira.DAL.EFcore.Extensions;      // SetDecimalPrecisionConvention

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // public DbSet<Product> Products => Set<Product>();   // add per entity

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.SetDecimalPrecisionConvention(); // global decimal precision, default (18, 4)
        // configure relationships per entity here
    }
}
```

---

## P3: Program.cs

**Changes to BasicApi**
```csharp no-compile
// ... usings from BasicApi
using Microsoft.EntityFrameworkCore;

// JSON contract: ignore reference cycles + nulls, serialize enums as names. ConfigureDefaultJsonOptions
// applies all three to BOTH the MVC options and Http.Json.JsonOptions — the set AddOpenApi() and
// minimal-API results read — so the generated schema matches the wire format.
builder.Services.AddControllers();
builder.Services.ConfigureDefaultJsonOptions();   // Regira.Entities.Web.DependencyInjection

// add DbContext — only the provider is needed here: UseEntities(e => e.UseDefaults()) (inside
// AddEntityServices) auto-wires the primer/normalizer/auto-truncate interceptors and the UTC date
// convention (DateTime values round-trip as UTC → Z suffix in JSON) into these options
builder.Services.AddDbContext<AppDbContext>(options =>
    // use DB provider at wish
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// add entity services (repositories) and configurations
builder.Services.AddEntityServices();

// ...
// build app and configure as in BasicApi
```

> **⚠️ SQLite ignores foreign keys unless the connection string enables them.** `Microsoft.Data.Sqlite` leaves
> `PRAGMA foreign_keys` **off**, so `DeleteBehavior.Restrict` never fires, cascades don't run, and orphan rows
> are accepted — no error, no 409. Enable it per connection:
> ```json
> "ConnectionStrings": { "Default": "Data Source=app.db;Foreign Keys=True" }
> ```

> **⚠️ Why the one-liner, not `AddJsonOptions`.** All three settings are required once entities have related
> entities or enums: without `ReferenceHandler.IgnoreCycles` a cyclic graph (`Order → Customer → Orders`)
> throws a `JsonException` at request time; without `JsonStringEnumConverter` enums serialize as `0/1/2`
> instead of names. Configuring only `AddControllers().AddJsonOptions(...)` fixes the controller wire format
> but **not** `Http.Json.JsonOptions` — the set `AddOpenApi()` and minimal-API results (`Results.Ok(...)`)
> read — so the generated schema types enums as integers while the API sends names, a mismatch nothing
> reports that reaches the SPA as wrong types. `ConfigureDefaultJsonOptions()` applies all three to both; its
> `configure` / `configureHttp` callbacks target the two sets separately (a converter added to only one
> re-creates the mismatch).

> **DbContext wiring is automatic.** `UseEntities<TContext>(e => e.UseDefaults())` contributes the
> primer/normalizer/auto-truncate interceptors, the UTC date convention and the archived query filter to the
> context's options itself
> (via EF's `IDbContextOptionsConfiguration<TContext>`), regardless of `AddDbContext` ↔ `UseEntities()` call
> order — `AddDbContext<AppDbContext>(options => …)` only needs the provider. The match is by assignability,
> so `UseEntities<AppContextBase>()` (abstract base) also wires derived provider-specific contexts
> (`AddDbContext<SqlServerAppContext>()`). Control the wiring with
> `e.WireDbContext(...)` (`DbContextWiring` flags): `DbContextWiring.None` opts out entirely; without
> `UseDefaults()` pick pieces à la carte, e.g.
> `e.WireDbContext(DbContextWiring.PrimerInterceptors | DbContextWiring.UtcDateTimeConvention)`.
> ⚠️ It reaches contexts EF builds from the service collection. A `DbContext` you construct yourself
> (`new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()…Options)`) gets none of it — add what that
> context needs to its own options builder (`.AddArchivedQueryFilter()`, `.AddUtcDateTimeConvention()`, …).

> **ValidateOnBuild:** turn it on — a `.For<>()` with a wrong generic argument, or one never added at all, then throws when the host builds instead of on some later request.
>
> ```csharp
> builder.Host.UseDefaultServiceProvider(o =>
> {
>     o.ValidateOnBuild = true;
>     o.ValidateScopes = true;
> });
> ```
>
> ⚠️ It only sees **constructor-injected** dependencies. `EntityControllerBase<>` resolves its `IEntityService<>` from `HttpContext.RequestServices`, so a controller ↔ `.For<>()` arity mismatch is invisible to it — that one is caught by Regira's own startup validation (§Startup validation in [`entities.instructions.md`](./entities.instructions.md)) and by the error message in `ControllerExtensions.GetRequiredEntityService`.

> **OpenAPI/UI note:** If the shared project guide is not available locally yet, keep the API surface aligned with the Regira baseline here as well: use `app.MapOpenApi()` plus `app.MapScalarApiReference()` and do not add `Swashbuckle.AspNetCore` or `UseSwaggerUI()`.

> **SQLite starter note:** For the default SQLite starter/test setup, do not scaffold an initial EF migration. After `app = builder.Build()`, create a scope and call `Database.EnsureCreated()` instead:

```csharp no-compile
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    // seed here, in the same scope — see entities.instructions §Seeding via IEntityService
}
```

> ⚠️ **This block, and any seeding, must sit between `builder.Build()` and `app.Run()`.** `app.Run()` blocks
> until shutdown, so anything written after it never executes and the app simply starts up empty — no
> exception, no log line.

> **⚠️ Delete the `.db` file after any model change — `EnsureCreated()` does not migrate.** It creates the
> schema only when the `.db` is absent, so any model change after the first run stays invisible — a new
> table/column simply won't exist — until you delete the `.db` file and re-run (it re-creates and re-seeds).
> Treat the SQLite database as disposable; adopt explicit migrations only once the schema stabilizes.

> **Don't judge seeding by the `.db` file size — query through the app.** SQLite's default is rollback journaling, so committed rows land directly in the `.db` (only a transient `<db>.db-journal` appears mid-transaction). Under WAL (`<db>.db-wal` present — treat it as expected, not as something you must have configured), freshly-committed rows instead sit in the WAL file until a checkpoint, so the `.db` can look empty (a few KB) while the data is really there.

> **Quiet SQL logging:** EF Core logs every command at `Information`. Set `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` to `Warning` in `appsettings.json`. Code-configured Serilog ignores that section unless you call `ReadFrom.Configuration(...)`; otherwise add `.MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)`.

### API route prefix

Keep controller routes resource-relative — `[Route("[controller]")]` (or the resource name, e.g. `[Route("products")]`) — and apply a shared `api` base **once** at the host/app level so it stays configurable. Either host under that path (IIS virtual directory / reverse proxy, or `app.UsePathBase("/api")`), or register a global route-prefix convention. `Regira.Entities.Web` already brings `Regira.Web` transitively, so `options.UseCentralRoutePrefix(new RouteAttribute("api"))` (`Regira.Web.Routing`) is available without another reference; the self-contained equivalent below is here for a host that does not reference it:

```csharp no-compile
// namespace Microsoft.AspNetCore.Mvc.ApplicationModels (RouteAttribute is in Microsoft.AspNetCore.Mvc)
public sealed class RoutePrefixConvention(string prefix) : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix = new(new RouteAttribute(prefix));
    public void Apply(ApplicationModel app)
    {
        foreach (var controller in app.Controllers)
            foreach (var selector in controller.Selectors)
                selector.AttributeRouteModel = selector.AttributeRouteModel is { } existing
                    ? AttributeRouteModel.CombineAttributeRouteModel(_prefix, existing)
                    : _prefix;
    }
}
// Program.cs — the prefix can come from configuration
builder.Services.AddControllers(o => o.Conventions.Add(new RoutePrefixConvention("api")));
```

Spell a multi-word resource in **kebab-case plural**: `InterventionType` → `[Route("intervention-types")]`, `FacetGroup` → `[Route("facet-groups")]`. The SPA's `IConfig.api` must match it character-for-character, so a flattened `interventiontypes` or a camelCase `interventionTypes` costs a 404 on every call to that entity.

> **Building the paired SPA?** This prefix is one of four settings that must line up — SPA axios base,
> entity `IConfig.api`, the Vite dev proxy, and this route prefix. The front-end guide resolves them as one
> contract: `regira_modules.vue.entities` → `entities.setup` → *The URL contract — four owners, one request*.

### Local development vs production (HTTPS + a dev SPA)

`app.UseHttpsRedirection()` 308-redirects every HTTP request to HTTPS. That is correct in production, but it
breaks a browser SPA dev server (Vite, etc.) that proxies API calls over plain HTTP — the preflight/redirect
chain fails and requests never reach the API. Two safe options:

- **Proxy to the HTTPS origin** from the SPA dev server and disable cert verification in dev. In `vite.config.ts`:
  ```ts
  server: { proxy: { '/api': { target: 'https://localhost:7xxx', changeOrigin: true, secure: false } } }
  ```
- **Or skip the redirect in Development** so the SPA can talk to the API over HTTP:
  ```csharp no-compile
  if (!app.Environment.IsDevelopment())
  {
      app.UseHttpsRedirection();
  }
  ```

**CORS for a dev SPA on a different origin.** When the SPA is served from its own dev origin (e.g.
`http://localhost:5173`) rather than proxied, the browser needs CORS. This is a general web concern the
consumer owns — the front-end guide wires it as part of the dev SPA setup (`regira_modules.vue.entities` →
`entities.setup`). Two constraints an agent must not get wrong when writing the policy: use
`SetIsOriginAllowed(...)` (e.g. accepting any loopback origin in dev), **not** `AllowAnyOrigin()` — the latter
throws at runtime when combined with `AllowCredentials()`; and place `app.UseCors(...)` **before**
`UseAuthorization()`/`MapControllers()`.

---

## P4: Create the DI Extension Method

Create `Extensions/ServiceCollectionExtensions.cs`. The complete wiring pattern is below; for a real
`Add{Entities}()` body see [`entities.examples.md`](./entities.examples.md) — **Order + OrderLine entities**
(`OrderServiceConfiguration.cs`).

- Call `services.UseRegira(configuration)` **before** `UseEntities()` to apply paid license keys — omit for the free tier. See [§License requirement](./entities.instructions.md#license-requirement).
- Call `options.UseDefaults()` to register primers, global query filters, and normalizer services
- Call `options.UseMapsterMapping()` (default) or `options.UseAutoMapper()` for DTO mapping
- *(web apps with attachments)* Call `options.UseAttachmentUris()` (namespace `Regira.Entities.Web.Attachments.DependencyInjection`, package `Regira.Entities.Web`) and register `AddHttpContextAccessor()` so attachment DTOs get a resolved `Uri`. `Regira.Entities.DependencyInjection` does not reference ASP.NET Core, so this resolution is opt-in; without it the `Uri` is `null` (not an error). The `Uri` links to the `GetFile` action on the attachment entity's controller, so keep the generated `EntityAttachmentControllerBase<…>` endpoints mapped — a custom/replacement download route is not auto-discovered (the `Uri` stays `null`; use the download endpoint directly).
- *(optional)* Set `options.DefaultPageSize` / `options.MaxPageSize` to cap what List/Search controller endpoints return by default — see [Paging defaults](./entities.instructions.md#paging-defaults)
- Create one `Add{EntityNameInPlural}()` extension method per entity — take `this IEntityServiceCollection<TContext>` as the parameter and **return `EntityServiceCollection<TContext>`** (the concrete type every `For<>()` returns). This keeps chains composable and assignable to `IServiceCollection` without extra unwrapping.

**Complete wiring pattern — callback vs. return value:**

```csharp no-compile
// Program.cs — register license once before any module setup
services.UseRegira(configuration);

// Extensions/ServiceCollectionExtensions.cs
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Mapping.Mapster;

public static IServiceCollection AddEntityServices(this IServiceCollection services, IConfiguration configuration)
    => services
        .UseEntities<AppDbContext>(options =>
        {
            // ↑ options is EntityServiceCollectionOptions — global settings only
            // License is resolved from the UseRegira() registration — no license key here
            options.UseDefaults();
            options.UseMapsterMapping();
        })
        // ↑ UseEntities returns EntityServiceCollection<AppDbContext>
        // Chain per-entity registrations
        .AddProducts()
        .AddCategories();
```

> **First-build usings quick-reference** — the most commonly missed extension-method namespaces:
>
> | `using` | Enables |
> |---|---|
> | `Regira.Licensing.DependencyInjection` | `services.UseRegira(configuration)` in `Program.cs` (comes transitively via `Regira.Entities.DependencyInjection`) |
> | `Regira.Entities.Mapping.Mapster` | `options.UseMapsterMapping()` |
> | `Regira.Entities.DependencyInjection.ServiceCollections.Models` | `DbContextWiring` flags for `e.WireDbContext(...)` *(à-la-carte wiring without `UseDefaults()`)* |
> | `Regira.DAL.EFcore.Services` | `options.AddAutoTruncateInterceptors()` in `AddDbContext` *(standalone EF — auto-wired by `UseDefaults()`)* |
> | `Microsoft.EntityFrameworkCore` | `.Include(...)` / `.ThenInclude(...)` inside an `e.Includes((q, _) => ...)` registration — plain EF Core, needed in the service-config file |
>
> These are all extension methods — the compiler error "does not contain a definition for X" means the `using` is missing, not that the method doesn't exist.
