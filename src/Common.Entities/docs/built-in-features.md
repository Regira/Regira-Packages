# Ready to use Features

## Entity Services

### Wrapping service

**EntityWrappingServiceBase**: *Create a pipeline of services that wrap around an inner service.
Different responsibilities can be implemented in separate services.*

Samples:
- Auditing (Can also be done using Primers for write operations)
- Security
- Caching
- Validation

```csharp
.For<Order>(e =>
{
    // define custom EntityService interface
    e.AddTransient<IOrderService, OrderService>(); // optional: if you want to inject the service by custom interface
    e.UseEntityService<OrderService>();
})
```

Possible overrides:
```csharp
// Read
Task<TEntity?> Details(TKey id, CancellationToken token = default)
Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes, PagingInfo? pagingInfo, CancellationToken token = default)
Task<long> Count(TSearchObject? so, CancellationToken token = default)
Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default)

// Write
Task Save(TEntity item, CancellationToken token = default)
Task Add(TEntity item, CancellationToken token = default)
Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
Task Remove(TEntity item, CancellationToken token = default)
Task<int> SaveChanges(CancellationToken token = default)
```


### Input Exceptions

**EntityInputException**: Caught by Controllers and returned as BadRequest (400).

```csharp
public class EntityInputException<T>(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public T? Item { get; set; }
    public IDictionary<string, string> InputErrors { get; set; } = new Dictionary<string, string>();
}
```


### Constraint Exceptions

**EntityConstraintException**: thrown by the EFcore write services when `SaveChanges()` fails on a database
**integrity-constraint** violation — unique index, foreign key, NOT NULL, check. Detection is per provider
(SQLSTATE class 23, SQLite error 19, SQL Server 547/515/2601/2627 — `DbUpdateException.IsConstraintViolation()`
in `Regira.Entities.EFcore.Extensions`); transient faults (deadlocks, timeouts, concurrency conflicts) are
**not** wrapped and keep throwing `DbUpdateException` subtypes.

```csharp
public class EntityConstraintException(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    // generic detail returned to clients — provider messages can leak index names and other users' values
    public const string ClientMessage = "A database constraint rejected the change.";
}
```

- **Every web write surface returns 409 Conflict** with `ClientMessage` as the `ProblemDetails` detail:
  the controller helpers (`ControllerExtensions.Save`/`Delete`), the `[EntityConstraintConflict]` exception
  filter (attachment controller bases), the FastEndpoints minimal-API group filter, and the FastEndpoints
  `Endpoint<>` bases. The provider's constraint message is logged server-side (warning) by the write service.
- **Direct `SaveChanges()` callers** (seeding, jobs, custom services): catch `EntityConstraintException`,
  not `DbUpdateException` — a `catch (DbUpdateException)` no longer sees constraint failures, only
  transient faults. `Message` is the same generic text (safe to render anywhere); the provider message is
  on `InnerException` and in the write service's warning log.
- The response is deliberately generic — throw `EntityInputException` from a prepper when the client
  should receive a field-level 400 instead.


## DbContext

**SetDecimalPrecisionConvention**: *Automatically configures decimal properties.*

```csharp
using Regira.DAL.EFcore.Extensions; // external namespace

// In DbContext class
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.SetDecimalPrecisionConvention(18, 2);
}
```

**AddArchivedQueryFilter / SetArchivedQueryFilter**: *Applies `e => !e.IsArchived` as a named EF Core query
filter to every entity type implementing `IArchivable`, which is what makes a soft-deleted row disappear from
reads — including inside `Include(...)`.*

*With `UseEntities<TContext>(e => e.UseDefaults())` the filter is wired into the context's options
automatically (`DbContextWiring.ArchivedQueryFilter`) and applied at model finalization, so a `DbContext`
registered through `AddDbContext` needs no soft-delete configuration of its own. The two forms below are for a
context built outside that wiring.*

```csharp
using Regira.Entities.EFcore.Extensions;

// a DbContext constructed by hand — tests, a design-time factory, a seeding tool
new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .AddArchivedQueryFilter()
    .Options);
```

```csharp
using Regira.Entities.EFcore.Extensions;

// or from the model, for a setup that opted out of DbContextWiring.ArchivedQueryFilter
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // model configuration, including your own HasQueryFilter calls, goes above
    modelBuilder.SetArchivedQueryFilter();
}
```

A model that ends up with no archived filter returns archived rows everywhere, and startup validation raises
an error naming the entity. See [Soft delete](#soft-delete) for the ordering rules and the read-side opt-ins.

**AddUtcDateTimeConvention / SetUtcDateTimeConvention**: *Rounds all `DateTime` properties through the
database as UTC: normalized to UTC on save, materialized with `DateTimeKind.Utc` on read (→ `Z` suffix in
JSON). Follows the process-wide `DateTimeDefaults.UseUtc` policy (inert when disabled); a property with its
own value converter is left alone, which doubles as the per-property opt-out. Use one of the two forms:*

*(With `UseEntities(e => e.UseDefaults())` the convention is wired automatically — the forms below are for
standalone EF usage without the entities stack.)*

```csharp
using Regira.DAL.EFcore.Extensions; // external namespace

// In AddDbContext
services.AddDbContext<MyDbContext>(db =>
{
    db.UseSqlServer(connectionString)
        .AddUtcDateTimeConvention();
});

// Or in the DbContext class
protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
{
    configurationBuilder.SetUtcDateTimeConvention();
}
```

**AddAutoTruncateInterceptors**: *Truncates string properties based on MaxLength attribute before saving to database.
Adds a global `AutoTruncatePrimer` as interceptor*.

```csharp
using Regira.DAL.EFcore.Services; // external namespace

services.AddDbContext<MyDbContext>(db =>
{
    db.UseSqlServer(connectionString)
        .AddAutoTruncateInterceptors();
});
```

## Helper Services


### Defaults

```csharp
using Regira.Entities.DependencyInjection.Extensions;

services.UseEntities<ContosoContext>(e => e.UseDefaults());
```

Registers a set of commonly used features for typical applications, including:
- `AddDefaultInterceptors()` — **automatic DbContext wiring**: `UseEntities<TContext>()` contributes the
  primer/normalizer/auto-truncate interceptors, the UTC date convention and the archived query filter to the
  context's options, so `AddDbContext` only needs the provider and the `DbContext` itself stays free of
  framework calls. Matches by assignability (an abstract-base registration also wires derived
  provider-specific contexts), in any registration order. Fine-grained control via
  `WireDbContext(DbContextWiring …)`: `None` opts out; without `UseDefaults()` pick pieces à la carte
  (e.g. `DbContextWiring.PrimerInterceptors`). A `DbContext` constructed outside the service collection is
  not covered — configure such a context's options builder directly
- `AddDefaultPrimers()`
  - `ArchivablePrimer`
  - `HasCreatedDbPrimer`
  - `HasLastModifiedDbPrimer`
- `AddDefaultGlobalQueryFilters()`
  - `FilterIdsQueryBuilder`
  - `FilterArchivablesQueryBuilder`
  - `FilterHasCreatedQueryBuilder`
  - `FilterHasLastModifiedQueryBuilder`
- `AddDefaultEntityNormalizer()`
  - `DefaultNormalizer`
  - `ObjectNormalizer`
  - `DefaultEntityNormalizer`
  - `QKeywordHelper`


### Read behavior

```csharp
services.UseEntities<AppDbContext>(o =>
{
    o.RefetchAfterSave = RefetchAfterSave.WhenProcessorsRegistered; // save endpoints re-fetch only when needed
})
.For<Product>(e => e.SetReadBehavior(RefetchAfterSave.Never)); // per entity — fully replaces the global options
```

- `Details(id)` always eager-loads every registered include (the OR of all `TIncludes` flags); gate each
  navigation behind its flag so `List`/`Search` stay lean while `Details` still loads the full graph.
- **`RefetchAfterSave`** (`Always` default / `WhenProcessorsRegistered` / `Never`) — whether the web save
  endpoints re-fetch the saved entity via `Details(id)` for the response.
- **`SetReadBehavior(...)`** — the per-entity override; when registered it fully replaces the global
  options for that entity.

### Startup validation

`UseEntities()` registers a hosted service that validates the entity registrations at host start
(Development environment by default). It fails fast — with actionable messages — on controller ↔ `For<>()`
generic-arity mismatches (the controller check activates automatically when `Regira.Entities.Web` is
referenced, or explicitly via `ValidateEntityControllers()`), warns when primers/normalizers are registered
without their SaveChanges interceptor (an informational note instead when the `RegisterPrimerContainer` +
`ApplyPrimers()` pattern is detected), and warns when `?q=` would be silently ignored for an entity.
Configure via `UseEntities(o => o.ConfigureValidation(v => { v.Enabled = true; /* Production opt-in */ }))`.


### Preppers

| Prepper | Description |
|---------|-------------|
| **RelatedCollectionPrepper** | *Prepares related collections for saving by adding, updating, or removing items as necessary.* |

```csharp
// use shortcut when configuring Entity (creates RelatedCollectionPrepper in background)
.For<Order>(e => {
    e.Related(x => x.OrderItems, (item, _) => item.OrderItems?.Prepare());
});
```

### Primers

**ArchivablePrimer**:
*Sets IsArchived to true instead of deleting entities implementing `IArchivable`.
To be combined with the archived query filter and `FilterArchivablesQueryBuilder` — see
[Soft delete](#soft-delete).*

| Primer                      | Description |
|-----------------------------|-------------|
| **HasCreatedDbPrimer**      | *Sets Created timestamp (UTC) on new entities implementing `IHasCreated`. Client-supplied values are normalized to UTC.* |
| **HasLastModifiedDbPrimer** | *Sets LastModified timestamp (UTC) when updating entities implementing `IHasLastModified`.* |

#### UTC timestamps

By default all timestamps are handled as UTC: primers write `DateTime.UtcNow` and normalize client-supplied
values (local kinds are converted, unspecified kinds are assumed UTC). `UseEntities(e => e.UseDefaults())`
also wires the UTC date convention into the DbContext options, so `DateTime` values read from the database
materialize with `DateTimeKind.Utc` and JSON responses carry the ISO 8601 `Z` suffix. (Standalone EF usage
can apply the same convention via `AddUtcDateTimeConvention()` / `SetUtcDateTimeConvention()` — see
[DbContext](#dbcontext) above.)

UTC handling is one policy per process (`Regira.Utilities.DateTimeDefaults.UseUtc`, on by default):

```csharp
services.UseEntities<AppDbContext>(e => e.UseDefaults()); // UTC (default)
services.UseEntities<AppDbContext>(e => e.UseDefaults().UseUtc(false)); // local time; values used as given
```

When disabled, timestamps use `DateTime.Now` and filters compare dates as given. The UTC convention's
converter follows the same policy — it passes values through unchanged when UTC is disabled, so a wired
convention cannot shift locally-written values.


### Query Builders

| Query Builder                              | Description |
|--------------------------------------------|-------------|
| **FilterIdsQueryBuilder**                  | *Filters entities based on a collection of IDs.* |
| **FilterArchivablesQueryBuilder**          | *Translates `ISearchObject.Archived` (or `DefaultArchivedFilter` when it is `null`). Only the opt-ins compose anything: hiding archived rows is done by the archived EF query filter — see Soft delete below.* |
| **FilterHasCreatedQueryBuilder**           | *Filters entities based on Created timestamp range (input normalized to UTC).* |
| **FilterHasLastModifiedQueryBuilder**      | *Filters entities based on LastModified timestamp range (input normalized to UTC).* |
| **FilterHasNormalizedContentQueryBuilder** | *Filters entities based on normalized content keywords (input: `ISearchObject.Q`).* |

#### Soft delete

Soft delete needs one thing from the application: the entity implements `IArchivable`.
`UseEntities<TContext>(e => e.UseDefaults())` supplies the rest — `ArchivablePrimer`,
`FilterArchivablesQueryBuilder`, and the archived EF query filter itself, wired into the context's options
(`DbContextWiring.ArchivedQueryFilter`). The query filter is what hides archived rows — on lists, on
`Details(id)`, and inside `Include(...)`. A model that ends up without it still flags rows on `DELETE` while
nothing hides them; startup validation reports that as an error naming the entity.

A `DbContext` constructed outside the service collection — `new AppDbContext(options)` in tests, a design-time
factory, a seeding tool — is not covered by that wiring and needs `.AddArchivedQueryFilter()` on its own
options builder (see [DbContext](#dbcontext)).

Reads opt back in through `ISearchObject.Archived` (`ArchivedFilter?`, bound from `?archived=`):

| `Archived` | Rows returned |
|------------|---------------|
| `Excluded` | non-archived only (composes nothing — the query filter already excludes them) |
| `Included` | archived and non-archived alike |
| `Only`     | archived only |
| `null`     | falls back to `DefaultArchivedFilter` on `UseEntities()` (default `Excluded`) |

The archived filter is a named query filter (`"Regira:Archived"`), and EF Core 10 rejects a model that mixes
anonymous and named filters — so an anonymous filter the application configured on the same `IArchivable`
entity is re-registered under `"Regira:Model"`. It keeps applying unchanged. The wired convention runs at
model finalization, after everything `OnModelCreating` configured, so ordering takes care of itself; the
explicit `SetArchivedQueryFilter()` must be called **after** any `HasQueryFilter(...)` of your own, and only
once — calling it first leaves the later anonymous filter beside the named one and the model fails to build.
On `net10.0` the two opt-ins suspend the archived filter by name and nothing else: a query filter the
application configured itself keeps applying, as does every `IGlobalFilteredQueryBuilder`.

On `net8.0` (EF Core 9) there are no named query filters, so **no archived query filter is installed** — by
either route. Honouring the opt-ins would take the untargeted `IgnoreQueryFilters()`, which also suspends every
query filter the application defined — and because the write path resolves its row archived-inclusive on every
update, row security expressed as a `HasQueryFilter` would become a cross-tenant read *and* write. Archived
rows are excluded by `FilterArchivablesQueryBuilder` at the root of the query instead: soft delete works and
nothing is ever suspended, but archived rows are not filtered out of an `Include(...)`d collection. Row
scoping written as an
`IGlobalFilteredQueryBuilder` is a plain `Where` in the query pipeline rather than an EF query filter, so it
keeps applying on both target frameworks.

### Query Extensions

```csharp
public static class QueryExtensions
{
    public static IQueryable<TEntity> FilterId<TEntity, TKey>(this IQueryable<TEntity> query, TKey? id)
    public static IQueryable<TEntity> FilterIds<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids)
    public static IQueryable<TEntity> FilterExclude<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids)

    public static IQueryable<TEntity> FilterCode<TEntity>(this IQueryable<TEntity> query, string? code)

    public static IQueryable<TEntity> FilterTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)
    public static IQueryable<TEntity> FilterNormalizedTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)

    public static IQueryable<TEntity> FilterCreated<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate)
    public static IQueryable<TEntity> FilterLastModified<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate)
    public static IQueryable<TEntity> FilterTimestamps<TEntity>(this IQueryable<TEntity> query, DateTime? minCreated, DateTime? maxCreated, DateTime? minModified, DateTime? maxModified)

    public static IQueryable<TEntity> FilterQ<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords)
    public static IQueryable<TEntity> FilterArchivable<TEntity>(this IQueryable<TEntity> query, ArchivedFilter archived)

    public static IQueryable<TEntity> FilterHasAttachment<TEntity>(this IQueryable<TEntity> query, bool? hasAttachment)

    public static IQueryable<TEntity> SortQuery<TEntity, TKey>(this IQueryable<TEntity> query)
}
```


### Pagination

```csharp
using Regira.DAL.Paging; // external namespace

public static class QueryExtensions
{
    public static IQueryable<T> PageQuery<T>(this IQueryable<T> query, PagingInfo? info)
    public static IQueryable<T> PageQuery<T>(this IQueryable<T> query, int pageSize, int page = 1)
}
```

**Default & maximum page size** — configure these so List/Search endpoints page automatically instead of returning the full set. Enforced at the HTTP boundary only (MVC controllers and FastEndpoints alike); direct `IEntityService` calls keep full control.

```csharp
// Global (all entities)
services.UseEntities<AppDbContext>(options =>
{
    options.UseDefaults();
    // make sure to put this after UseDefaults()
    options.DefaultPageSize = 50;   // applied when the request omits pageSize (null = off)
    options.MaxPageSize = 200;      // clamp larger requested pageSize values (null = no limit)
    // or
    options.SetPageSize(pageSize: 50, maxPageSize: 200);
});

// Per-entity override (fully replaces the global values for that entity)
services.For<Product>(e => e.SetPageSize(defaultPageSize: 25, maxPageSize: 100));
services.For<AuditLog>(e => e.SetPageSize()); // opt out — never force-paged
```

> See [Web Endpoints → Paging](web-endpoints.md#paging) for the full behaviour.

## Entity Extensions

```csharp
public static class EntityExtensions
{
    public static bool IsNew<TKey>(this IEntity<TKey> item)
    public static void SetSortOrder(this IEnumerable<ISortable> items)
}
```

## Entity Interfaces

| Interface | Properties | Services | Info |
|-----------|-----------|-------------|-------------|
| `IEntity<TKey>` | Id (TKey) | `FilterIdsQueryBuilder` | Define type of Primary Key |
| `IEntityWithSerial` | Id (int) | *see `IEntity<int>`* | Auto-incrementing int ID |
| `IHasCode` | Code (string) | *`Normalizers`* | Entities with short code |
| `IHasTitle` | Title (string) | *`Normalizers`* | Name, title, short description to display |
| `IHasDescription` | Description (string) | *`Normalizers`* | Entities with description field |
| `IHasCreated` | Created (DateTime) | `HasCreatedDbPrimer` | Track creation time (UTC) |
| `IHasLastModified` | LastModified (DateTime?) | `HasLastModifiedDbPrimer` | Track modification time (UTC) |
| `IHasTimestamps` | Created, LastModified | see `IHasCreated` & `IHasLastModified` | Both timestamps |
| `IArchivable` | IsArchived (bool) | `ArchivablePrimer`, `FilterArchivablesQueryBuilder`, archived query filter | Soft delete capability |
| `ISortable` | SortOrder (int) | *`Preppers`* -> `EntityExtensions.SetSortOrder` | Sortable as (child) collection |
| `IHasObjectId` | ObjectId (TKey) | *`Attachments`* | FK to owning entity |

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. **[Built-in Features](built-in-features.md)** — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
