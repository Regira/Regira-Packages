# Entity Services

The `IEntityService` is the core service interface for managing entities. It provides standard CRUD operations and can be customized or extended as needed.

Possible combinations:
```csharp
IEntityService<TEntity> // int ID
IEntityService<TEntity, TKey>
IEntityService<TEntity, TKey, TSearchObject>
IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
```

## Service Layer Architecture

- The default implementation is `EntityRepository`, which uses EF Core `DbContext` for data access
- The `EntityRepository` is enriched by multiple helper services (QueryBuilders, Processors, Preppers, Primers)
- Replace the default EntityService using `UseEntityService` with a custom implementation (e.g., `CachedEntityService` that adds caching on top of the repository)

## Standard EntityRepository Methods

### Read Operations

```csharp
// Get single entity details by ID
Task<TEntity?> Details(TKey id, CancellationToken token = default)

// List with custom SearchObject (enhanced filtering)
Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
// List with sorting and includes (complex filtering)
Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)

// Count with custom (nullable) SearchObject
Task<long> Count(TSearchObject? so, CancellationToken token = default)
// Count with multiple SearchObjects
Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default)
```

> **Paging at the service layer:** `List` only pages when you pass a `PagingInfo` with a positive `PageSize`; otherwise it returns the full set. The configurable default/maximum page size (`DefaultPageSize` / `MaxPageSize`, or per-entity `e.SetPageSize(...)`) is applied at the **HTTP boundary** (MVC controllers and FastEndpoints alike), not here — so direct service calls keep full control. See [Web Endpoints → Paging](web-endpoints.md#paging).

### Write Operations

- Write methods (`Add`, `Modify`, `Save`, `Remove`) **do NOT automatically persist changes**
- You **must call** `SaveChanges()` to commit all changes to the database
- After a **successful** `SaveChanges()` the EF change tracker is cleared — all entities saved in that call are now detached. To update one later, pass it through `Modify()` or `Save()` again before the next `SaveChanges()`. A **failed** `SaveChanges()` leaves every entry tracked (stock EF Core semantics), so you can fix or remove the offending entity and retry the same call
- A database **integrity-constraint violation** (unique index, foreign key, NOT NULL, check) surfaces as `EntityConstraintException` — catch that, not `DbUpdateException`, around direct `SaveChanges()` calls (seeding, jobs). Transient faults (deadlocks, timeouts, concurrency conflicts) are not wrapped and still throw `DbUpdateException` subtypes. See [Built-in Features → Constraint Exceptions](built-in-features.md#constraint-exceptions)

```csharp
Task Save(TEntity item, CancellationToken token = default) // calls Add() or Modify() internally
Task Add(TEntity item, CancellationToken token = default)
Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
Task Remove(TEntity item, CancellationToken token = default)
// Persist all changes to database
Task<int> SaveChanges(CancellationToken token = default)
```

## Repository helper services

### Query Builders

Query builders are used to filter, sort entities and include navigation properties.

#### Filter Query Builders

- Uses the configured `TSearchObject`
- Inline shortcut is available `.Filter((query, so) => ...)`
- If no SearchObject is configured, a basic `SearchObject<TKey>` is provided

```csharp
// interface
public interface IFilteredQueryBuilder<TEntity, TKey, in TSearchObject>
    where TSearchObject : ISearchObject<TKey>
{
    IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so);
}
// base class
public abstract class FilteredQueryBuilderBase<TEntity, TKey, TSearchObject> : IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    where TSearchObject : ISearchObject<TKey>
{
    public abstract IQueryable<TEntity> Build(IQueryable<TEntity> query, TSearchObject? so);
}
```
#### Global Filter Query Builders

- Global filters apply to all entities implementing an interface and are **registered globally**
- uses the configured `TSearchObject` for the Entity who's Filter is being executed
- if no SearchObject is configured, a basic `SearchObject<TKey>` is provided

```csharp
// interface
public interface IGlobalFilteredQueryBuilder
{
    IQueryable<TEntity> Build<TEntity, TKey>(IQueryable<TEntity> query, ISearchObject<TKey>? so);
}
public interface IGlobalFilteredQueryBuilder<TEntity, TKey> : IGlobalFilteredQueryBuilder
{
    IQueryable<TEntity> Build(IQueryable<TEntity> query, ISearchObject<TKey>? so);
}
// base class
public abstract class GlobalFilteredQueryBuilderBase<TEntity> : GlobalFilteredQueryBuilderBase<TEntity, int>;
public abstract class GlobalFilteredQueryBuilderBase<TEntity, TKey> : FilteredQueryBuilderBase<TEntity, TKey, ISearchObject<TKey>>,
    IGlobalFilteredQueryBuilder<TEntity, TKey>
{
    IQueryable<TEntity> IGlobalFilteredQueryBuilder<TEntity, TKey>.Build(IQueryable<TEntity> query, ISearchObject<TKey>? so)
        => Build(query, so);
    IQueryable<T> IGlobalFilteredQueryBuilder.Build<T, TK>(IQueryable<T> query, ISearchObject<TK>? so)
        // a search object of a foreign key type coerces to null — the filter then applies its
        // key-agnostic default (e.g. hide archived rows); it must NOT step aside, or a
        // soft-delete/security default would be silently dropped
        => Build(query.Cast<TEntity>(), so as ISearchObject<TKey>).Cast<T>();
}
```

A search object of a foreign key type coerces to `null`, so a keyed filter falls back to its key-agnostic
default (e.g. a security filter's scoping predicate) rather than being dropped. The query builder runs **one
variant per filter family**, preferring the one whose key type matches the search object; when an entity uses
a non-int key, register the matching variants with `AddDefaultGlobalQueryFilters<TKey>()` so its typed fields
(Id/Ids) are honoured too. Key-agnostic defaults apply even when only the int variant is registered.

#### Sort Query Builder

- Uses the configured `TSortyBy`
- Inline shortcut is available
- If no SortBy enum is configured, a basic `EntitySortBy` is provided
- Implement interface, no base class provided

```csharp
// interface
public interface ISortedQueryBuilder<TEntity, TKey, TSortBy>
    where TEntity : IEntity<TKey>
    where TSortBy : struct, Enum
{
    IQueryable<TEntity> SortBy(IQueryable<TEntity> query, TSortBy? sortBy = null);
}
```

#### Include Query Builder

- Uses the configured `TIncludes`
- Inline shortcut is available
- If no Includes enum is configured, a basic `EntityIncludes` is provided
- Implement interface, no base class provided

```csharp
// interface
public interface IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    where TEntity : IEntity<TKey>
    where TIncludes : struct, Enum
{
    IQueryable<TEntity> AddIncludes(IQueryable<TEntity> query, TIncludes? includes = null);
}
```

### Entity Processors

- Processors modify/decorate entities after fetching from database
- Inline shortcut is available
- Implement interface, no base class provided

*Fill `[NotMapped]` properties here.*

```csharp
// interface
public interface IEntityProcessor<TEntity, TIncludes>
    where TIncludes : struct, Enum
{
    Task Process(IList<TEntity> items, TIncludes? includes, CancellationToken token = default);
}
```

### Entity Preppers

- Prepare entities before saving
- Inline shortcut is available
- Can be registered globally (apply to an interface/base type) or per entity 
- The original item is passed to enable advanced operations 

*Prepare child collections here, or calculated fields.*

```csharp
// interface
public interface IEntityPrepper<in TEntity> : IEntityPrepper
{
    Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default);
}
// base class
public abstract class EntityPrepperBase<TEntity> : IEntityPrepper<TEntity>
{
    public abstract Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default);
}
```

#### Related child collections

`e.Related()` is the shortcut for synchronizing owned child collections. It registers a `RelatedCollectionPrepper` that diffs the incoming collection against the stored one before `SaveChanges()`, marking items as added, modified or removed.

> **One writer per save path.** A collection synchronized with `Related()` is *owned* by the parent. Adding a `.For<>()`/`IEntityService<T>` for the same child is **allowed** — the registrations don't conflict, since `Related()` registers only a save-time prepper for the parent — but it is safe only under one condition: **the parent's input DTO must leave the collection `null`.**
>
> - **`null` on the parent DTO → the sync is a no-op.** It short-circuits before diffing, so the child's own service is the sole writer. This is the supported way to give an owned child its own read/PATCH endpoints.
> - **Collection present → the parent wins.** Its next save re-diffs the collection and silently reverts rows written through the standalone service. Watch the difference between absent and empty: `null` touches nothing, `[]` **deletes every row** — including when the navigation was never eager-loaded, since the prepper loads the rows from the store to diff against.
>
> **Key-type caveat:** deletions only happen when *every* incoming item has a non-null `Id`. With `int`/`Guid` keys that always holds; with a `string` key, one new child carrying a null `Id` suppresses **all** deletions in that save.
>
> Startup validation warns on the pairing because it cannot inspect your DTO shape — see §Validation. If the parent genuinely must send the collection, pick one authority: drop the `.For<>()`, or drop the `Related()` and load the navigation with `Include()` in the query builder.

The signature is `Related(navigationExpression, prepareFunc, configure)`, where both `prepareFunc` and `configure` are optional:

- **`prepareFunc`** — a parent-level prepare callback, invoked with the parent entity.
- **`configure`** — a `RelatedEntityBuilder` callback for shaping the child collection. Use `builder.Related(...)` to synchronize a nested sub-collection (recursively, to any depth) and `builder.Prepare(...)` to run a per-item prepare on each child.

```csharp
// Sync the collection, with an optional parent-level prepare:
e.Related<TRelated, TRelatedKey>(x => x.Collection, parentEntity => { /* ... */ });

// Nest sub-collections or add a per-item prepare via the RelatedEntityBuilder:
e.Related<TRelated, TRelatedKey>(x => x.Collection, configure: builder =>
{
    builder.Related(item => item.SubCollection);        // sync a nested sub-collection
    builder.Prepare(item => item.RecalculateTotals());  // per-item prepare on each TRelated
});

// Combine both — a parent-level prepare alongside the nested configuration:
e.Related<TRelated, TRelatedKey>(x => x.Collection,
    parentEntity => { /* parent-level prepare */ },
    builder =>
    {
        builder.Related(item => item.SubCollection);
        builder.Prepare(item => item.RecalculateTotals());
    });
```

### Entity Primers

- Executed as EF Core `SaveChangesInterceptors` by DbContext 
- The interceptor is wired into the DbContext options automatically by `UseEntities(e => e.UseDefaults())`;
  without `UseDefaults()`, select it with `e.WireDbContext(DbContextWiring.PrimerInterceptors)`
- Can be registered **globally** (apply to an interface or base type) or **per entity**
- Timestamp primers (`HasCreatedDbPrimer`, `HasLastModifiedDbPrimer`) write UTC values by default; the
  auto-wired UTC date convention (`UseDefaults()`) makes dates read from the database materialize as
  `DateTimeKind.Utc` and serialize to JSON with the `Z` suffix (standalone EF: `.AddUtcDateTimeConvention()` /
  `SetUtcDateTimeConvention()` — `Regira.DAL.EFcore.Extensions`). Disable UTC handling with
  `UseEntities(e => e.UseUtc(false))` → local time, values used as given; the convention's converter follows
  the same policy (one process-wide decision: `Regira.Utilities.DateTimeDefaults.UseUtc`, on by default)

```csharp
// interface
public interface IEntityPrimer<in T>
{
    Task PrepareAsync(T entity, EntityEntry entry, CancellationToken token = default);
    bool CanPrepare(T entity);
}
// base class
public abstract class EntityPrimerBase<T> : IEntityPrimer<T>
{
    public virtual async Task PrepareManyAsync(IList<EntityEntry> entries, CancellationToken token = default)

    public abstract Task PrepareAsync(T entity, EntityEntry entry, CancellationToken token = default);
    public virtual bool CanPrepare(T? entity) => entity != null;
}
```

## Dependency Injection

### Configuration Example

This example demonstrates how to configure entities with all helper services:

```csharp
// Configure DbContext — only the provider; UseEntities(e => e.UseDefaults()) wires the interceptors
services.AddDbContext<MyDbContext>(db =>
{
    db.UseSqlServer(connectionString);
});

// Configure Entity Services with all helper services
services
    .UseEntities<MyDbContext>(options =>
    {
        // Global helper services (apply to all entities implementing an interface)
        options.AddGlobalFilterQueryBuilder<FilterIdsQueryBuilder<int>>();
        options.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder>();
        // using Prepper shortcut (inline implementation)
        options.AddPrepper<IHasAggregateKey>(x => x.AggregateKey ??= Guid.NewGuid());
        options.AddPrimer<AutoTruncatePrimer>();
    })
    
    // Category
    .For<Category, Guid>(e =>
    {
        // Query Filter
        e.AddFilter<CategoryQueryFilter>();
        
        // Sorting — a simple entity takes one fixed order; the request's ?sortBy= is honored only on complex entities
        e.SortBy(query => query.OrderBy(x => x.Name));

        // Processor
        e.AddProcessor<CategoryProcessor>();
    })
    
    // Product
    .For<Product, ProductSearchObject, ProductSortBy, ProductIncludes>(e =>
    {
        // Query Filter (inline)
        e.Filter((query, so) =>
        {
            // filtering on Id is implemented by global filter
            if (so?.MinPrice != null)
                query = query.Where(x => x.Price >= so.MinPrice);
            if (so?.MaxPrice != null)
                query = query.Where(x => x.Price <= so.MaxPrice);
            return query;
        });
        
        // Sorting
        e.SortBy((query, sortBy) =>
        {
            return sortBy switch
            {
                ProductSortBy.Name => query.OrderBy(x => x.Name),
                ProductSortBy.NameDesc => query.OrderByDescending(x => x.Name),
                ProductSortBy.Price => query.OrderBy(x => x.Price),
                ProductSortBy.PriceDesc => query.OrderByDescending(x => x.Price),
                _ => query.OrderBy(x => x.Id)
            };
        });
        
        // Include — one registration per entity (a second call replaces the first).
        // Order inside an include when the relation carries sorted rows. Archived rows need no
        // predicate here on net10.0: the archived filter is an EF query filter, so it also
        // applies inside Include(...) — see Built-in features > Soft delete for the net8.0 gap.
        e.Includes((query, includes) =>
        {
            if (includes?.HasFlag(ProductIncludes.Category) == true)
                query = query.Include(x => x.Category);
            if (includes?.HasFlag(ProductIncludes.Reviews) == true)
                query = query.Include(x => x.Reviews!.OrderBy(r => r.SortOrder));
            return query;
        });
        
        // Processor
        e.Process((items, includes) =>
        {
            foreach (var item in items)
            {
                // Calculate display properties
                item.DisplayPrice = $"${item.Price:F2}";
            }
            return Task.CompletedTask;
        });
        
        // Prepper
        e.Prepare(item =>
        {
            // Ensure SKU is set
            item.Sku ??= GenerateSku(item);
        });
        
        // Primer
        e.AddPrimer<ProductPrimer>();
        
        // Related entities — simple: just sync the collection
        e.Related(x => x.Reviews);
    })
    
    // Order
    .For<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes>(e =>
    {
        e.AddFilter<OrderQueryFilter>();
        
        // OrderOrThenBy / OrderOrThenByDescending (Regira.Entities.EFcore.Extensions) start the
        // ordering or continue it with ThenBy — the lambda is called once per requested sort value
        e.SortBy((query, sortBy) => sortBy switch
        {
            OrderSortBy.OrderNumber => query.OrderOrThenBy(x => x.OrderNumber),
            OrderSortBy.OrderDate => query.OrderOrThenBy(x => x.OrderDate),
            OrderSortBy.TotalAmount => query.OrderOrThenBy(x => x.TotalAmount),
            _ => query.OrderOrThenByDescending(x => x.OrderDate)
        });
        
        e.AddIncludes<OrderIncludableQueryBuilder>();
        
        e.AddProcessor<OrderProcessor>();
        
        // Complex prepper with DbContext
        e.Prepare(async (item, dbContext) =>
        {
            // Recalculate order totals
            item.TotalAmount = item.OrderItems?.Sum(x => x.Quantity * x.UnitPrice) ?? 0;
            await Task.CompletedTask;
        });
        
        // Simple related with parent-level prepare
        e.Related(x => x.OrderItems, item => item.OrderItems?.SetSortOrder());
        // Configure overload — nest sub-collections or add per-item prepare
        // e.Related(x => x.OrderItems, builder =>
        // {
        //     builder.Related(oi => oi.Options);
        //     builder.Prepare(oi => oi.RecalculateTotals());
        // });
    });
```

**Registration Order Matters**

1. **Global services** execute first (registered on `EntityServiceCollectionOptions`)
2. **Entity-specific services** execute next (registered on entity builder)

**Tip**:

```csharp
// Use extension methods to configure Entities.
// Take the interface as the 'this' parameter; return the concrete EntityServiceCollection<TContext>
// (every For<>() returns it, and only it implements IServiceCollection — chains stay composable).
public static class ProductServiceCollectionExtensions
{
    public static EntityServiceCollection<TContext> AddProducts<TContext>(this IEntityServiceCollection<TContext> services)
        where TContext : DbContext
        => services.For<Product>(e =>
        {
            // put logic here ...
        });
}

// Resulting:
services
    .UseEntities<MyDbContext>(/* ... */)
    .AddProducts()
    .AddCategories()
    .AddOrders();
```

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. **[Services](services.md)** — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
