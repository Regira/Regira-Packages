# Regira Entities Framework - API Signatures Reference

Exact signatures for interfaces, classes, and extension methods in the Regira Entities framework. **Do not guess — look up here first.**

---

## Table of Contents

1. [Entity Interfaces](#entity-interfaces)
2. [Service Interfaces](#service-interfaces)
3. [Controller Base Classes](#controller-base-classes)
4. [Search and Filter Objects](#search-and-filter-objects)
5. [Extension Methods](#extension-methods)
6. [Service Builders](#service-builders)
7. [Mapping and Processing](#mapping-and-processing)
8. [Response Types](#response-types)
9. [Attachments](#attachments)
10. [Exceptions](#exceptions)
11. [Supporting Types](#supporting-types)

---

## Entity Interfaces

### Core Entity Interfaces

```csharp
using Regira.Entities.Models.Abstractions;

public interface IEntity;
public interface IEntity<TKey> : IEntity
{
    TKey Id { get; set; }
}

// Shortcut for int primary key
public interface IEntityWithSerial : IEntity<int>;
```

### Property Interfaces

```csharp
using Regira.Entities.Models.Abstractions;

public interface IHasCode
{
    string? Code { get; set; }
}
// Title is read-only on the interface; entity classes may expose a setter
public interface IHasTitle
{
    string? Title { get; }
}
public interface IHasNormalizedTitle : IHasTitle
{
    string? NormalizedTitle { get; set; }
}
public interface IHasDescription
{
    string? Description { get; set; }
}
public interface IHasNormalizedContent
{
    string? NormalizedContent { get; set; }
}
public interface IHasLastNormalized : IHasNormalizedContent, IHasLastModified
{
    DateTime? LastNormalized { get; set; }
}
public interface ISortable
{
    int SortOrder { get; set; }
}
```

### Timestamp Interfaces

Timestamps are UTC: the default primers write `DateTime.UtcNow` and normalize client-supplied values.

```csharp
using Regira.Entities.Models.Abstractions;

public interface IHasCreated
{
    DateTime Created { get; set; } // UTC
}
public interface IHasLastModified
{
    DateTime? LastModified { get; set; } // UTC
}

// Combined — most common choice
public interface IHasTimestamps : IHasCreated, IHasLastModified;
```

### Lifecycle Interface

```csharp
using Regira.Entities.Models.Abstractions;

public interface IArchivable
{
    bool IsArchived { get; set; }
}
```

### Period / Slug / Uri Interfaces

```csharp
using Regira.Entities.Models.Abstractions;

// ⚠️ members are nullable — implementing them with non-nullable DateTime fails with CS0738.
// UTC instants by default; prefer a DateOnly property of your own for pure calendar semantics.
public interface IHasStartDate { DateTime? StartDate { get; set; } }
public interface IHasEndDate   { DateTime? EndDate { get; set; } }
public interface IHasStartEndDate : IHasStartDate, IHasEndDate;

public interface IHasSlug { string? Slug { get; set; } }
public interface IHasUri  { string? Uri { get; set; } }
```

### Attachment Interfaces

```csharp
using Regira.Entities.Attachments.Abstractions;

public interface IHasAttachments
{
    ICollection<IEntityAttachment>? Attachments { get; set; }
    bool? HasAttachment { get; set; } // ⚠️ yours to set (a primer/prepper or a mapped projection) — nothing populates it, so it serializes null even for a row that has attachments. `FilterHasAttachment(so.HasAttachment)` does not read it either: it filters on Attachments.Any(). To show a "has documents" flag without loading the collection, set it; otherwise drop it from the DTO.
}

// Typed (int keys) — most common
public interface IHasAttachments<TEntityAttachment>
    : IHasAttachments<TEntityAttachment, int, int, int, Attachment>
    where TEntityAttachment : IEntityAttachment<int, int, int, Attachment>;

public interface IHasAttachments<TEntityAttachment, TKey, TObjectKey, TAttachmentKey, TAttachment>
    where TEntityAttachment : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    ICollection<TEntityAttachment>? Attachments { get; set; }
    bool? HasAttachment { get; set; } // ⚠️ yours to set (a primer/prepper or a mapped projection) — nothing populates it, so it serializes null even for a row that has attachments. `FilterHasAttachment(so.HasAttachment)` does not read it either: it filters on Attachments.Any(). To show a "has documents" flag without loading the collection, set it; otherwise drop it from the DTO.
}

public interface IHasObjectId<TKey>
{
    TKey ObjectId { get; set; }
}
```

---

## Service Interfaces

### Read

```csharp no-compile
using Regira.Entities.Services.Abstractions;

public interface IEntityReadService<TEntity, in TKey>
{
    Task<TEntity?> Details(TKey id, CancellationToken token = default);
    // Explicit archived scope; null falls back to EntityQueryOptions.DefaultArchivedFilter.
    // Only the built-in IArchivable filter reads it — tenant/owner row security still applies.
    // ⚠️ Default interface member (falls back to Details(id, token)) — a custom read service must
    // OVERRIDE BOTH overloads, or the archived-inclusive write path cannot see archived rows and
    // restore silently 404s while everything still compiles.
    Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default);
    // ⚠️ first parameter is object? — a positionally-passed CancellationToken binds as the
    // search object and silently filters nothing; call List(null, null, token)
    Task<IList<TEntity>> List(object? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    Task<long> Count(object? so, CancellationToken token = default);
}

public interface IEntityReadService<TEntity, in TKey, in TSearchObject>
    : IEntityReadService<TEntity, TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    Task<long> Count(TSearchObject? so = null, CancellationToken token = default);
}

public interface IEntityReadService<TEntity, in TKey, TSearchObject, TSortBy, TIncludes>
    : IEntityReadService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    Task<IList<TEntity>> List(
        IList<TSearchObject?> so,
        IList<TSortBy> sortBy,
        TIncludes? includes = null,
        PagingInfo? pagingInfo = null,
        CancellationToken token = default);
    Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default);
}
```

### Write

```csharp no-compile
using Regira.Entities.Services.Abstractions;

public interface IEntityWriteService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    Task Add(TEntity item, CancellationToken token = default);
    Task<TEntity?> Modify(TEntity item, CancellationToken token = default);
    Task Save(TEntity item, CancellationToken token = default);  // Upsert: Add or Modify
    Task Remove(TEntity item, CancellationToken token = default);
    Task<int> SaveChanges(CancellationToken token = default);
}
```

### Combined (IEntityService)

```csharp
using Regira.Entities.Services.Abstractions;

// Full-featured (TKey explicit)
public interface IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    : IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>,
      IEntityService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

// Shortcut — int key assumed (most common for injection)
public interface IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
    : IEntityService<TEntity, int, TSearchObject, TSortBy, TIncludes>,
      IEntityService<TEntity>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;
```

> **Inject** as `IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>` (int key shortcut) when registered with `.For<TEntity, TSearchObject, TSortBy, TIncludes>()`.

### IEntityRepository / IEntityManager

Custom services with `HasRepository<>()` or `HasManager<>()`.

```csharp no-compile
using Regira.Entities.Services.Abstractions;

// Primary shortcut forms (int key, full-featured)
public interface IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>
    : IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>;

public interface IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>
    : IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityManager<TEntity>;

// Additional TKey and partial variants follow the same pattern as IEntityService.
```

### EntityWrappingServiceBase

Inject the inner service via constructor; override only the methods you need.

```csharp no-compile
using Regira.Entities.Services.Abstractions;

// Without sort/includes — exposes Service field
public abstract class EntityWrappingServiceBase<TEntity, TKey, TSearchObject>(
    IEntityService<TEntity, TKey, TSearchObject> service) : IEntityService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    protected readonly IEntityService<TEntity, TKey, TSearchObject> Service = service;

    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default);
    public virtual Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    public virtual Task<long> Count(TSearchObject? so, CancellationToken token = default);
    public virtual Task Add(TEntity item, CancellationToken token = default);
    public virtual Task<TEntity?> Modify(TEntity item, CancellationToken token = default);
    public virtual Task Save(TEntity item, CancellationToken token = default);
    public virtual Task Remove(TEntity item, CancellationToken token = default);
    public virtual Task<int> SaveChanges(CancellationToken token = default);
}

// Full-featured (int key) — most common; empty body, delegates to explicit-TKey variant
public abstract class EntityWrappingServiceBase<TEntity, TSearchObject, TSortBy, TIncludes>(
    IEntityService<TEntity, int, TSearchObject, TSortBy, TIncludes> service)
    : EntityWrappingServiceBase<TEntity, int, TSearchObject, TSortBy, TIncludes>(service)
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

// Full-featured (explicit TKey)
public abstract class EntityWrappingServiceBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes> service)
    : IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    // All IEntityService members are virtual — override as needed:
    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default);
    public virtual Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    public virtual Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    public virtual Task<long> Count(TSearchObject? so, CancellationToken token = default);
    public virtual Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default);
    public virtual Task Add(TEntity item, CancellationToken token = default);
    public virtual Task<TEntity?> Modify(TEntity item, CancellationToken token = default);
    public virtual Task Save(TEntity item, CancellationToken token = default);
    public virtual Task Remove(TEntity item, CancellationToken token = default);
    public virtual Task<int> SaveChanges(CancellationToken token = default);
    public virtual TSearchObject? Convert(object? so);
}
```

> Register the wrapper with `e.UseEntityService<MyService>()` (**⚠️ Beware for circular dependency!**).
> You can register implementations for custom interface derived from `IEntityService` with `e.AddTransient<IMyService, MyService>()`.

---

## Controller Base Classes

The generic type arguments on the controller must **exactly match** those used in `.For<>()`. The controller adds `TDto` and `TInputDto` on top.

| `.For<>()` registration | Required controller base |
|---|---|
| `.For<TEntity>()` | `EntityControllerBase<TEntity, TDto, TInputDto>` |
| `.For<TEntity, TKey>()` | `EntityControllerBase<TEntity, TKey, SearchObject<TKey>, TDto, TInputDto>` |
| `.For<TEntity, TKey, TSearchObject>()` | `EntityControllerBase<TEntity, TKey, TSearchObject, TDto, TInputDto>` |
| `.For<TEntity, TSearchObject, TSortBy, TIncludes>()` | `EntityControllerBase<TEntity, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>` |
| `.For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>()` | `EntityControllerBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>` |

```csharp no-compile
using Regira.Entities.Web.Controllers.Abstractions;

// Minimal — no sorting or includes
[ApiController]
public abstract class EntityControllerBase<TEntity, TKey, TSearchObject, TDto, TInputDto>
    : ControllerBase
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>
    where TDto : class
    where TInputDto : class;

// Full-featured — with sorting and includes (int key shortcut)
[ApiController]
public abstract class EntityControllerBase<TEntity, TSo, TSortBy, TIncludes, TDto, TInputDto>
    : EntityControllerBase<TEntity, int, TSo, TSortBy, TIncludes, TDto, TInputDto>
    where TEntity : class, IEntity<int>
    where TSo : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
    where TDto : class
    where TInputDto : class;

// Full-featured — with explicit TKey
[ApiController]
public abstract class EntityControllerBase<TEntity, TKey, TSo, TSortBy, TIncludes, TDto, TInputDto>
    : ControllerBase
    where TEntity : class, IEntity<TKey>
    where TSo : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
    where TDto : class
    where TInputDto : class;
```

**Endpoints exposed by controller bases:**

| Method | Route | Action | Availability |
|---|---|---|---|
| `GET` | `/{id}` | Details | All |
| `GET` | `/` | List | All |
| `POST` | `/list` | List (body) | **Complex only** |
| `GET` | `/search` | Search (with count) | All |
| `POST` | `/search` | Search (body) | **Complex only** |
| `POST` | `/save` | Save (upsert) | All |
| `POST` | `/` | Create | All |
| `PUT` | `/{id}` | Modify (full update) | All |
| `PATCH` | `/{id}` | Patch (partial update, JSON Merge Patch) | All |
| `DELETE` | `/{id}` | Delete | All |

> **Complex only** = bases with `TSortBy` + `TIncludes`. Simple bases omit `POST /list` and `POST /search`
> (the body/array variants), but do expose `GET /search` (single search object, with count for paging)
> alongside basic list via `GET /?q=…`. For response envelope shapes
> (`item` / `items,count`) see `entities.instructions.md` §Step 13.

---

## Search and Filter Objects

```csharp
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Models;

public interface ISearchObject
{
    string? Q { get; set; }
    DateTime? MinCreated { get; set; }
    DateTime? MaxCreated { get; set; }
    DateTime? MinLastModified { get; set; }
    DateTime? MaxLastModified { get; set; }
    // Bound from ?archived=. null → EntityQueryOptions.DefaultArchivedFilter (Excluded).
    ArchivedFilter? Archived { get; set; }
}

public interface ISearchObject<TKey> : ISearchObject
{
    TKey? Id { get; set; }
    ICollection<TKey>? Ids { get; set; }
    ICollection<TKey>? Exclude { get; set; }
}

// Default implementation (int key) — extend this for custom filters
public record SearchObject : SearchObject<int>;

public record SearchObject<TKey> : ISearchObject<TKey>
{
    public TKey? Id { get; set; }
    public ICollection<TKey>? Ids { get; set; }
    public ICollection<TKey>? Exclude { get; set; }
    public string? Q { get; set; }
    public DateTime? MinCreated { get; set; }
    public DateTime? MaxCreated { get; set; }
    public DateTime? MinLastModified { get; set; }
    public DateTime? MaxLastModified { get; set; }
    public ArchivedFilter? Archived { get; set; }
}
```

---

## Extension Methods

### EntityExtensions

```csharp no-compile
using Regira.Entities.Extensions;

public static class EntityExtensions
{
    public static bool IsNew<TKey>(this IEntity<TKey> item);
    public static void AdjustIdForEfCore(this IEnumerable<IEntity<int>> items);
    public static void SetSortOrder(this IEnumerable<ISortable> items);
}
```

### ModelBuilderExtensions

```csharp no-compile
using Regira.Entities.EFcore.Extensions;

public static class ModelBuilderExtensions
{
    public const string ArchivedQueryFilterName = "Regira:Archived";

    // Applies e => !e.IsArchived as a NAMED filter to every IArchivable root entity type (a hierarchy is
    // filtered through its root, an owned type through its owner) — the only thing that hides archived rows,
    // Include(...) included. Excluded on the search object composes nothing.
    // OPTIONAL: UseEntities<TContext>(e => e.UseDefaults()) installs the same filter through the context's
    // options (DbContextWiring.ArchivedQueryFilter). Call this only when the context is built outside that
    // wiring, or in place of it. Then it goes AFTER your own HasQueryFilter(...) calls, exactly once;
    // calling it alongside the wiring is safe. Startup validation raises an ERROR when a model ends up with
    // no archived filter. (net8.0: no-op — see entities.patterns → Soft Delete.)
    public static void SetArchivedQueryFilter(this ModelBuilder modelBuilder);
}
```

```csharp no-compile
public static class DbContextOptionsBuilderExtensions
{
    // The AddDbContext counterpart of SetArchivedQueryFilter: same named filter, applied at model
    // finalization (so after everything OnModelCreating configured). Auto-added by UseDefaults() —
    // call it yourself for a DbContext constructed outside DI (tests, design-time factory, seeding tool),
    // which no service-collection wiring can reach. (net8.0: no filter is installed.)
    public static DbContextOptionsBuilder AddArchivedQueryFilter(this DbContextOptionsBuilder optionsBuilder);
    public static DbContextOptionsBuilder<TContext> AddArchivedQueryFilter<TContext>(
        this DbContextOptionsBuilder<TContext> optionsBuilder) where TContext : DbContext;
}
```

### DeleteCycleExtensions

```csharp no-compile
using Regira.Entities.EFcore.Extensions;

public static class DeleteCycleExtensions
{
    // Two rows deleted together that reference each other — an entity carrying a foreign key to one of its
    // own children — are a save EF Core refuses with "a circular dependency was detected in the data to be
    // saved". Dropping the reference needs an UPDATE before the DELETEs, so it cannot happen inside one
    // SaveChanges: call these FROM the DbContext's own overrides, BOTH of them, passing base.SaveChanges as
    // the delegate. A save with no such pair calls the delegate exactly once and opens no transaction.
    public static int SaveChangesBreakingDeleteCycles(this DbContext dbContext, Func<int> save);
    public static Task<int> SaveChangesBreakingDeleteCyclesAsync(this DbContext dbContext,
        Func<CancellationToken, Task<int>> save, CancellationToken token = default);
}
```

> Direct pairs only; a longer ring (`A → B → C → A`) is left to EF's own exception. Startup validation warns
> about the shape at registration time. Full recipe: `entities.patterns` → *An entity that references one of
> its own children*.

### QueryExtensions

> Every method

```csharp no-compile
using Regira.Entities.EFcore.Extensions;

public static class QueryExtensions
{
    // Requires IEntity<TKey>
    public static IQueryable<TEntity> FilterId<TEntity, TKey>(this IQueryable<TEntity> query, TKey? id);
    public static IQueryable<TEntity> FilterIds<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids);
    public static IQueryable<TEntity> FilterExclude<TEntity, TKey>(this IQueryable<TEntity> query, ICollection<TKey>? ids);

    // Requires IHasStartEndDate — rows whose period contains `date`; a null bound is treated as open.
    // The only built-in helper for the date interfaces, and you call it yourself (no global filter).
    public static IQueryable<TEntity> FilterIsActiveOn<TEntity>(this IQueryable<TEntity> query, DateTime? date);

    // Requires IHasCode
    public static IQueryable<TEntity> FilterCode<TEntity>(this IQueryable<TEntity> query, string? code);

    // Requires IHasTitle
    public static IQueryable<TEntity> FilterTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords);

    // Requires IHasNormalizedTitle
    public static IQueryable<TEntity> FilterNormalizedTitle<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords);

    // Requires IHasNormalizedContent
    public static IQueryable<TEntity> FilterQ<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords);

    // No constraint — keyword search over explicit fields (each keyword must match at least one).
    // Matches the RAW family (TrimmedQW): the fields you name are ordinary columns holding the client's
    // value verbatim. Do not pass a normalized column here — build that predicate with QW yourself.
    public static IQueryable<TEntity> FilterQ<TEntity>(this IQueryable<TEntity> query, ParsedKeywordCollection? keywords,
        Expression<Func<TEntity, string?>> field, params Expression<Func<TEntity, string?>>[] moreFields);

    // Requires IHasCreated — input dates are normalized to UTC (local kinds converted, unspecified assumed UTC)
    public static IQueryable<TEntity> FilterCreated<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate);

    // Requires IHasLastModified — input dates are normalized to UTC
    public static IQueryable<TEntity> FilterLastModified<TEntity>(this IQueryable<TEntity> query, DateTime? minDate, DateTime? maxDate);

    // Requires IHasTimestamps — input dates are normalized to UTC
    public static IQueryable<TEntity> FilterTimestamps<TEntity>(this IQueryable<TEntity> query,
        DateTime? minCreated, DateTime? maxCreated, DateTime? minModified, DateTime? maxModified);

    // Requires class + IArchivable. net10.0: Excluded composes nothing (the named archived EF query filter
    // already hides archived rows); Included/Only suspend that ONE filter by name, never yours. net8.0: no
    // query filter exists, so Excluded composes the predicate itself and nothing is ever suspended.
    public static IQueryable<TEntity> FilterArchivable<TEntity>(this IQueryable<TEntity> query, ArchivedFilter archived);

    // Requires IHasAttachments
    public static IQueryable<TEntity> FilterHasAttachment<TEntity>(this IQueryable<TEntity> query, bool? hasAttachment);

    public static IQueryable<TEntity> SortQuery<TEntity, TKey>(this IQueryable<TEntity> query)
        where TEntity : IEntity<TKey>;

    // Start the ordering, or continue it with ThenBy when one is already applied. ALWAYS use these in
    // SortBy builders — a hand-rolled `is IOrderedQueryable<T>` check throws at request time on EF Core.
    public static IOrderedQueryable<TEntity> OrderOrThenBy<TEntity, TKey>(this IQueryable<TEntity> query, Expression<Func<TEntity, TKey>> keySelector);
    public static IOrderedQueryable<TEntity> OrderOrThenByDescending<TEntity, TKey>(this IQueryable<TEntity> query, Expression<Func<TEntity, TKey>> keySelector);
}
```

**How it decides:** it reads `query.Expression.Type` — whether the *composed expression tree* already reports
an ordering — not the runtime type. That distinction is the whole point: an EF Core query object satisfies
`is IOrderedQueryable<T>` before any ordering exists, which is why the hand-rolled check throws.

**Chaining is therefore supported, and is how you write a multi-key sort.** Inside one `SortBy` arm,
`query.OrderOrThenBy(x => x.LastName).OrderOrThenBy(x => x.FirstName)` composes `OrderBy(LastName)
.ThenBy(FirstName)`: the first call returns an `IOrderedQueryable<T>`, so the second sees an ordered
expression tree and continues it. No need to split a composite sort across several enum members.

---

## Service Builders

### Quick reference — `For<>()` overload → builder → available methods

Each `For<>()` overload yields a different builder type, which determines the `SortBy` lambda arity
and whether typed `Includes` is available. Match the controller base and any manual
`IEntityService<>` resolution to the same generics (see `entities.instructions.md` §Step 13).

| `For<>()` overload | Builder type | Tier | `SortBy` lambda | Typed `Includes` | `Process` / `Related` |
|---|---|---|---|---|---|
| `For<TEntity>()` | `EntityIntServiceBuilder` | Simple | `query => …` (1-arg) | — | `Process` ✓ · `Related<TRelated>` ✓ |
| `For<TEntity, TKey>()` | `EntityServiceBuilder` | Simple | `query => …` (1-arg) | — | `Process` ✓ · `Related<TRelated, TRelatedKey>` (2-arg for non-int key) |
| `For<TEntity, TKey, TSearchObject>()` | `EntitySearchObjectServiceBuilder` | Simple | `query => …` (1-arg) | — | `Process` ✓ · `Related` ✓ |
| `For<TEntity, TSearchObject, TSortBy, TIncludes>()` | `ComplexEntityIntServiceBuilder` | **Complex** | `(query, sortBy) => …` (2-arg) | ✓ | `Process` ✓ · `Related<TRelated>` ✓ |
| `For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>()` | `ComplexEntityServiceBuilder` | **Complex** | `(query, sortBy) => …` (2-arg) | ✓ | `Process` ✓ · `Related` ✓ |

> Using the 2-arg `SortBy((query, sortBy) => …)` on a **simple** builder is **CS1593** — simple
> builders have no `TSortBy`. The **typed** `e.Includes((query, includes) => …)` overload (keyed to a
> `[Flags]` `TIncludes`) exists only on the two **complex** builders — but every builder, **simple ones
> included**, inherits the **untyped** `e.Includes((query, EntityIncludes?) => query.Include(...))`
> overload (the "Typed `Includes`" column below tracks only the typed form), so simple registrations can
> still eager-load navigations. The single-arg `e.Related<TRelated>(…)` shortcut works on every int-key
> builder (incl. the simple `For<TEntity, int, TSearchObject>()`); a non-int related key needs the
> 2-arg `e.Related<TRelated, TRelatedKey>(…)`.
>
> `HasAttachments` is an extension on the **base** `EntityServiceBuilder` (`Regira.Entities.DependencyInjection.Attachments`),
> so it applies on **every** tier — a **complex** owner chains `.HasAttachments(...)` exactly like a simple one.
> `WithAttachments` is an instance method on the `EntityServiceCollection<TContext>` that `UseEntities` returns
> (registered once for the shared `Attachment`, independent of any owner's tier). (§Attachments)

### Top-Level DI Entry Point

```csharp no-compile
using Regira.Entities.DependencyInjection.Extensions;

public static EntityServiceCollection<TContext> UseEntities<TContext>(
    this IServiceCollection services,
    Action<EntityServiceCollectionOptions>? configure = null)
    where TContext : DbContext;
```

---

### EntityServiceCollectionOptions Extension Methods

#### Setup

```csharp no-compile
using Regira.Entities.DependencyInjection.Extensions;

// Registers in one call: paging defaults (DefaultPageSize=10, MaxPageSize=100), default primers
// (HasCreated/HasLastModified/Archivable), default global filters (Ids/Archivables/HasCreated/HasLastModified),
// and the default entity normalizer. Also calls AddDefaultInterceptors() (= WireDbContext(DbContextWiring.All)): UseEntities<TContext>()
// then wires the primer/normalizer/auto-truncate interceptors + UTC date convention into the DbContext options
// automatically (AddDbContext only needs the provider; assignability match — an abstract-base registration
// also wires derived provider-specific contexts, in any registration order). UTC date handling itself is on
// by default process-wide (DateTimeDefaults.UseUtc) — disable with UseUtc(false).
// The normalized-content Q filter (FilterHasNormalizedContentQueryBuilder)
// is added ONLY on the parameterless overload — UseDefaults(cfg => …) skips it, so register it yourself then.
public static EntityServiceCollectionOptions UseDefaults(
    this EntityServiceCollectionOptions options,
    Action<EntityDefaultNormalizingOptions>? configure = null);

public static EntityServiceCollectionOptions UseNormalizerDefaults(
    this EntityServiceCollectionOptions options,
    Action<EntityDefaultNormalizingOptions>? configure = null);
```

#### Mapping

```csharp no-compile
// package: Regira.Entities.Mapping.Mapster
using Regira.Entities.Mapping.Mapster;

public static EntityServiceCollectionOptions UseMapsterMapping(
    this EntityServiceCollectionOptions options,
    Action<TypeAdapterConfig>? configure = null);
```

```csharp no-compile
// package: Regira.Entities.Mapping.AutoMapper
using Regira.Entities.Mapping.AutoMapper;

public static EntityServiceCollectionOptions UseAutoMapper(
    this EntityServiceCollectionOptions options,
    Action<IServiceProvider, IMapperConfigurationExpression>? configure = null);
```

```csharp no-compile
using Regira.Entities.DependencyInjection.Mapping;

public static EntityServiceCollectionOptions AddAfterMapper<TAfterMapper>(
    this EntityServiceCollectionOptions options)
    where TAfterMapper : class, IEntityAfterMapper;

public static EntityServiceCollectionOptions AfterMap<TSource, TTarget>(
    this EntityServiceCollectionOptions options,
    Action<TSource, TTarget> afterMapAction);

public static EntityServiceCollectionOptions AfterMap<TSource, TTarget>(
    this EntityServiceCollectionOptions options,
    Func<IServiceProvider, Action<TSource, TTarget>> afterMapAction);
```

#### Preppers (global)

```csharp no-compile
using Regira.Entities.DependencyInjection.Preppers;

public static EntityServiceCollectionOptions AddPrepper<TImplementation>(
    this EntityServiceCollectionOptions options)
    where TImplementation : class, IEntityPrepper;

public static EntityServiceCollectionOptions AddPrepper<TEntity>(
    this EntityServiceCollectionOptions options,
    Action<TEntity> prepareFunc)
    where TEntity : class;

public static EntityServiceCollectionOptions AddPrepper<TContext, TEntity, TKey>(
    this EntityServiceCollectionOptions options,
    Func<TEntity, TContext, Task> prepareFunc)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>;
```

#### Primers (global)

```csharp no-compile
using Regira.Entities.DependencyInjection.Primers;

public static EntityServiceCollectionOptions AddPrimer<TPrimer>(
    this EntityServiceCollectionOptions options)
    where TPrimer : class, IEntityPrimer;

// Registers ArchivablePrimer + HasCreatedDbPrimer + HasLastModifiedDbPrimer
public static EntityServiceCollectionOptions AddDefaultPrimers(
    this EntityServiceCollectionOptions options);
```

#### Global Filter Query Builders

```csharp no-compile
using Regira.Entities.DependencyInjection.QueryBuilders;

public static EntityServiceCollectionOptions AddGlobalFilterQueryBuilder<TImplementation>(
    this EntityServiceCollectionOptions options)
    where TImplementation : class, IGlobalFilteredQueryBuilder;

// Registers FilterIdsQueryBuilder + FilterArchivablesQueryBuilder
//   + FilterHasCreatedQueryBuilder + FilterHasLastModifiedQueryBuilder
public static EntityServiceCollectionOptions AddDefaultGlobalQueryFilters(
    this EntityServiceCollectionOptions options);
```

#### Normalizers (global)

```csharp no-compile
using Regira.Entities.DependencyInjection.Normalizers;

public static EntityServiceCollectionOptions AddNormalizer<TNormalizer>(
    this EntityServiceCollectionOptions options)
    where TNormalizer : class, IEntityNormalizer;

public static EntityServiceCollectionOptions AddNormalizer<TEntity, TNormalizer>(
    this EntityServiceCollectionOptions options)
    where TNormalizer : class, IEntityNormalizer<TEntity>;

// Registers DefaultNormalizer + ObjectNormalizer + DefaultEntityNormalizer + QKeywordHelper
public static EntityServiceCollectionOptions AddDefaultEntityNormalizer(
    this EntityServiceCollectionOptions options,
    Action<NormalizeOptions>? configure = null);
```

---

### EntityServiceCollection\<TContext\>

```csharp no-compile
using Regira.Entities.DependencyInjection.ServiceCollections;

public class EntityServiceCollection<TContext>
    where TContext : DbContext
{
    // Simple entity (int key, default SearchObject)
    EntityServiceCollection<TContext> For<TEntity>(
        Action<EntityIntServiceBuilder<TContext, TEntity>>? configure = null)
        where TEntity : class, IEntity<int>;

    // Entity with custom TKey
    EntityServiceCollection<TContext> For<TEntity, TKey>(
        Action<EntityServiceBuilder<TContext, TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>;

    // Entity with custom TKey + SearchObject
    EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject>(
        Action<EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new();

    // Full-featured (int key) — most common
    EntityServiceCollection<TContext> For<TEntity, TSearchObject, TSortBy, TIncludes>(
        Action<ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>>? configure = null)
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

    // Full-featured (with TKey)
    EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
        Action<ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

    // Attachments — register the shared Attachment entity + file store + bytes→file primer.
    // Framework infrastructure: the shared Attachment base is registered once and reused by every owner.
    EntityServiceCollection<TContext> WithAttachments(
        Func<IServiceProvider, IFileService> factory,
        Action<EntitySearchObjectServiceBuilder<TContext, Attachment, int, AttachmentSearchObject<int>>>? configure = null);

    // Generic service helpers
    EntityServiceCollection<TContext> AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    EntityServiceCollection<TContext> AddTransient<TService>(
        Func<IServiceProvider, TService> factory)
        where TService : class;
}

// Typed per-owner attachment registration — extension on the For<>() builder
// (Regira.Entities.DependencyInjection.Attachments.EntityServiceBuilderExtensions).
// Registers the typed read/write services, the link prepper, RelatedAttachments and DTO mapping,
// plus a per-owner join entity (e.g. ProductAttachment). For slot cost see §License requirement.
// Usage: services.For<Product>(e => e.HasAttachments<AppDbContext, Product, ProductAttachment>(x => x.Attachments));
public static class EntityServiceBuilderExtensions
{
    public static EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>
        HasAttachments<TContext, TEntity, TEntityAttachment>(
            this EntityServiceBuilder<TContext, TEntity, int> builder,
            Expression<Func<TEntity, ICollection<TEntityAttachment>?>> navigationExpression,
            Action<EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>>? configure = null)
        where TContext : DbContext
        where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
        where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new();
}
```

---

### EntityServiceBuilder\<TContext, TEntity, TKey\>

Base builder. Derives from `EntityServiceCollection<TContext>` (above), so inside a `.For<>(e => …)` lambda `e` also offers that type's generic `AddTransient(...)` registration helpers — e.g. `e.AddTransient<IOrderService, OrderManager>()` — alongside the builder methods below.

```csharp no-compile
using Regira.Entities.DependencyInjection.ServiceBuilders;

public partial class EntityServiceBuilder<TContext, TEntity, TKey> : EntityServiceCollection<TContext>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    bool HasEntityService();
    bool HasService<TService>();

    // Elevate to typed search object builder
    EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>
        WithSearchObject<TSearchObject>()
        where TSearchObject : class, ISearchObject<TKey>, new();

    // Mapping
    MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto>
        UseMapping<TDto, TInputDto>(Action<IEntityMapConfigurator>? mapAction = null);

    // Escape hatch — register an explicit Mapster config for a single type pair.
    // NOT required to project nested DTOs / Related() collections: Mapster maps similar models
    // (incl. nested children) by convention. Add a pair only when convention gets it wrong —
    // e.g. a child DTO whose shape diverges, or a child InputDto that needs a custom mapping.
    // (See entities.instructions.md §Step 10. An empty nested collection is usually a missing
    //  Includes, not a missing mapping.)
    EntityServiceBuilder<TContext, TEntity, TKey> AddMapping<TSource, TTarget>();

    // Service registration
    EntityServiceBuilder<TContext, TEntity, TKey> AddDefaultService();

    // Registers IEntityService<TEntity,TKey> + IEntityService<TEntity,TKey,SearchObject<TKey>>.
    // Note: does NOT register the bare IEntityService<TEntity> shortcut — that is only registered by
    // For<TEntity>() and the complex int For<TEntity,TSearchObject,TSortBy,TIncludes>() overloads.
    EntityServiceBuilder<TContext, TEntity, TKey> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey>, IEntityService<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> UseEntityService<TService>(
        Func<IServiceProvider, TService> factory)
        where TService : class, IEntityService<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> UseWriteService<TService>()
        where TService : class, IEntityWriteService<TEntity, TKey>;

    EntityServiceBuilder<TContext, TEntity, TKey> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> HasRepository<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> HasManager<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>;

    // Query builders
    EntityServiceBuilder<TContext, TEntity, TKey> AddDefaultQueryBuilder();

    EntityServiceBuilder<TContext, TEntity, TKey> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>;

    EntityServiceBuilder<TContext, TEntity, TKey> UseQueryBuilder<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>;

    EntityServiceBuilder<TContext, TEntity, TKey> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> AddFilter<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>;

    EntityServiceBuilder<TContext, TEntity, TKey> Filter(
        Func<IQueryable<TEntity>, SearchObject<TKey>?, IQueryable<TEntity>> filterFunc);

    // Sorting / includes (typed to EntitySortBy / EntityIncludes at this level)
    EntityServiceBuilder<TContext, TEntity, TKey> SortBy(
        Func<IQueryable<TEntity>, IQueryable<TEntity>> sortBy);

    EntityServiceBuilder<TContext, TEntity, TKey> Includes(
        Func<IQueryable<TEntity>, EntityIncludes?, IQueryable<TEntity>> addIncludes);

    // Per-entity paging override (HTTP boundary). null = off; SetPageSize() opts out entirely.
    EntityServiceBuilder<TContext, TEntity, TKey> SetPageSize(
        int? defaultPageSize = null, int? maxPageSize = null);

    // Per-entity read-path override (fully replaces the global EntityReadOptions for this entity):
    // whether save endpoints re-fetch the entity via Details(id) for the response.
    EntityServiceBuilder<TContext, TEntity, TKey> SetReadBehavior(
        RefetchAfterSave refetchAfterSave = RefetchAfterSave.Always);

    EntityServiceBuilder<TContext, TEntity, TKey> AddPrimer<TPrimer>()
        where TPrimer : class, IEntityPrimer<TEntity>;

    // Normalizers
    EntityServiceBuilder<TContext, TEntity, TKey> AddNormalizer<TNormalizer>()
        where TNormalizer : class, IEntityNormalizer<TEntity>;

    // Processors
    EntityServiceBuilder<TContext, TEntity, TKey> Process(
        Func<IList<TEntity>, EntityIncludes?, Task> process);

    EntityServiceBuilder<TContext, TEntity, TKey> Process(
        Action<TEntity, EntityIncludes?> process);

    EntityServiceBuilder<TContext, TEntity, TKey> AddProcessor<TProcessor>()
        where TProcessor : class, IEntityProcessor<TEntity, EntityIncludes>;

    // Preppers
    // inline shortcuts:
    EntityServiceBuilder<TContext, TEntity, TKey> Prepare(Action<TEntity> prepareFunc);

    EntityServiceBuilder<TContext, TEntity, TKey> Prepare(
        Func<TEntity, TContext, Task> prepareFunc);

    // class-based:
    EntityServiceBuilder<TContext, TEntity, TKey> AddPrepper<TPrepper>()
        where TPrepper : class, IEntityPrepper<TEntity>;

    // Server-owned scalar/FK: restored from the stored row on update, minted on create when
    // mintOnCreate is supplied and the property is unset. [ServerOwned] is the protect-only
    // attribute form (no registration needed once UseDefaults() has run).
    EntityServiceBuilder<TContext, TEntity, TKey> ServerOwned<TProp>(
        Expression<Func<TEntity, TProp>> selector,
        Func<TEntity, TProp>? mintOnCreate = null);

    // Primers
    // inline shortcuts:
    EntityServiceBuilder<TContext, TEntity, TKey> Prime(Action<TEntity> primeFunc);

    EntityServiceBuilder<TContext, TEntity, TKey> Prime(
        Func<TEntity, EntityEntry, TContext, Task> primeFunc);

    // class-based: AddPrimer<TPrimer>() (see above)

    // Related child collections (managed by RelatedCollectionPrepper).
    // prepareFunc = optional parent-level prepare; configure = optional RelatedEntityBuilder
    // callback to nest sub-collections or add per-item prepare logic.
    EntityServiceBuilder<TContext, TEntity, TKey> Related<TRelated, TRelatedKey>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, TRelatedKey>>? configure = null)
        where TRelated : class, IEntity<TRelatedKey>;

    void Build();
}
```

---

### RelatedEntityBuilder\<TContext, TRelated, TRelatedKey\>

Passed into the `configure` callback of the `Related(...)` overload. Allows configuring nested sub-collections and per-item prepare logic for a related collection.

```csharp no-compile
using Regira.Entities.DependencyInjection.ServiceBuilders;

public class RelatedEntityBuilder<TContext, TRelated, TRelatedKey>
    where TContext : DbContext
    where TRelated : class, IEntity<TRelatedKey>
{
    // Nest a sub-collection (generic key)
    RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated, TSubRelatedKey>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TSubRelated, TSubRelatedKey>>? configure = null)
        where TSubRelated : class, IEntity<TSubRelatedKey>;

    // Int-key shortcut for sub-collections
    RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TSubRelated, int>>? configure = null)
        where TSubRelated : class, IEntity<int>;

    // Add a prepare action applied to each item in the collection
    RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Prepare(Action<TRelated> prepareFunc);

    // Server-owned scalar/FK on the child (a line's UnitPrice) - same semantics as the parent's
    RelatedEntityBuilder<TContext, TRelated, TRelatedKey> ServerOwned<TProp>(
        Expression<Func<TRelated, TProp>> selector,
        Func<TRelated, TProp>? mintOnCreate = null);
}
```

---

### EntitySearchObjectServiceBuilder

Returned by `WithSearchObject<TSearchObject>()`. Inherits all `EntityServiceBuilder` methods.
**Only listing new / changed members:**

```csharp no-compile
public partial class EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>
    : EntityServiceBuilder<TContext, TEntity, TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    // NEW: elevate to full-featured builder
    ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>
        Complex<TSortBy, TIncludes>()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

    // CHANGED: constraint uses TSearchObject instead of SearchObject<TKey>
    EntitySearchObjectServiceBuilder<...> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey, TSearchObject>;

    EntitySearchObjectServiceBuilder<...> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>;

    EntitySearchObjectServiceBuilder<...> AddFilter<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>;

    EntitySearchObjectServiceBuilder<...> Filter(
        Func<IQueryable<TEntity>, TSearchObject?, IQueryable<TEntity>> filterFunc);

    // NEW: single-type-arg Related shortcut for int-keyed children (related key is int,
    // independent of the parent TKey). Use the inherited Related<TRelated, TRelatedKey> for non-int related keys.
    EntitySearchObjectServiceBuilder<...> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, int>>? configure = null)
        where TRelated : class, IEntity<int>;

    void Build();
}
```

> **No bare `IEntityService<TEntity>` for this builder.** `For<TEntity, int, TSearchObject>()` registers
> `IEntityService<TEntity, int>` and `IEntityService<TEntity, int, TSearchObject>` — **not** the bare
> `IEntityService<TEntity>` shortcut. Inject/resolve `IEntityService<TEntity, int, TSearchObject>`
> (see the Inject-as table in `entities.instructions.md`). The bare shortcut exists only for
> `For<TEntity>()` and the complex int `For<TEntity, TSearchObject, TSortBy, TIncludes>()` overloads.

---

### Int-Key Variants

```csharp no-compile
// For<TEntity>() → EntityIntServiceBuilder
public partial class EntityIntServiceBuilder<TContext, TEntity>
    : EntityServiceBuilder<TContext, TEntity, int>
    where TEntity : class, IEntity<int>
{
    // Advance to SearchObject variant
    EntityIntServiceBuilder<TContext, TEntity, TSearchObject> WithSearchObject<TSearchObject>()
        where TSearchObject : class, ISearchObject<int>, new();

    // Int-key shortcuts (no TRelatedKey / TContext parameter needed)
    EntityIntServiceBuilder<TContext, TEntity> Prepare(Func<TEntity, TContext, Task> prepareFunc);

    // Re-declared to keep the builder type through a chain — without it the next call falls back to
    // the base Related<TRelated, TRelatedKey>, whose key argument cannot be inferred (CS0411).
    EntityIntServiceBuilder<TContext, TEntity> ServerOwned<TProp>(
        Expression<Func<TEntity, TProp>> selector,
        Func<TEntity, TProp>? mintOnCreate = null);

    // Int-key shortcuts: sync only, or with a configure callback. For a parent-level prepare use
    // the inherited Related<TRelated, int>(nav, prepareFunc) or a separate e.Prepare(...).
    EntityIntServiceBuilder<TContext, TEntity> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression)
        where TRelated : class, IEntity<int>;

    EntityIntServiceBuilder<TContext, TEntity> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TRelated, int>> configure)
        where TRelated : class, IEntity<int>;

    void Build();
}

// For<TEntity, TSearchObject>() or WithSearchObject() → EntityIntServiceBuilder<TContext, TEntity, TSearchObject>
public class EntityIntServiceBuilder<TContext, TEntity, TSearchObject>
    : EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
{
    // Advance to full-featured
    ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>
        Complex<TSortBy, TIncludes>()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

    void Build();
}
```

---

### ComplexEntityServiceBuilder

Returned by `.For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>()` or `.Complex<TSortBy, TIncludes>()`.
Inherits all `EntitySearchObjectServiceBuilder` methods.
**Only listing new / changed members:**

```csharp no-compile
public partial class ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    : EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    // CHANGED: constraints use full TSortBy/TIncludes
    ComplexEntityServiceBuilder<...> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>;

    ComplexEntityServiceBuilder<...> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>;

    ComplexEntityServiceBuilder<...> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity, TKey>;

    ComplexEntityServiceBuilder<...> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>;

    ComplexEntityServiceBuilder<...> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>;

    // NEW: typed sorting
    ComplexEntityServiceBuilder<...> AddSortBy<TImplementation>()
        where TImplementation : class, ISortedQueryBuilder<TEntity, TKey, TSortBy>;

    ComplexEntityServiceBuilder<...> SortBy(
        Func<IQueryable<TEntity>, TSortBy?, IQueryable<TEntity>> sortByFunc);

    // NEW: typed includes
    ComplexEntityServiceBuilder<...> AddIncludes<TImplementation>()
        where TImplementation : class, IIncludableQueryBuilder<TEntity, TKey, TIncludes>;

    ComplexEntityServiceBuilder<...> Includes(
        Func<IQueryable<TEntity>, TIncludes?, IQueryable<TEntity>> addIncludes);

    // NEW: typed processors
    ComplexEntityServiceBuilder<...> Process(Func<IList<TEntity>, TIncludes?, Task> process);
    ComplexEntityServiceBuilder<...> Process(Action<TEntity, TIncludes?> process);
    ComplexEntityServiceBuilder<...> AddProcessor<TImplementation>()
        where TImplementation : class, IEntityProcessor<TEntity, TIncludes>;

    void Build();
}
```

---

### ComplexEntityIntServiceBuilder

Returned by `.For<TEntity, TSearchObject, TSortBy, TIncludes>()`.
Inherits all `ComplexEntityServiceBuilder` methods. Only addition vs parent:

```csharp no-compile
public partial class ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>
    : ComplexEntityServiceBuilder<TContext, TEntity, int, TSearchObject, TSortBy, TIncludes>
{
    // Int-key shortcut — no TRelatedKey type parameter needed
    ComplexEntityIntServiceBuilder<...> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, int>>? configure = null)
        where TRelated : class, IEntity<int>;

    void Build();
}
```

---

### MappedEntityServiceBuilder

Returned by `UseMapping<TDto, TInputDto>()`. Inherits all builder methods.

> ⚠️ The class-based `After<TImplementation>()` overloads live on the **base (untyped)** variant and return it — `TDto`/`TInputDto` are lost, so the typed `.After(...)`/`.AfterInput(...)` shortcuts no longer compile after them (CS1061). Chain the typed shortcuts first, or keep both after-mappers inline.

```csharp no-compile
// Base variant — any source/target after-mapper
public class MappedEntityServiceBuilder<TContext, TEntity, TKey>
    : EntityServiceBuilder<TContext, TEntity, TKey>
{
    MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TImplementation>()
        where TImplementation : class, IEntityAfterMapper;

    MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TImplementation>(
        Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityAfterMapper;

    MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TSource, TTarget>(
        Action<TSource, TTarget> afterMapAction);

    MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TSource, TTarget>(
        Func<IServiceProvider, Action<TSource, TTarget>> afterMapActionFactory);
}

// Typed variant — TDto and TInputDto known; shortcut After/AfterInput
public class MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto>
    : MappedEntityServiceBuilder<TContext, TEntity, TKey>
{
    // Entity → TDto after-mapper
    MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto> After(
        Action<TEntity, TDto> afterMapAction);

    // TInputDto → Entity after-mapper
    MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto> AfterInput(
        Action<TInputDto, TEntity> afterMapAction);
}
```

---

## Mapping and Processing

### IEntityMapper

```csharp
using Regira.Entities.Mapping.Abstractions;

public interface IEntityMapper
{
    TTarget Map<TTarget>(object source);
    TTarget Map<TSource, TTarget>(TSource source, TTarget target);
}
```

### After Mappers

```csharp no-compile
using Regira.Entities.Mapping.Abstractions;

public interface IEntityAfterMapper
{
    bool CanMap(object source);
    void AfterMap(object source, object target);
}

public interface IEntityAfterMapper<in TSource, in TTarget> : IEntityAfterMapper
{
    void AfterMap(TSource source, TTarget target);
}

// Inherit to create a custom after-mapper class
public abstract class EntityAfterMapperBase<TSource, TTarget> : IEntityAfterMapper<TSource, TTarget>
{
    public abstract void AfterMap(TSource source, TTarget target);
    public bool CanMap(object source);
}
```

### Query Builders

```csharp
using Regira.Entities.QueryBuilders.Abstractions;

public interface IFilteredQueryBuilder<TEntity, TKey, in TSearchObject>
    where TSearchObject : ISearchObject<TKey>
{
    IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so);
}

// Preferred base class — inherit and override Build().
public abstract class FilteredQueryBuilderBase<TEntity>
    : FilteredQueryBuilderBase<TEntity, SearchObject<int>>
    where TEntity : IEntity<int>;

public abstract class FilteredQueryBuilderBase<TEntity, TSearchObject>
    : FilteredQueryBuilderBase<TEntity, int, TSearchObject>
    where TEntity : IEntity<int>
    where TSearchObject : ISearchObject<int>;

public abstract class FilteredQueryBuilderBase<TEntity, TKey, TSearchObject>
    : IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    where TSearchObject : ISearchObject<TKey>
{
    public abstract IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so);
}

// 2-param shortcut defaults TSortBy to EntitySortBy
public interface ISortedQueryBuilder<TEntity, TKey> : ISortedQueryBuilder<TEntity, TKey, EntitySortBy>
    where TEntity : IEntity<TKey>;

public interface ISortedQueryBuilder<TEntity, TKey, TSortBy>
    where TEntity : IEntity<TKey>
    where TSortBy : struct, Enum
{
    IQueryable<TEntity> SortBy(IQueryable<TEntity> query, TSortBy? sortBy = null);
}

// 2-param shortcut defaults TIncludes to EntityIncludes
public interface IIncludableQueryBuilder<TEntity, TKey> : IIncludableQueryBuilder<TEntity, TKey, EntityIncludes>
    where TEntity : IEntity<TKey>;

public interface IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    where TEntity : IEntity<TKey>
    where TIncludes : struct, Enum
{
    IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, TIncludes? includes = null);
}
```

### Processors

```csharp no-compile
using Regira.Entities.Processing.Abstractions;

public interface IEntityProcessor<TEntity, in TIncludes>
    where TIncludes : struct, Enum
{
    Task Process(IList<TEntity> items, TIncludes? includes, CancellationToken token = default);
}
```

### Preppers

```csharp no-compile
using Regira.Entities.Preppers.Abstractions;

public interface IEntityPrepper
{
    Task Prepare(object modified, object? original, CancellationToken token = default);
}

public interface IEntityPrepper<in TEntity> : IEntityPrepper
{
    Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default);
}
```

### Primers

```csharp no-compile
using Regira.Entities.EFcore.Primers.Abstractions;

public interface IEntityPrimer
{
    Task PrepareManyAsync(IList<EntityEntry> entries, CancellationToken token = default);
    Task PrepareAsync(object entity, EntityEntry entry, CancellationToken token = default);
    bool CanPrepare(object entity);
}

public interface IEntityPrimer<in T> : IEntityPrimer
{
    Task PrepareAsync(T entity, EntityEntry entry, CancellationToken token = default);
    bool CanPrepare(T entity);
}
```

### Normalizers

```csharp no-compile
using Regira.Entities.Normalizing.Abstractions;

public interface IEntityNormalizer
{
    bool IsExclusive { get; }
    Task HandleNormalize(object item, CancellationToken token = default);
    Task HandleNormalizeMany(IEnumerable<object> items, CancellationToken token = default);
}

public interface IEntityNormalizer<in T> : IEntityNormalizer
{
    Task HandleNormalize(T item, CancellationToken token = default);
    Task HandleNormalizeMany(IEnumerable<T> items, CancellationToken token = default);
}
```

### Keyword Parsing

```csharp no-compile
using Regira.Entities.Keywords;
using Regira.Entities.Keywords.Abstractions;

public interface IQKeywordHelper
{
    ParsedKeywordCollection Parse(string? input);   // splits on spaces — one QKeyword per term
    QKeyword ParseKeyword(string? input);           // a single term
}

public class QKeyword
{
    public string? Keyword { get; set; }            // unmodified input, wildcards included
    public bool HasWildcardAtStart { get; set; }
    public bool HasWildcardAtEnd { get; set; }
    public bool HasWildcard { get; }

    // RAW family — the input minus its wildcards, untouched otherwise
    public string? Trimmed { get; set; }
    public string? TrimmedStartsWith { get; set; }  // "term%"
    public string? TrimmedEndsWith { get; set; }    // "%term"
    public string? TrimmedQ { get; set; }           // wildcards the INPUT carried, e.g. "*term" → "%term"
    public string? TrimmedQW { get; set; }          // "%term%", always both ends

    // NORMALIZED family — same shapes, built from the normalized keyword
    public string? Normalized { get; set; }
    public string? StartsWith { get; set; }
    public string? EndsWith { get; set; }
    public string? Q { get; set; }
    public string? QW { get; set; }
}
```

**Match the family to the column.** Everything carrying the `Trimmed` prefix holds the raw keyword;
everything without it holds the normalized one (`Q`/`QW` included). Pair a normalized column
(`NormalizedContent`, `NormalizedLastName`) with the unprefixed members, and a column that stores the
client's value verbatim (`FileName`, a reference code) with the `Trimmed*` ones. Crossing them compiles
and silently matches nothing — `QKeywordHelper.ApplyNormalize` defaults to `true`, and the default
normalizer drops `.` and turns `-` into a space (case is preserved: `Transform` defaults to
`NoChanges`), so no real file name survives it — `my-report.pdf` normalizes to `my reportpdf`.
Note the flip side: because the `Trimmed*` members are raw, a `%` or `_` the client typed keeps its
SQL `LIKE` meaning and over-matches. The normalized family is narrower here, not exempt: the default
normalizer's allow-list (`[^a-z0-9\s\-_,!;&']`) deletes `%` but keeps `_`, so `_` reaches `Q`/`QW`
as a single-character wildcard too. Escape them if exact punctuation has to match.

---

## Response Types

```csharp
using Regira.Entities.Web.Models;

public record DetailsResult<TDto>  { public TDto Item { get; set; }        public long? Duration { get; set; } }
public record ListResult<TDto>     { public IList<TDto> Items { get; set; } public long? Duration { get; set; } }
public record SearchResult<TDto>   { public IList<TDto> Items { get; set; } public long Count { get; set; }     public long? Duration { get; set; } }
public record SaveResult<TDto>     { public TDto Item { get; set; }        public bool IsNew { get; set; }     public int Affected { get; set; }   public long? Duration { get; set; } }
public record DeleteResult<TDto>   { public TDto Item { get; set; }        public long? Duration { get; set; } }
```

---

## Attachments

```csharp no-compile
using Regira.Entities.Attachments.Abstractions;

public interface IAttachment : IBinaryFile, IHasTimestamps;
public interface IAttachment<TKey> : IAttachment, IEntity<TKey>;
// Members (via IBinaryFile -> IStorageFile -> INamedFile and IHasTimestamps):
//   string? FileName; string? Identifier; string? Prefix; string? Path;
//   string? ContentType; long Length; byte[]? Bytes; Stream? Stream;
//   DateTime Created; DateTime? LastModified
// A Uri property exists on EntityAttachmentDto (Regira.Entities.Mapping.Models), not on the entity.

public interface IEntityAttachment
{
    string? ObjectType { get; }

    string? NewFileName { get; set; }
    string? NewContentType { get; set; }
    byte[]? NewBytes { get; set; }
    IAttachment? Attachment { get; set; }
}

public interface IEntityAttachment<TKey, TObjectKey> : IEntityAttachment<TKey, TObjectKey, int, Attachment>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey> : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, Attachment<TAttachmentKey>>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>
    : IEntity<TKey>, IHasObjectId<TObjectKey>, IEntityAttachment, ISortable
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    TAttachmentKey AttachmentId { get; set; }
    new TAttachment? Attachment { get; set; }
}
```

### Attachment Controller

```csharp no-compile
using Regira.Entities.Web.Attachments.Abstractions;

// Simplest variant
public abstract class EntityAttachmentControllerBase<TEntity>
    : EntityAttachmentControllerBase<TEntity, EntityAttachmentDto, EntityAttachmentInputDto>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>, IEntity<int>;

// Standard variant — set the class [Route] to the owner base path (e.g. [Route("products")]).
// The base actions append the sub-routes: {objectId}/attachments, attachments/{id},
// {objectId}/files, files/{id}, etc.
public abstract class EntityAttachmentControllerBase<TEntity, TDto, TInputDto>
    : ControllerBase
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>, IEntity<int>
    where TInputDto : class, IEntityAttachmentInput;
```

---

## Exceptions

```csharp
using Regira.Entities.Models;

// Throw to return HTTP 400 from a controller action
public class EntityInputException<T>(string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public T? Item { get; set; }
    public IDictionary<string, string> InputErrors { get; set; } = new Dictionary<string, string>(); // pre-initialized
}
```

`InputErrors` is initialized, so both forms work — a nested initializer for a fixed set, indexer assignment for a map you build:

```csharp no-compile
throw new EntityInputException<Order>("Saving order failed")
{
    InputErrors = { ["OrderLines"] = "Order must contain at least one order line." }   // nested initializer, no `new`
};

var ex = new EntityInputException<Order>("Saving order failed");
foreach (var line in invalidLines)
    ex.InputErrors[$"OrderLines[{line.Index}].Quantity"] = "Must be greater than zero.";  // dynamic map
throw ex;
```

---

## Supporting Types

```csharp
using Regira.DAL.Paging;

public record PagingInfo
{
    public int PageSize { get; set; }
    public int Page { get; set; } = 1;
}
```

```csharp no-compile
using Regira.Entities.DependencyInjection.ServiceCollections.Models;

public class EntityServiceCollectionOptions(IServiceCollection services)
{
    public IServiceCollection Services { get; }

    // Global list/paging defaults applied at the HTTP boundary (null = off).
    public int? DefaultPageSize { get; set; } // forced page size when the request omits paging
    public int? MaxPageSize { get; set; }     // upper limit a requested page size is clamped to

    // Query-pipeline behavior (see EntityQueryOptions below): the archived scope applied
    // when a search object leaves Archived null. Default Excluded.
    public ArchivedFilter DefaultArchivedFilter { get; set; }

    // UTC handling of entity DateTime values — one policy per process (Regira.Utilities.DateTimeDefaults.UseUtc,
    // on by default): primers write UtcNow, filters normalize input dates, and the UTC convention's converter
    // follows the same policy. UseUtc(false) → DateTime.Now and values are used as given.
    public EntityServiceCollectionOptions UseUtc(bool enabled = true);

    // The DbContext plumbing UseEntities<TContext>() auto-wires into the context's options.
    // UseDefaults() → All; à la carte without UseDefaults(): e.g. WireDbContext(DbContextWiring.PrimerInterceptors);
    // WireDbContext(DbContextWiring.None) → wire the DbContext yourself.
    public DbContextWiring DbContextWiring { get; set; }
    public EntityServiceCollectionOptions WireDbContext(DbContextWiring wiring = DbContextWiring.All);
    // Shorthand for WireDbContext(DbContextWiring.All) — the full default plumbing; called by UseDefaults()
    public EntityServiceCollectionOptions AddDefaultInterceptors();

    // Startup validation (arity mismatches, unwired interceptors, ignored ?q=, competing write authorities,
    // null attachment Uri, out-of-scope global filters, missing archived query filter, archivable reference
    // data behind a required FK, attachments the input DTO cannot carry). Development-only by default.
    public EntityServiceCollectionOptions ConfigureValidation(Action<EntityValidationOptions> configure);
}

[Flags]
public enum DbContextWiring
{
    None = 0,
    PrimerInterceptors = 1 << 0,
    NormalizerInterceptors = 1 << 1,
    AutoTruncateInterceptors = 1 << 2,
    UtcDateTimeConvention = 1 << 3,
    // e => !e.IsArchived on every IArchivable entity type — soft delete without a DbContext change
    ArchivedQueryFilter = 1 << 4,
    All = PrimerInterceptors | NormalizerInterceptors | AutoTruncateInterceptors | UtcDateTimeConvention
        | ArchivedQueryFilter
}
```

```csharp no-compile
using Regira.Entities.DependencyInjection.Validation;

public class EntityValidationOptions
{
    public bool? Enabled { get; set; }      // null = Development only; true = always; false = never
    public bool ThrowOnError { get; set; }  // default true: error-severity issues stop the host
}

// Entities.Web — registers the controller ↔ For<>() arity startup check
// (also enabled automatically by ConfigureDefaultJsonOptions())
public static EntityServiceCollectionOptions ValidateEntityControllers(this EntityServiceCollectionOptions options);
public static IServiceCollection ValidateEntityControllers(this IServiceCollection services);
```

```csharp
using Regira.Entities.Models;

// Runtime carrier for the list/paging defaults; resolved by the controller List/Search endpoints.
public class EntityListOptions
{
    public int? DefaultPageSize { get; set; }
    public int? MaxPageSize { get; set; }
}

// Per-entity override, registered via e.SetPageSize(...). When present it fully replaces the
// global EntityListOptions for that entity (each null aspect is off).
public class EntityListOptions<TEntity> : EntityListOptions where TEntity : class;

// Which archived rows a read returns. ISearchObject.Archived binds this from ?archived=.
public enum ArchivedFilter
{
    Excluded = 0, // non-archived only
    Included,     // archived and non-archived alike
    Only          // archived only — the recycle bin
}

// Runtime carrier for query-pipeline behavior; registered as a singleton by UseEntities().
public class EntityQueryOptions
{
    // Applied when ISearchObject.Archived is null.
    public ArchivedFilter DefaultArchivedFilter { get; set; }
}

// Read-path behavior. Global via UseEntities(o => { o.RefetchAfterSave = ...; }), or per-entity via
// e.SetReadBehavior(...). Details(id) always loads all registered includes; the web save endpoints
// re-fetch per RefetchAfterSave.
public enum RefetchAfterSave { Always = 0, WhenProcessorsRegistered = 1, Never = 2 }  // Always = current default
public class EntityReadOptions
{
    public RefetchAfterSave RefetchAfterSave { get; set; }
}
public class EntityReadOptions<TEntity> : EntityReadOptions where TEntity : class;  // per-entity override

// DTO wire-shape descriptor recorded by UseMapping<TDto, TInputDto>(); registered as a singleton so
// endpoint scanners and other infrastructure can resolve the wire shape configured for an entity.
public sealed record EntityMappingRegistration(Type EntityType, Type DtoType, Type InputDtoType);
```

---

## See Also

- [Entities Instructions](./entities.instructions.md) — Complete framework guide and decision rules
- [Entities Examples](./entities.examples.md) — Working code patterns
- [Entities Namespaces](./entities.namespaces.md) — Full namespace listing

