# Entity Services

The `IEntityService` is the core service interface for managing entities. It provides standard CRUD operations and can be customized or extended as needed.

Possible combinations:

<!-- no-compile -->
```csharp
IEntityService<TEntity> // int ID
IEntityService<TEntity, TKey>
IEntityService<TEntity, TKey, TSearchObject>
IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
```

## Service Layer Architecture

- The default implementation is `EntityRepository`, which uses EF Core `DbContext` for data access
- The `EntityRepository` is enriched by multiple helper services (QueryBuilders, Processors, Preppers, Validators, Primers, Reactors)
- Replace the default EntityService using `UseEntityService` with a custom implementation (e.g., `CachedEntityService` that adds caching on top of the repository)
- Replace the default `EntityRepository` for every entity at once with `UseRepository` — see [Replacing the repository app-wide](#replacing-the-repository-app-wide)

## Standard EntityRepository Methods

### Read Operations

<!-- no-compile -->
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

> **Paging at the service layer:** `List` only pages when you pass a `PagingInfo` with a positive `PageSize`; otherwise it returns the full set. The configurable default/maximum page size (`DefaultPageSize` / `MaxPageSize`, or per-entity `e.SetPageSize(...)`) is applied by the list and search requests the endpoints send ([Entity Operations](web-endpoints.md#entity-operations)), not here — so direct service calls keep full control. See [Web Endpoints → Paging](web-endpoints.md#paging).

### Write Operations

- Write methods (`Add`, `Modify`, `Save`, `Remove`) **do NOT automatically persist changes**
- You **must call** `SaveChanges()` to commit all changes to the database
- After a **successful** `SaveChanges()` the EF change tracker is cleared — all entities saved in that call are now detached. To update one later, pass it through `Modify()` or `Save()` again before the next `SaveChanges()`. A **failed** `SaveChanges()` leaves every entry tracked (stock EF Core semantics), so you can fix or remove the offending entity and retry the same call
- A database **integrity-constraint violation** (unique index, foreign key, NOT NULL, check) surfaces as `EntityConstraintException` — catch that, not `DbUpdateException`, around direct `SaveChanges()` calls (seeding, jobs). Transient faults (deadlocks, timeouts) are not wrapped and still throw `DbUpdateException` subtypes. See [Built-in Features → Constraint Exceptions](built-in-features.md#constraint-exceptions)
- A write built on a **stale read** — a concurrency token the row no longer holds, or a row another writer removed — surfaces as `EntityConcurrencyException`; catch that, not `DbUpdateConcurrencyException`. See [Built-in Features → Concurrency Exceptions](built-in-features.md#concurrency-exceptions)
- An entity read with `Details(id)` can go straight back into `Modify()` with its navigations loaded. When a foreign key — on the entity or on a `Related()` child — was set to another key, `Modify()` drops the reference navigation still pointing at the stored principal and removes the row from that principal's loaded collections (it may still be in the graph through another path), so the new key is saved rather than overwritten by EF's attach fixup; the entity then carries that navigation as `null`. A `Related()` child stays with the parent whose collection lists it, whatever its own parent key says; move it through the collections. To clear a relation, set both the foreign key and the navigation to `null`: an empty key beside a loaded navigation is left to the navigation, which is what a request that sends only the nested object relies on

<!-- no-compile -->
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

<!-- no-compile -->
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

`RemoveGlobalQueryFilters()` removes every global filter registered before it, the ones `UseDefaults()` adds among
them, so an application can start over with its own; register those after it with `AddGlobalFilterQueryBuilder<>()`.
The archived filter goes too, so archived rows are listed until one is registered again. It extends
`IServiceCollection` — the `UseEntities<TContext>()` builder is one, so it chains there — not
`EntityServiceCollectionOptions`.

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

<!-- no-compile -->
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

#### Server-owned fields

`[ServerOwned]` on a scalar (or `e.ServerOwned(x => x.Code, mint)` for the fluent form, which also mints on
create) restores that property from the stored row on update, so a PUT/PATCH that omits it cannot null it.
Enforced by `AutoServerOwnedPrepper`, registered by `UseDefaults()`. Being a prepper, it guards the
`IEntityService` write path only and leaves a workflow service's raw `DbContext` write alone. It has no
bypass, so a workflow action saving through `IEntityService` is restored too — guard such fields with your
own prepper and a scoped "trusted writer" flag instead. See
[Built-in features](./built-in-features.md#server-owned-fields).

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

For an `int`-keyed child, every builder infers the type argument, whatever the parent's key, and takes `configure` in second position too, so `e.Related(x => x.Lines, r => r.ServerOwned(x => x.UnitPrice))` needs no parameter name. A child with another key type takes the two-type-argument form below, which always reads its second argument as `prepareFunc`, so it names `configure:`.

<!-- no-compile -->
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

### Entity Validators

- Refuse a write: run inside `Add` / `Modify` / `Save` after **every** prepper (the global ones and the `Related()`
  sync included, whatever the registration order), and inside `Remove`, where no prepper runs
- Every validator in scope runs and adds its errors to one context; the write service then throws one
  `EntityInputException<TEntity>` of the entity it saves, before the entity is tracked, so the client gets every
  error in one 400, `DELETE` included
- Inline shortcut is available: `e.Validate(ctx => …)` or `e.Validate(async ctx => …)`, and
  `e.Validate(async (ctx, db, token) => …)` with the `DbContext` and the write's cancellation token for its queries. The
  write awaits an async delegate, so an error added after an `await` still refuses it
- A `Modify` whose stored row is not found runs no validator: it answers `null` (not found)
- A refused `Add` or `Modify` takes back what its preppers marked — the rows a `Related()` sync added, changed or
  deleted — and leaves the item itself untracked, even when the caller tracked it. A prepper's plain edit to another
  row the scope already tracked stays, since EF notices it only at `SaveChanges()`
- Scoped like preppers and global filters — to the entity, a base class or an interface (table below) — but matched
  against the item's **runtime** type: a validator on `Person` also runs when a `Person` is saved through the
  `Party` service, and the exception is still the service's own `EntityInputException<Party>`
- The place of registration never narrows the scope: a validator on an interface registered inside one `For<>()`
  checks every entity implementing it, so register such a validator once, with `options.AddValidator<T>()`; a class
  registered twice runs once
- A lookup through the `DbContext` skips row security: global filters (tenant, owner) scope the entity services'
  reads, not `db`, so `db.Customers.AnyAsync(…)` accepts another tenant's `CustomerId` and its 400 tells the client
  that id exists. Where reads are scoped, check the reference through the filtered read service — a class validator
  taking `IEntityReadService<Customer, int>` and refusing when `await customers.Details(id, token)` is `null` — or
  repeat the scope's predicate in the query. A validator never takes `IEntityService<>`: its write service imports
  every validator, so the container meets a circular dependency and no entity service in the app resolves
- Queries on `db` track nothing while the validators run, `Find` / `FindAsync` included, so a uniqueness check that
  returns the row being written — `db.Orders.FirstOrDefaultAsync(o => o.Code == ctx.Item.Code, token)` on a `PUT`
  that keeps its code — leaves the write free to track its own instance. Only an explicit `AsTracking()` opts back
  in, and the write then throws because another instance with the same key is tracked
- Children are validated through their parent: a validator checks the entity a write service saves, not the rows a
  `Related()` sync writes — check `Lines` from the `Order` validator, with keys like `Lines[0].Quantity`
- The context carries `Item`, `Original` (the stored row on `Modify`), `Operation` (`Add` / `Modify` / `Remove`;
  a soft delete of an `IArchivable` is a `Remove`) and the `Errors` added so far; `AddError(key, message)` takes the property path, `""` for the whole entity
- The message is yours to choose: a text the client shows, or a translation key it pairs with its own messages
  (`ValueTooLarge`) — the Regira front-end shows the translation when it has one and the message as is otherwise.
  `AddError(key, message, args)` adds the values a translation fills in — an anonymous object, `new { max = 20 }`, or
  a dictionary; they go out in the 400's [`errorDetails`](built-in-features.md#input-exceptions), scalar values only
- On `Remove`, `Item` is the row as stored — the caller's instance when none is found — so a delete by key,
  `Remove(new Order { Id = id })`, is judged by the row's state
- Validators read and never write — `ctx.Item` is the instance that gets saved, so a value a validator sets is still
  written; changing the entity is a prepper's job. A primer runs later, on `SaveChanges()`, so a value a primer
  stamps is not there yet
- A custom write service passes `IEnumerable<IEntityValidator>` to the `EntityWriteService` constructor (the
  constructor without it runs no validators); a service over another store calls
  `validators.ValidateItem(item, original, operation)` itself — for a delete, with the stored row — and
  `validators.AnyApplyTo(item.GetType())` tells whether any of them can refuse the write. Startup validation warns
  about a write path that cannot run them
- An override of `Remove` that skips `base.Remove` skips the validators; mark extra rows for a delete in an override
  of `RemoveItem`, which runs once the validators passed
- A validator scoped wider than what it checks — to `IEntity`, say — implements `ISelectiveEntityValidator` and its
  `Covers(entityType)`, so it runs only for the types it covers and startup validation counts it only for those
- Unit-test a validator with an `EntityValidatorContext<TEntity>(operation, item, original)`: run `Validate` on it
  and read its `Errors`
- `AbstractValidator` rules run in this stage through the [FluentValidation adapter](built-in-features.md#validators)

| Scope | Runs for |
|-------|----------|
| `EntityValidatorBase<Order>` | `Order` only |
| `EntityValidatorBase<Party>` | `Person` and `Organization`, whichever service saves them |
| `EntityValidatorBase<IHasCode>` | every entity implementing `IHasCode` |

<!-- no-compile -->
```csharp
// interface
public interface IEntityValidator<in TScope> : IEntityValidator
{
    Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);
}
// base class
public abstract class EntityValidatorBase<TScope> : IEntityValidator<TScope>
    where TScope : class
{
    public virtual bool CanValidate(TScope item) => true;
    public abstract Task Validate(IEntityValidatorContext<TScope> context, CancellationToken token = default);
}
```

<!-- no-compile -->
```csharp
.For<Order>(e =>
{
    e.Validate(ctx =>
    {
        if (ctx.Operation == EntityWriteOperation.Remove && ctx.Item.Status == OrderStatus.Shipped)
            ctx.AddError(nameof(Order.Status), "A shipped order cannot be deleted.");
    });
    e.Validate(async (ctx, db, token) =>
    {
        // db sees every row: with scoped reads (tenants, owners), check through the filtered read service (above)
        if (ctx.Operation != EntityWriteOperation.Remove && !await db.Customers.AnyAsync(c => c.Id == ctx.Item.CustomerId, token))
            ctx.AddError(nameof(Order.CustomerId), $"Customer {ctx.Item.CustomerId} does not exist.");
    });
    e.AddValidator<OrderStatusValidator>();
})
```

<!-- no-compile -->
```csharp
using Regira.Entities.DependencyInjection.Validators;   // AddValidator on the options and on IServiceCollection

// global: one validator for every entity implementing the interface
services.UseEntities<AppDbContext>(o =>
{
    o.AddValidator<CodeValidator>();
    // or a delegate: ctx => …, async ctx => …, or async (ctx, db, token) => … with AddValidator<AppDbContext, IHasCode>
    o.AddValidator<IHasCode>(ctx =>
    {
        if (ctx.Operation != EntityWriteOperation.Remove && ctx.Item.Code?.Contains(' ') == true)
            ctx.AddError(nameof(IHasCode.Code), "A code has no spaces.");
    });
});
// the same overloads extend IServiceCollection, for a registration outside UseEntities()
services.AddValidator<CodeValidator>();

public class CodeValidator : EntityValidatorBase<IHasCode>
{
    public override Task Validate(IEntityValidatorContext<IHasCode> ctx, CancellationToken token = default)
    {
        if (ctx.Operation == EntityWriteOperation.Remove)
            return Task.CompletedTask;
        if (string.IsNullOrWhiteSpace(ctx.Item.Code))
            ctx.AddError(nameof(IHasCode.Code), "A code is required.");
        else if (ctx.Item.Code.Length > 20)
            ctx.AddError(nameof(IHasCode.Code), "TooLong", new { max = 20 });
        return Task.CompletedTask;
    }
}
```

### Entity Primers

- Run by an EF Core `SaveChangesInterceptor` when the DbContext saves
- Run on both `SaveChanges()` and `SaveChangesAsync()`. On the synchronous call a primer that awaits is waited on
  without the caller's synchronization context — its own awaits do not deadlock the caller, but it holds the calling thread for its I/O, so
  prefer `SaveChangesAsync()` when primers do I/O
- The interceptor is wired into the DbContext options automatically by `UseEntities(e => e.UseDefaults())`;
  without `UseDefaults()`, select it with `e.WireDbContext(DbContextWiring.PrimerInterceptors)`
- Can be registered **globally** (apply to an interface or base type) or **per entity**
- Timestamp primers (`HasCreatedDbPrimer`, `HasLastModifiedDbPrimer`) write UTC values by default; the
  auto-wired UTC date convention (`UseDefaults()`) makes dates read from the database materialize as
  `DateTimeKind.Utc` and serialize to JSON with the `Z` suffix; `ConfigureDefaultJsonOptions()` reads
  request-body `DateTime` properties as UTC too, so a prepper sees both sides on one clock (standalone EF: `.AddUtcDateTimeConvention()` /
  `SetUtcDateTimeConvention()` — `Regira.DAL.EFcore.Extensions`). Disable UTC handling with
  `UseEntities(e => e.UseUtc(false))` → local time, values used as given; the convention's converter follows
  the same policy (one process-wide decision: `Regira.Utilities.DateTimeDefaults.UseUtc`, on by default)

<!-- no-compile -->
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

### Entity Reactors

- Run once the changes of a save are **committed** — the place for side effects that must not happen for a write
  that fails or rolls back: sending mail, calling another system, enqueueing a background job, starting a
  follow-up workflow when a status changes. What must be part of the save itself stays a primer
- Committed means: at once for a save that commits on its own, at `Commit()` for the saves inside an explicit
  `BeginTransaction()` — also for contexts sharing that transaction through `UseTransaction`, whichever of them
  commits it — and when an ambient `TransactionScope` completes. A failed save, a rollback, or a transaction
  disposed without committing reacts to nothing
- Not seen: a rollback to a savepoint — the reactions of the saves made after it still run — and a transaction
  committed outside EF, on the `DbTransaction` itself — its reactions never run. A transaction begun outside EF and
  handed to `UseTransaction` is known to have ended only when it commits or rolls back through EF: on Npgsql, which
  reuses the transaction object of a pooled connection, one disposed without either leaves its reactions to the
  next such transaction on that connection
- Receive an `IEntityChange<TEntity>`: `Kind` (`Added`/`Modified`/`Deleted` — a soft delete is `Modified`),
  `Entity` (the committed row, generated keys filled in), `Original` (the row as stored before the save) and
  `ChangedProperties`, with the `HasChanged(x => x.Status)` and `ChangedTo(x => x.Status, value)` helpers. Values
  are detached snapshots without navigations
- The stored values come from the entity as it was loaded — by a tracking query, `Modify` or the `Related()` sync.
  Every other write (`Update()` of a detached entity, a stub `Attach` or `Remove`, a delete through the service)
  has its rows read during the save, one query per entity type — only for entity types a reactor is registered for.
  `ExecuteUpdate` / `ExecuteDelete` bypass the change tracker, so no reactor sees them
- Run in process, inside the call that commits, which returns once they have run: `SaveChanges()` for a save that
  commits on its own; `Commit()` / `CommitAsync()` for the saves inside `BeginTransaction()`, whose own
  `SaveChanges()` returns before anything reacts; the `Dispose()` that ends a completed `TransactionScope`, which
  runs them synchronously
- Run in registration order, in a DI scope of their own with a fresh `DbContext` — a reactor that writes saves its
  own unit of work, and that save runs the reactors of what it wrote (up to 8 levels deep). Hand slow work to a job
  system
- A reactor that throws is logged and skipped: the save still succeeds and the other reactors still run
- Wired into the DbContext options by `UseEntities(e => e.UseDefaults())`; without `UseDefaults()`, add
  `DbContextWiring.Reactors` to `e.WireDbContext(...)`
- Can be registered **globally** (`options.AddReactor<T>()` — a reactor on an interface or base type reaches every entity it covers) or **per entity** (`e.AddReactor<T>()` — that entity only, whatever type the reactor is written against)

<!-- no-compile -->
```csharp
services.UseEntities<MyDbContext>(e => e.UseDefaults())
    .For<Order>(e =>
    {
        // inline: the second argument is the reaction's own scoped service provider
        e.React(x => x.Status, OrderStatus.Shipped, (change, services, token) =>
            services.GetRequiredService<IOrderMailer>().SendShipped(change.Entity.Id, token));
        // class-based
        e.AddReactor<OrderInvoicingReactor>();
    });

public class OrderInvoicingReactor(IOrderInvoicer invoicer) : EntityReactorBase<Order>
{
    public override bool CanReact(IEntityChange<Order> change) => change.ChangedTo(x => x.Status, OrderStatus.Delivered);
    public override Task React(IEntityChange<Order> change, CancellationToken token = default)
        => invoicer.CreateFor(change.Entity.Id, token);   // IOrderInvoicer: the app's own service
}
```

## Dependency Injection

### Configuration Example

This example demonstrates how to configure entities with all helper services:

<!-- no-compile -->
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

### Replacing the repository app-wide

`options.UseRepository(...)` swaps the default `EntityRepository` for your own generic repository in every
`For<>()` that does not name one itself (`e.HasRepository<T>()` and `e.UseEntityService<T>()` still win).
Pass the classes unbound; each is matched to a `For<>()` by its number of type parameters, the same as the
`EntityRepository` it derives from — `AppRepository<TEntity>` serves `For<TEntity>()`, `AppRepository<TEntity, TKey>`
serves `For<TEntity, TKey>()`, and so on up to the five-parameter complex shape.

```csharp
using Regira.Entities.EFcore.Services;

public class AppRepository<TEntity, TKey>(
    IEntityReadService<TEntity, TKey, SearchObject<TKey>> readService,
    IEntityWriteService<TEntity, TKey> writeService)
    : EntityRepository<TEntity, TKey>(readService, writeService)
    where TEntity : class, IEntity<TKey>
{
    public override Task Save(TEntity item, CancellationToken token = default)
    {
        // behaviour shared by every entity
        return base.Save(item, token);
    }
}

// For<TEntity>() with an int key
public class AppRepository<TEntity>(
    IEntityReadService<TEntity, int, SearchObject<int>> readService,
    IEntityWriteService<TEntity, int> writeService)
    : AppRepository<TEntity, int>(readService, writeService), IEntityRepository<TEntity>
    where TEntity : class, IEntity<int>;
```

<!-- no-compile -->
```csharp
services
    .UseEntities<MyDbContext>(options =>
    {
        options.UseDefaults();
        options.UseRepository(typeof(AppRepository<>), typeof(AppRepository<,>));
    })
    .For<Product>()                 // AppRepository<Product>
    .For<Category, Guid>();         // AppRepository<Category, Guid>
```

To see every read and write — logging, timing, auditing — override every member the repository has: `Add`,
`Modify`, `Save`, `Remove` and `SaveChanges`; both `Details` overloads; `List` and `Count` with the search object
and with an `object`, which the generated controllers use to look a row up before a save or a delete; and, on the
complex shapes, `List` and `Count` with a list of search objects. Each member hands its call straight to the read or
write service, so one call reaches one member. The exception is `Details(id, archived)`: it returns `Details(id)`
when the filter changes nothing — none given, or an entity that is not `IArchivable` — so log there only when it does
not hand over. `Save` never passes through `Add` or `Modify`. Keep the shared logic in one class that each shape's
repository calls.

A `For<>()` whose shape has no matching class keeps the default `EntityRepository`, and the startup validation
logs a warning naming those entities. A class that cannot serve an entity — a type constraint the entity does
not meet, or a missing interface of the `EntityRepository` it replaces — throws at that `For<>()`.

**Tip**:

<!-- no-compile -->
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
1. **[Services](services.md)** — Implementing entity services, repositories and the write pipeline
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
