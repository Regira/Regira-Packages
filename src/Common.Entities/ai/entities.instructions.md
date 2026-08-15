# Regira Entities AI Agent Instructions

> A generic, extensible framework for managing data entities in .NET with standardized CRUD operations, filtering, sorting, and includes.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Entities` | `Regira.Entities` | Shared abstractions and interfaces |
| `Entities.EFcore` | `Regira.Entities.EFcore` | EF Core `EntityRepository` |
| `Entities.Web` | `Regira.Entities.Web` | ASP.NET Core Endpoints |
| `Entities.DependencyInjection` | `Regira.Entities.DependencyInjection` | `UseEntities()` / `.For<>()` DI builder |
| `Entities.Mapping.Mapster` | `Regira.Entities.Mapping.Mapster` | Mapster integration |
| *`Entities.Mapping.AutoMapper`* | *`Regira.Entities.Mapping.AutoMapper`* | *AutoMapper integration (deprecated)* |

Always prefer clear, conventional patterns over clever solutions. Default to the more feature-rich options when in doubt. Use the latest .NET version (net10) unless instructed otherwise.

---

## License requirement

`Regira.Entities.DependencyInjection` validates a license key at startup (product code `regira.entities`). **The free tier needs no call — omit `UseRegira()` entirely.** To apply **paid** keys, register them **once** via `UseRegira()` before calling `UseEntities()`:

```csharp no-compile
// Program.cs — before UseEntities
services.UseRegira(configuration); // paid keys only — omit this line on the free tier

services.UseEntities<AppDbContext>(options =>
{
    // no license key here — resolved from the registered License
    options.UseDefaults();
});
```

Without a key, the **free tier** applies automatically — no configuration for dev/CI: **5 simple + 2 complex registrations**, two independent buckets (counted separately, not a shared pool of 7), one slot per distinct entity type, and one extra *simple* slot per attachment owner (the per-owner join entity via `HasAttachments`; the shared `Attachment` base via `WithAttachments` is free infrastructure, however many owners use it). Exceeding either bucket throws at startup naming the offenders. **Classify each entity and tally the budget before scaffolding — the decision table, the simple/complex definitions, and a worked example live in [§Step 0](#step-0--classify-every-entity-before-scaffolding), the canonical spot.**

Store paid keys under `Regira:LicenseKeys` in `appsettings.json`. A single key can cover multiple products; add more keys to the array to combine them — the system picks the best per product (paid always wins over free). Obtain a key at [https://regira.com/licensing](https://regira.com/licensing).

---

## Quick Agent Playbook

**Create project:** → [`entities.setup.md`](./entities.setup.md)

**Add entity:** → §Step 0, then §Entity Implementation Workflow

**Modify entity:** update the entity + `DbSet`/relationships, then propagate to whatever the change touches — DTOs/mapping, `SearchObject`/enums/query builders (filters & sorting), processors/preppers/normalizers (behavior).

**Reading guide:** read §Step 0 and §Entity Implementation Workflow before the first entity, then §Steps 1–5 / §Steps 6–10 / §Steps 11–15 as the happy path sends you into them. Everything from §Custom Entity Services on is a lookup table — fetch a section when you have the symptom, not up front. The one exception is §Security & Authorization: **if any row belongs to a user or tenant, read it before you design entities** — row scoping constrains DTOs, preppers and query builders, and retrofitting it is expensive. Exact signatures: `get_type` / [`entities.signatures.md`](./entities.signatures.md), never guessed.

---

## References

**Namespaces:** [`entities.namespaces.md`](./entities.namespaces.md) — never guess, invent, or assume a namespace.

**Signatures:** [`entities.signatures.md`](./entities.signatures.md) — never guess method names, parameter types, or return types; always verify here.

---

## Core Understanding

**The moving parts:** POCO entity (`IEntity<TKey>`) → `IEntityService` (default `EntityRepository`, DbContext-backed) → `EntityControllerBase`, with a read `TDto` / write `TInputDto` pair and five pipeline extension points: **QueryBuilders, Processors, Preppers, Primers, AfterMappers**.

### Generic Type System

| Type | Required | Purpose | Default (when omitted) | Example |
|---|---|---|---|---|
| TEntity | ✓ | The entity class | - | `Product` |
| TKey | ○ | Primary key type | `int` | `Guid`, `int` |
| TSearchObject | ○ | Advanced filtering | `SearchObject` | `ProductSearchObject` |
| TSortBy | ○ | Sorting enum | `EntitySortBy` | `ProductSortBy` |
| TInclude | ○ | Navigation properties enum | `EntityIncludes` | `ProductIncludes` |
| TDto | ○ | Read/display model (details & lists) | `TEntity` | `ProductDto` |
| TInputDto | ○ | Create/update model | `TEntity` | `ProductInputDto` |

> **`EntitySortBy`/`EntityIncludes` are the default *types*, not request-driven behavior on simple entities.** A **simple** registration applies one **fixed** `SortBy(q => q.OrderBy(...))` order and ignores the request's `?sortBy=`; `?includes=` is likewise not bound on simple List/Search. A gated `Includes(...)` navigation therefore loads only on `Details` (which passes `EntityIncludes.All`) — on List/Search it stays off unless you eager-load it *unconditionally*. To let a client pick the sort key or opt into individual navigations per request, promote to the **complex** `For<…, TSortBy, TIncludes>()` path (`SortBy((q, key) => key switch { … })` + typed `[Flags]` includes) — only complex bases bind `?sortBy=`/`?includes=`.

### Processing Pipelines

**Read Pipeline:**
```
EntitySet → QueryBuilders (Filters → Sorting → Paging → Includes) → Processors → Mapping → AfterMapping*
```
*AfterMapping is only executed in API controllers

**Write Pipeline:**
```
Input → Mapping* → AfterInput* → Preppers → SaveChanges → Primers (Interceptors) → Submit
```
*Only executed in API controllers

**`SaveChanges()` is explicit.** Base controllers call it for you; direct `IEntityService` callers (seeding, jobs, custom services) must `await service.SaveChanges()` themselves.

---

## Decision-Making Guidelines

**Inline vs separate class.** Inline for simple (<10 lines), entity-specific, non-reusable logic. Separate class when it needs DI (DbContext/services), is reused across entities, or warrants isolated testing.

**Extend, don't add endpoints.** Reach for a `SearchObject` property before a custom controller action; add actions only when the base methods genuinely can't express the operation.

**Default `EntityRepository` vs a wrapping service.** Stay on the default while the custom logic fits a QueryBuilder / Processor / Prepper / Primer. Wrap (`EntityWrappingServiceBase`) when the behavior sits *around* the call rather than inside the pipeline — caching, auditing, cross-entity validation, combining data sources.

---

## Project Creation Workflow

*(One-time project bootstrap. Distinct from the per-entity `Step N` scheme below.)*

Full procedure — packages, `Program.cs`, DbContext, DI extension method: [`entities.setup.md`](./entities.setup.md). Defaults unless instructed otherwise: **net10**, **SQLite** + `Database.EnsureCreated()` (no initial migration), **Mapster**, per-entity folder structure, default `EntityRepository`.

The two rules that are easy to get wrong and that `entities.setup.md` assumes you already know:

> **`IEntityServiceCollection<T>` vs `EntityServiceCollection<T>`:** `UseEntities<TContext>()` returns the concrete `EntityServiceCollection<TContext>`, which implements `IEntityServiceCollection<TContext>`. Write your extension methods with the **interface as the `this` parameter** (`this IEntityServiceCollection<TContext> services`) but declare the **return type as the concrete `EntityServiceCollection<TContext>`** — every `For<>()` overload already returns the concrete type, and only the concrete type implements `IServiceCollection`. This means chains can be assigned to or returned as `IServiceCollection` without any extra unwrapping call.

> **`EntityServiceCollection<TContext>` implements `IServiceCollection`** — it inherits from `ServiceCollectionWrapper : IServiceCollection`, so returning it from a method typed `IServiceCollection` compiles directly. You can also call `.GetServices<TContext>()` to explicitly unwrap to `IServiceCollection` if needed, e.g. when the chain is broken across multiple statements.

---

## Step 0 — Classify every entity before scaffolding

> The canonical classification/budget spot — the card and §License requirement point here. Three decisions per
> entity, then a budget tally. Do this for the **whole domain** before scaffolding anything.
>
> | Decision | Pick | Consequence |
> |---|---|---|
> | **Addressable or owned?** | independently addressable **entity**, or **owned child** (order lines, join / share / member rows) | Owned child → `e.Related()` on the parent, **no `.For<>()`, no controller, no budget slot**; the SPA edits it inside the parent form. Give it its own `.For<>()` + controller only when clients must address its rows outside the parent. Deciding late is the most expensive rework (§Relationship Patterns — Decision Table). |
> | **Simple or complex?** | **simple** = `For<>()` without `TSortBy`/`TIncludes`; **complex** = `For<…, TSortBy, TIncludes>()` | Sets the `.For<>()` overload, the `EntityControllerBase<>` generics (Step 13), and which endpoints exist. Complex is required for typed sorting/includes and the batch `POST /list` & `POST /search`; `GET /search` (with `count`) exists for simple too. An attachment owner's `HasAttachments` join entity is classified **simple**. |
> | **⚠️ Who writes it?** | one writer per save path | A parent's `Related()` sync and the child's own `IEntityService<T>` coexist **only while the parent's input DTO leaves that collection `null`** (the sync then no-ops). Send it and the parent wins — `[]` deletes every row, and fields missing from the child input DTO reset to default: silent data loss. Startup validation warns for top-level `Related()` pairings only (a nested one isn't detected). Need an owned child's own endpoint (a per-row toggle)? Supported — keep the collection off the parent's input DTO and give each field a narrow route. |
>
> **Collection-level exception:** a field meaningful only relative to its siblings (`SortOrder`) *must* travel
> on the parent DTO, so the collection can't be omitted — guard per-row fields with a `Prepare` hook instead
> ([`entities.patterns.md`](./entities.patterns.md) → *Owned children that are both sortable and individually togglable*).
>
> **Budget tally (free tier = 5 simple + 2 complex, two independent buckets — not a shared pool of 7).** One
> slot per distinct entity type; the built-in attachment feature costs one extra *simple* slot per owner (the
> per-owner join entity registered by `HasAttachments`). The shared `Attachment` base that `WithAttachments`
> registers is **free infrastructure** — it costs nothing, however many owners use it. Write the tally as a
> comment block atop your `Add{Entities}()` extension:
>
> | Entity | Classification | Running tally |
> |---|---|---|
> | `Category` | simple | 1/5 simple |
> | `Product` | simple | 2/5 simple |
> | `Order` | complex (typed `TSortBy`/`TIncludes`) | 1/2 complex |
> | `OrderLine` | owned child via `e.Related()` — no slot | — |
> | `Attachment` | shared base via `WithAttachments` — no slot | — |
> | `ProductAttachment` | simple (`HasAttachments` join) | 3/5 simple |
>
> **→ 3 simple / 1 complex → fits free.** Confirm the tally at runtime: the app logs
> `Regira.Entities: 3 simple / 1 complex registered → tier = free` at `Information` on every start.

### Step 0 overflow — what to actually do

⚠️ **The free tier caps at 7 registrations in total (5 simple + 2 complex), and the buckets don't lend.** "Promote one to complex" only helps while the complex bucket has room — it *moves* a slot, it never creates one. A domain with 8+ addressable entity types therefore cannot be rescued by re-classifying at all; it needs a model change or a key. Apply in this order:

| Remedy | Cost | Use when |
|---|---|---|
| **Role-discriminated actor** — one `Person` / `Party` / `Stakeholder` entity with a role enum (or TPH subtype) instead of `Customer` + `Supplier` + `Employee` | none — this is usually the *better* model, since one human is often several roles | several entities share ~the same fields and differ only in who they are to you. Ready-made slice: [`entities.blueprints.md`](./entities.blueprints.md) — **Stakeholders** |
| **Demote to owned child** (`e.Related()` on the parent) | ⚠️ loses its queryable endpoints — no `/search`, no paging, no filtering of its own rows; it is only reachable through the parent | rows are never addressed outside their parent (order lines, join rows, per-parent settings) |
| **Promote simple → complex** | one complex slot; gains typed sorting/includes | the simple bucket is full **and** the complex bucket has room |
| **Paid key** | `UseRegira(configuration)` — §License requirement | none of the above fits the domain |

**Worked overflow.** A field-service brief names `Customer`, `Supplier`, `Technician`, `Site`, `Asset`, `WorkOrder`, `WorkOrderLine`, `Invoice`. Registered naively that is **8 simple** against a cap of 5 — and promoting the two allowed complex entities still leaves 6 simple. Re-classification alone cannot fix it.

| Move | Running total |
|---|---|
| start — one `.For<>()` per name | 8 simple / 0 complex ❌ |
| `WorkOrderLine` → owned child via `e.Related()` on `WorkOrder` | 7 simple ❌ |
| `Customer` + `Supplier` + `Technician` → one `Party` + `PartyRole` enum | 5 simple ✅ (at the cap) |
| `WorkOrder` → complex (typed sort + includes) | **4/5 simple** (`Party`, `Site`, `Asset`, `Invoice`) + **1/2 complex** ✅ |

The merge is the move that actually created capacity; the other two only rearranged it.

---

## Entity Implementation Workflow

> Every **→ See** pointer in the steps below is one call away: `get_example(id: "Regira.Entities", pattern: "<topic>")` returns just that section.

> Resolve every namespace from [`entities.namespaces.md`](./entities.namespaces.md) **before** writing the first entity — guessing one is the most common early misstep.

**Order of work:** §Step 0 (classify + tally) → the happy path below → only the optional steps a row in the table sends you to → §Steps 11–15 (wiring + runtime verification, never skipped). The step detail lives in three sections you can fetch on their own: §Steps 1–5, §Steps 6–10, §Steps 11–15.

### Minimal entity (happy path)

Steps 1–15 below are the **full menu** (optional steps included) — treat them as a reference index; this list is the fast path through it.
Most entities are *simple* and need only the steps below — **skip everything else** unless a row in the optional-steps table applies. This yields full CRUD + List + paging:

1. **Entity** — POCO implementing `IEntityWithSerial` (int key) or `IEntity<TKey>` (Step 1).
2. **DTOs** — `XDto` (read) and `XInputDto` (write) (Step 5).
3. **Register** — `services.For<X>(e => { });` in a per-entity DI extension method (Steps 12 & 14).
4. **Controller** — `public class XController : EntityControllerBase<X, XDto, XInputDto>;` (Step 13).
5. **DbContext** — add `public DbSet<X> Xs => Set<X>();` plus any relationship config (Step 11).
6. **Wire it up** — call your extension method inside `UseEntities<TContext>(…)` (Step 14).
7. **Verify at runtime** — run the app and walk the runtime checklist (Step 15); a green build proves almost nothing here.

A complete copy-paste slice (every file, in order) is in [`entities.examples.md`](./entities.examples.md) —
**Supplier** (simple) / **Order + OrderLine** (complex). Keep register/controller/inject aligned as you go
(N / N+2 / N) — copy a tier from the alignment card in Step 13.

Need custom filters? Add a `SearchObject` (Step 2) and switch to the still-simple
`services.For<X, int, XSearchObject>()` with controller `EntityControllerBase<X, int, XSearchObject, XDto, XInputDto>`.
Add `SortBy`/`Includes` enums, query builders, processors, preppers, primers, normalizers, or after-mappers **only** when an optional step below calls for them — and note that typed sorting/includes make the entity *complex* (Step 0).

> **⚠️ For a pager, use `/search`, not List.** `GET` (List) → `ListResult` (items only, no count); `GET /search`
> → `SearchResult` with `count`. Both endpoints exist on simple **and** complex, so a simple entity can page too.
> Complexity buys typed sorting/includes and the batch `POST /list` & `POST /search` — not the count.

The table below lists the optional steps; the required steps (1–6, 11–15) follow.

| Step | Default | Add it when |
|---|---|---|
| 7. Processors | Skip by default | You need to fill `[NotMapped]` or other derived values after fetching from the database |
| 8. Preppers | Skip by default | You must compute totals/codes/FKs, validate a required FK exists (→ 400 not 500), or manage child collections before the entity reaches EF Core |
| 9. Primers | Skip by default | You need EF Core interceptor behavior during `SaveChanges()` or transaction-aware stamping across modified entities |
| 10. Mapping & AfterMappers | Skip extra mapping config by default | DTO enrichment needs an after-mapper (`UseMapping<…>().After(...)`), or a nested/child mapping needs help Mapster's convention can't infer (`AddMapping<TSource, TTarget>()`) |

**Mnemonic:** Preppers run synchronously inside `Add()` / `Modify()` / `Save()`, *before* the change tracker — so computed values (totals, codes, FKs) are set the moment `await service.Add(item)` returns. Primers run later, in the `SaveChanges` interceptor, and can inspect every entity in the transaction.

---

## Steps 1–5 — Model the entity: interfaces, SearchObject, enums, DTOs

*The shape of the data and what the client may send. Classify first (§Step 0) — Steps 3–4 are what make an entity **complex**.*

### Step 1: Create Entity Model

- Use `SetDecimalPrecisionConvention` in DbContext instead of setting precision per property
- `DateTime` values round-trip as UTC automatically (JSON `Z` suffix): the UTC date convention is auto-wired by `UseEntities(e => e.UseDefaults())`. Standalone EF without the entities stack: `.AddUtcDateTimeConvention()` in `AddDbContext` or `SetUtcDateTimeConvention` in `ConfigureConventions`
- Nullable: follow interfaces when type is nullable, nullable properties can be combined with [Required] annotation

**Interface selection checklist:**

| Interface | Add when… |
|---|---|
| `IEntityWithSerial` | int primary key (auto-increment). Shortcut for `IEntity<int>` |
| `IEntity<TKey>` | Non-int primary key (e.g. `Guid`) |
| `IHasTimestamps` | Track Created + LastModified (stored as UTC by default) |
| `IArchivable` | Soft-delete — ⚠️ `DELETE /{id}` then flags the row instead of erasing it; the flagged rows are hidden by the archived query filter, auto-wired by `UseDefaults()` (Step 11); full round-trip in [`entities.patterns.md`](./entities.patterns.md) → Soft Delete |
| `IHasTitle` | Entity has a short display name |
| `IHasDescription` | Entity has a long text field |
| `IHasCode` | Entity has a short unique code |
| `ISortable` | Used as a sortable child collection |
| `IHasNormalizedContent` | Entity uses normalized text for search |
| `IHasAttachments` | Entity can have file attachments |

> **`IHasTitle` is getter-only in the interface** (`string? Title { get; }`). Implementing entities must declare `{ get; set; }` to allow writes — C# permits this even when the interface only specifies a getter. Declaring just `{ get; }` on the entity makes the property read-only and will cause compile errors whenever you try to assign it.

> **`IHasNormalizedContent` is all-or-nothing.** The global `Q` filter AND-s a `NormalizedContent` match for every entity that implements it, so declaring it but leaving `NormalizedContent` unpopulated (no `[Normalized]`/normalizer) makes `Q` match nothing and searches return empty. Populate it, or don't implement the interface. The attribute belongs on the **property** — `[MaxLength(1024), Normalized(SourceProperties = [nameof(Title), nameof(Description)])] public string? NormalizedContent { get; set; }` — see §Normalizing for why the class-level form silently normalizes nothing.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity

### Step 2: Create SearchObject

- Inherit from `SearchObject` and add filter properties as needed.
- Prefer using `ICollection<TKey>` for FK filters to allow multiple values.

```csharp
// Base record (SearchObject is shortcut for SearchObject<int>)
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
    public ArchivedFilter? Archived { get; set; }   // Excluded (default) | Included | Only
}
```

> **Add only *new* filter properties** — the members above are already on the base. Re-declaring one (e.g. `Archived`, `Q`) shadows it, which silently breaks the built-in global filters: they read the value through `ISearchObject<TKey>` (the empty base slot) while model binding fills the shadowed one.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity

### Step 3: Create SortBy Enum

- `SortBy` is a plain (non-`[Flags]`) enum
- A request may carry several sort values (`List(...)` accepts `IList<TSortBy>`, applied left to right). The framework calls your `SortBy(...)` builder lambda once per value, each time passing the query already ordered by the previous values — so the lambda receives a single `TSortBy?` and continues the order. See the Product example.

> **⚠️ Order via `query.OrderOrThenBy(...)` / `OrderOrThenByDescending(...)`** (`Regira.Entities.EFcore.Extensions`) — they start the ordering or continue it with `ThenBy` as appropriate. Never branch on `query is IOrderedQueryable<T>` yourself: EF Core queries satisfy that check *before* any ordering is applied, so the intuitive guard compiles, passes DI validation, and throws `Expression of type 'IQueryable<T>' cannot be used for parameter of type 'IOrderedQueryable<T>' of method 'ThenBy'` on the first sorted request.
>
> **Scope:** this is about the *typed* `TSortBy` builder, which is invoked once per requested sort value and must therefore continue an order it may not have started. A **simple** registration's `e.SortBy(query => …)` runs once for the whole query, so plain `OrderBy`/`ThenBy` is correct there — which is what the Category example uses.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Product entity

### Step 4: Create Includes Enum

> **Decision:** must a client pick *individual* navigations or sort keys per request? → **complex** `For<…, TSortBy, TIncludes>()` with domain `[Flags]`/sort enums (only these bind `?includes=`/`?sortBy=`). Otherwise → **simple**: one fixed sort, and gated navigations load on `Details` only. Either way, gate each navigation behind the flag so List/Search stay lean.

- `EntityIncludes` is minimal (`Default`, `All`) — define a domain-specific `[Flags]` enum when you need named flags (`Categories`, `Parents`, etc.) and use it consistently in `.For<>()`, controllers, processors, and `IEntityService<>` injections
- **`Details(id)` loads *all* includes; `List(...)` loads only those passed to it (none by default).** This is a service-layer default of the built-in `EntityReadService`/`EntityRepository` — `Details` applies the **OR of every defined flag** of `TIncludes` (alias members like `All = A | B` are fine), so registering a relation in `Includes(...)` makes it load automatically on Details. The controller `Details`/`List`/`Search` endpoints inherit this — on a **complex** base a client opts into List/Search includes via `?includes=` (`Details` ignores the query string); a **simple** base binds no `?includes=`, so a gated navigation there is Details-only. **Tell the front-end**: on complex entities, list/search responses come without nested collections unless the client sends `?includes=` (a `regira` SPA sets it once via the entity config's `baseQueryParams`).

- ⚠️ **"None by default" means the lambda's `includes` parameter is `null`, not the zero flag.** A request with no `?includes=` passes `null`; a client that sends `?includes=Default` passes the zero flag. Both mean "nothing opted in", so gate on the flag alone and they behave identically:
  ✅ `if (includes?.HasFlag(OrderIncludes.Lines) == true) query = query.Include(x => x.Lines);`
  ❌ `if (includes == null || includes.Value.HasFlag(OrderIncludes.Lines))` — the `null` branch loads the collection on **every list row**, which is the heavy list this gating exists to prevent.

- **A to-one shown on every list row loads unconditionally; a collection is flag-gated.** This is the whole includes decision, and it holds on **both** tiers — the simple/complex split changes only whether a client can opt in per request, never which navigations belong in which bucket. A flag on a to-one that every row renders is dead code (it always has to be on); an unconditional collection is a Cartesian row explosion waiting on Details.

- **Order (and further filter) *inside* an include** — EF Core's filtered include composes fine in the `Includes(...)` lambda: `.Include(x => x.Components!.OrderBy(c => c.SortOrder))` and `query.Include(x => x.Facets!.Where(f => f.IsPublic)).ThenInclude(f => f.Facet)`. **Archived children need no hand-written predicate**: the archived filter is an EF query filter, so it propagates into every `Include()` and archived rows stay out of nested collections by default ([`entities.patterns.md`](./entities.patterns.md) → Soft Delete).

> **Simple registrations can eager-load too — mind the asymmetry.** Only the *typed* `TIncludes` overload is
> complex-only; every builder also has the untyped `e.Includes((query, _) => query.Include(...))`. Split by trap:
> - **Simple bases bind no `?includes=`.** A **flag-gated** include on a simple base is *Details-only* — a
>   relation that must appear on **List/Search rows** must be eager-loaded **unconditionally**:
>   ❌ `if (includes?.HasFlag(All) == true) query.Include(x => x.Shopper)` (blank on lists) →
>   ✅ `e.Includes((query, _) => query.Include(x => x.Shopper))`.
> - **Unconditional loads are cheap to-one references — as many as the row genuinely displays — and at
>   most one collection.** References cost a join each and are what list rows are made of, so a list
>   showing a vehicle *and* a supplier eager-loads both. Collections are the constraint: two or more
>   *collection*
>   navigations in one query trip EF Core's `QuerySplittingBehavior` warning (a Cartesian row explosion).
>   Any aggregate root with several owned collections hits this on Details, which loads the OR of every
>   registered include by design — so it is the normal case, not a hierarchy edge case. Fix it where you
>   see it: append `.AsSplitQuery()` inside the `Includes(...)` lambda, or set it once on the provider
>   (`opt.UseSqlite(cs, o => o.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))`). Deeper
>   graphs: [`entities.patterns.md`](./entities.patterns.md) — Hierarchical Data.
> - **Eager-loads go in `Includes(...)`, never `Filter(...)`.** Filters decide *which rows* qualify; includes
>   decide *what's loaded* on them. An eager-load hidden in a filter is invisible to everything that reasons
>   about includes.
> - **`EntityIncludes` (or a domain `[Flags]` enum) is the opt-in contract.** Gate every collection or expensive
>   navigation behind the flag (`if (includes?.HasFlag(EntityIncludes.All) == true) query = query.Include(...)`):
>   it still auto-loads on Details while staying off List/Search until the client opts in.

> **`?includes=` value contract.** Entities on the generic `EntityIncludes` accept only `Default`/`All`; a
> specific member name (`Categories`, `Parents`, …) is accepted only by entities that expose a named `[Flags]`
> includes enum. The front-end scaffold's `--rel` defaults to `["All"]`.

> **⚠️ One `Includes(...)` registration per entity — a second call replaces the first (last-write-wins).**
> The builder registers a *single* `IIncludableQueryBuilder`, so DI resolves only the **last** registration:
> ❌ `e.Includes((q, _) => q.Include(x => x.Owner)); e.Includes((q, i) => …)` silently drops `Owner` from
> every response. Compose **everything in one lambda** — unconditional references first, flag-gated
> collections behind their checks. (Filters differ: every `Filter(...)`/`AddFilter<>()` registration runs.)

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Order + OrderLine entities (typed) / Category & Supplier entities (untyped)

### Step 5: Create DTOs

- Include `Id` in `InputDto` to support the Save (upsert) action
- Exclude normalized fields from DTOs — they are for internal use only
- Exclude auto-generated fields (`Created`, `LastModified`, `NormalizedContent`) from `InputDto`
- Exclude secured fields (e.g. `Password`) from DTOs
- ⚠️ **`IsArchived` is the exception — keep it on `TInputDto`.** It reads like an auto-generated *and* server-owned flag, so it gets excluded by reflex. The write path stays archived-inclusive either way, but a DTO that cannot express the flag can never clear it, so **restore becomes impossible** — and the row is invisible meanwhile (lists hide it, `GET /{id}` 404s). Generated forms hide the field; they don't drop it. Round-trip: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete
- Server-owned fields (generated codes like `Order.Code`, computed totals, and **per-line prices** — an order/invoice line's `UnitPrice` is a textbook price-tampering vector) belong in the manager/prepper — exclude them from `InputDto` and set them server-side (`item.Code ??= …` on create, resolve `line.UnitPrice` from `Product.Price`, restore from `entry.OriginalValues` in a primer on update — recipe below)
- When using Attachments, exclude full File paths, since the FileService accepts relative paths (identifiers)
- Try to facilitate mapping by keeping DTO structure similar to the entity (e.g. nested related entities instead of flattening)
- Use navigation properties in DTOs instead of flattening related entity data: this preserves structure and enables richer client-side handling (e.g. avoid `CategoryTitle`, but use `Category`=>`Title`)
- Only include child collections in `InputDto` when they are configured with `e.Related(...)` — declare them
  **nullable and uninitialized** (`ICollection<LineInputDto>? Lines { get; set; }`), never `= []`. An omitted
  collection then maps as `null` (untouched); an initialized/non-nullable property defaults to `[]` = **delete-all**
  on any save that doesn't send it (a status-only PATCH silently clears the rows)
- Use `AfterMapper` for computed/calculated properties (e.g. URLs, display names) in DTO

> **⚠️ Exclude a server-owned field and restore it in the same edit.** A field absent from `TInputDto` maps
> as `null`/default on every PUT *and* PATCH — 200 OK, silent corruption (a status-only PATCH zeroes a
> computed `Total`). Protect it in a **primer**, exactly as the built-in `HasCreatedDbPrimer` protects
> `Created` — mint on create, restore from `entry.OriginalValues` on update:
> ```csharp no-compile
> public class OrderCodePrimer : EntityPrimerBase<Order>
> {
>     public override Task PrepareAsync(Order entity, EntityEntry entry, CancellationToken token = default)
>     {
>         if (entry.State == EntityState.Added) entity.Code ??= GenerateCode();          // create: stamp once
>         else if (entry.State == EntityState.Modified)                                  // update: keep stored value
>             entity.Code = (string?)entry.OriginalValues[nameof(entity.Code)];
>         return Task.CompletedTask;
>     }
> }
> // e.AddPrimer<OrderCodePrimer>();
> ```
> Owner-stamp from the claim, and computed totals (prepper variant with the full `original`):
> [`entities.patterns.md`](./entities.patterns.md) → Server-owned / immutable fields on update.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity

---

## Steps 6–10 — Pipeline services: filters, processors, preppers, primers, mapping

*All optional except Step 6. Reach for one only when a row in the optional-steps table (§Entity Implementation Workflow) applies — and read §Step 0's "who writes it?" decision before adding anything that touches a child collection.*

### Step 6: Create Query Builders

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Product entity

→ Apply the inline vs separate class rule from §Decision-Making Guidelines.

> **Choosing a base:** inherit **`FilteredQueryBuilderBase<TEntity, TKey, TSearchObject>`** for the common case — it passes the query through unchanged and you override `Build(...)`. Implement the **`IFilteredQueryBuilder<…>`** interface directly only when you want full control with no inherited behavior. Both live in `Regira.Entities.QueryBuilders.Abstractions` (see Troubleshooting if you hit CS0246).

**⚠️ Can't `Filter`/`SortBy` on processor-populated `[NotMapped]` values** — they're filled *after* the SQL fetch, so they aren't in the query. Use the underlying DB expression instead (e.g. `OrderByDescending(x => x.Projects.Count)`, not the processor-filled count field).

> **Don't write a `Q` filter.** Keyword search on `Q` is already global for `IHasNormalizedContent` entities (`UseDefaults()` registers `FilterHasNormalizedContentQueryBuilder`); a per-entity `Q` filter ANDs on top of it and narrows or empties results — see §Filtering with Normalized Content. Keep your query builder for domain filters (category, price, …) only.

> **`SortBy` lambda signature depends on the `For<>` overload:**
> - Simple builders (no `TSortBy` — `For<TEntity>`, `For<TEntity, TKey>`, `For<TEntity, TKey, TSearchObject>`) → `e.SortBy(query => query.OrderBy(...))`
> - Complex builders (with `TSortBy` — `For<TEntity, TSearchObject, TSortBy, TIncludes>`) → `e.SortBy((query, sortBy) => ...)`
>
> Using the two-arg form with a simple builder produces **CS1593**.

> **Builder verb = inline lambda vs registered class.** The bare verbs take an inline **lambda**; the
> `Add*<T>()` verbs register a builder **class**. ⚠️ **An inline lambda receives only its declared arguments —
> there is no DI and no `DbContext` in it** (`Filter` gets `(query, searchObject)`, `Includes` gets
> `(query, includes)`; only `Prepare` has a `(entity, dbContext)` overload). Anything needing an injected
> service — `IQKeywordHelper`, a domain service, another entity's repository — or a cross-entity query belongs
> in the registered-class form, which constructor-injects normally. There is no
> `e.Filter<TService>((q, so, svc) => …)` overload. (`new QKeywordHelper()` inside a lambda *compiles* — its
> constructor parameters are optional — but it silently bypasses the app's configured `QKeywordHelperOptions`
> and normalizer, so its keywords stop matching what was stored. Resolve the service, don't re-`new` it.)
> The complete overload set per verb is in
> [`entities.signatures.md`](./entities.signatures.md) → *`For<>()` overload → builder*.
> Inline: `e.Filter((q, so) => …)` / `e.SortBy(...)` /
> `e.Includes((q, inc) => …)`. Class: `e.AddFilter<TBuilder>()` / `e.AddSortBy<TBuilder>()` /
> **`e.AddIncludes<TBuilder>()`**. `e.Includes<TBuilder>()` (no `Add`) does **not** exist — that name is the
> inline overload, so passing a type argument is **CS0308**. Compose all eager-loads in one
> `Includes`/`AddIncludes` per entity — a second registration replaces the first.

> ⚠️ **`Build` is synchronous** — `IQueryable<TEntity> Build(IQueryable<TEntity>, TSearchObject?)`, no
> `CancellationToken`, no async overload. It composes an expression; it must not execute one. A filter that
> needs data it has to *fetch* (expanding a picked category to its descendants, resolving a permission list)
> has no hook here, and blocking on `.Result` inside `Build` deadlocks or serializes every request. Two
> sanctioned answers: **express it as a subquery** in the same `IQueryable` (`q.Where(x =>
> ctx.Categories.Where(…).Select(c => c.Id).Contains(x.CategoryId))` — one round trip, and recursive shapes
> can use a mapped DB function, see [`entities.blueprints.md`](./entities.blueprints.md) → Recursive
> entities), or **resolve the set before the request reaches the filter** and pass it as an ordinary
> `ICollection<TKey>` on the SearchObject (`?categoryId=1&categoryId=7&…`), which keeps the API honest and
> puts the walk where the graph is already loaded.

### Step 7: Processors (Optional)

⚠️ **A processor runs only in its OWN entity's read pipeline.** It is invoked on the materialized root result of that entity's `Details`/`List` — it never walks into navigation properties. So a `Session` row arriving nested inside `GET /events/1?includes=Sessions` is materialized by **Event**'s read service and never enters `IEntityProcessor<Session, …>`: its `[NotMapped]` values are `null` there, while the same row fetched from `/sessions` carries them. Don't render such a field as `0` on a nested row — check for `null` and show nothing, or fetch the child list separately.

The same scoping applies to **after-mappers** (Step 10): `IEntityAfterMapper` is dispatched against the root source only, so a DTO produced inside another entity's projection does not get its after-mapper fields either.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity (CategoryProcessor) / Additional Patterns > Inline processor

### Step 8: Preppers (Optional)

Use to: manage child collections (if not using `e.Related()`), recalculate totals/codes/FKs before `SaveChanges()`.

⚠️ **A required FK left off `TInputDto` is written as `default` on PUT/PATCH** — restore it from the stored
row like any server-owned field ([`entities.patterns.md`](./entities.patterns.md) → Server-owned / immutable
fields on update). One that *is* on the DTO reaches the database unchecked: an existence check here turns
its `409` into a field-level `400` (`EntityInputException<TEntity>`, §Error Handling).

⚠️ **Stamp a server-owned timestamp or code only when the value is absent**, as `HasCreatedDbPrimer` does —
an unconditional `DateTime.UtcNow` on create overwrites back-dated seed data
([`entities.patterns.md`](./entities.patterns.md) → Back-date Created / LastModified when seeding).

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Prepper

**Variants:** inline (simple), inline with original (create vs update), inline with DbContext, separate class, `e.Related(x => x.ChildCollection)` shortcut.

Two shapes, and the choice is forced by whether you need the stored row:
- `e.Prepare(...)` — the builder delegate. Overloads are `Action<TEntity>` and `Func<TEntity, TContext, Task>`; there is no one-arg async form, so a single-parameter lambda ending in `return Task.CompletedTask;` picks the `Action` and fails **CS8030** — drop the return, or take the `TContext` parameter. The delegate never sees the original entity.
- `EntityPrepperBase<TEntity>` + `e.AddPrepper<T>()` — `Prepare(modified, original, token)`. This is the only shape that can compare against the stored row, so anything create-vs-update (restoring server-owned fields, diffing quantities) belongs here.

**Write-pipeline order** — one save, fixed order. It matters as soon as an entity carries more than one of these, which is the normal case (`e.Related()` *is* a prepper):

| # | Stage | Runs where | Sees |
|---|---|---|---|
| 1 | DTO → entity mapping | the controller / your caller | the request payload |
| 2 | **Preppers, in registration order** — `e.Prepare(...)` delegates, `e.AddPrepper<T>()` classes, and the `Related()` collection sync alike | `IEntityService.Add`/`Modify`/`Save`, **before** `SaveChanges()` | the mapped entity, plus the stored `original` on the `EntityPrepperBase` shape |
| 3 | `SaveChanges()` | your call (base controllers make it for you) | — |
| 4 | **Primers** | an EF `SaveChangesInterceptor`, **inside** the save | the `EntityEntry`, so `entry.State` and `entry.OriginalValues` |

Two consequences worth designing around: register a prepper that must observe a synced collection **after** the `e.Related()` that syncs it, and remember that a primer sees the entity *after* every prepper has finished with it. Only stage 4 runs for a writer that bypasses `IEntityService` and saves through the raw `DbContext` — the reason a field a workflow service legitimately writes belongs in a prepper, never a primer ([`entities.patterns.md`](./entities.patterns.md) → Server-owned / immutable fields on update).

`e.Related()` takes an optional parent-level `prepareFunc` followed by an optional `configure` callback — signature `Related<TRelated, TRelatedKey>(x => x.Collection, prepareFunc?, configure?)`:
- Sync only: `e.Related<TRelated, TRelatedKey>(x => x.Collection, prepareFunc?)` — syncs the collection, optional per-entity prepare.
- Nested: `e.Related<TRelated, TRelatedKey>(x => x.Collection, configure: builder => { ... })` — use `RelatedEntityBuilder` to nest sub-collections (`builder.Related(...)`) or add item-level prepare logic (`builder.Prepare(...)`). Pass `prepareFunc` before `configure` to combine both. Worked two-level example (party relationships carrying their own contact data): [`entities.blueprints.md`](./entities.blueprints.md) — Stakeholders (§Registration).

> **Single-arg shortcut for int-keyed children:** when the related entity has an `int` key, drop the second type argument — `e.Related<TRelated>(x => x.Collection, …)`. This shortcut is available on **all** int-key builders, including the simple `For<TEntity, int, TSearchObject>()` registration. The two-arg form `e.Related<TRelated, TRelatedKey>(…)` is only required for non-`int` related keys (type inference can't deduce `TRelatedKey` from a navigation expression alone — **CS0411** if both args are omitted with a non-int key).

**How the sync classifies an incoming row** — three cases, and the first is what the front end sends:

| Incoming `Id` | Treated as | Note |
|---|---|---|
| `0`, `null`, or **any negative number** | **new** → `INSERT` | A negative id is a client temp key (the Vue `useOwnedCollection` mints them so a row added in the session has a stable `:key`); it is cleared before insert, so the store generates the real one |
| matches an original row | update | fields absent from the child input DTO reset to default |
| positive but matching nothing | **new** → `INSERT`, ⚠️ **with that id** | Only temp (negative) keys are cleared, so a store-generated key arrives as an explicit insert: a silently wrong PK on SQLite, and *"Cannot insert explicit value for identity column"* on SQL Server. It means the client is sending a stale id for a row someone else deleted — reload before saving rather than relying on the server to absorb it. (A **client-assigned** key type — `Guid`, `string` — is never cleared, because there a chosen key is the point.) |

The **parent FK needs no stamping**. New children reach the store through the parent's navigation, so EF assigns the FK once the parent's key is generated — this holds for a brand-new parent saved with children in one call. Setting the FK yourself to a parent id that does not exist yet (`0`) is what breaks it.

### Relationship Patterns — Decision Table

| Relationship | Use `Related()`? | Notes |
|---|---|---|
| Owned child list (e.g., order lines) | ✅ Yes | Child has no own `.For<>()` registration; lifecycle is fully controlled by the parent |
| Optional parent FK (e.g., `InvoiceId?` on Intervention) | ❌ No | Manage via the child entity's own service. A total the parent rolls up from these children is a prepper reading the persisted rows — [`entities.patterns.md`](./entities.patterns.md) § Aggregates over a non-owned child collection. Two consequences decide whether you want this: the total is **eventually consistent** (it settles when the parent is next saved, so moving a child must save the parent too), and **seeding needs a second pass** re-saving every parent |
| Many-to-many join entity | ✅ Yes | The join entity itself is owned; see join-entity example in `entities.examples.md` |
| Independent entity with back-ref collection | ❌ No | Use `Include()` in the query builder to load the navigation property |

> **An owned child/join entity still implements `IEntityWithSerial`** (or `IEntity<TKey>`) — it skips only its
> own `.For<>()` registration, not the entity interface. `e.Related<TRelated>()` constrains `TRelated : IEntity<int>`.

> ⚠️ **One writer per save path.** A parent's `Related()` sync and the child's own `IEntityService<T>` *may* coexist — the registrations don't conflict — but only while **the parent's input DTO leaves the collection `null`**, which makes the sync short-circuit to a no-op. Send the collection and the parent wins: its next save re-diffs and silently overwrites rows the standalone service wrote (`null` = untouched, `[]` = **all rows deleted**). Need independent endpoints for an owned row? That's the supported join-toggle recipe in [`entities.patterns.md`](./entities.patterns.md) — *Single-field PATCH / state toggle*; just keep the collection off the parent's `TInputDto`. Unless the child is also **sortable**: `SortOrder` is collection-level and must ride the parent DTO, so guard the per-row field with a `Prepare` hook instead (same file → *Owned children that are both sortable and individually togglable*).

> **`Related()` children cost no budget slot.** A child collection managed via `e.Related(...)` gets **no
> `.For<>()` and no controller** — it rides on the parent's endpoints. Giving `OrderLine` its own registration
> is the classic way to blow the free-tier bucket for nothing (see Step 0). Editing a m2m join from a SPA:
> bind the join rows through the related entity's picker — see the front-end guide
> (`regira_modules.vue.entities` → patterns → *Editing a many-to-many join*).

> **Set `OnDelete` intent per FK.** Choose the behavior deliberately in `OnModelCreating` — `Cascade` to
> remove dependents with the parent, or `Restrict` so a referenced row can't be deleted. A database
> constraint violation (FK, unique index) surfaces as **409 Conflict** on the base controllers
> (§Error Handling); throw `EntityInputException` from a prepper when the client should get a
> field-level **400** instead.
>
> ⚠️ **A join entity has two FKs and they usually want different behaviour.** On the **owner** side
> (`ProductCategory.Product`) `Cascade` is almost always right — the join rows are part of the product. On the
> **lookup** side (`ProductCategory.Category`) `Cascade` means deleting one category silently strips it from
> every product that had it: 200 OK, no 409, no warning, and the only trace is rows that quietly disappeared.
> Prefer `Restrict` there when you want the delete refused while the lookup is in use. The `ProductCategory`
> example in [`entities.examples.md`](./entities.examples.md) cascades on both sides because both ends are
> disposable there — don't copy it onto a lookup you care about.
>
> ⚠️ **On SQLite that guarantee is off by default.** `Microsoft.Data.Sqlite` leaves `PRAGMA foreign_keys`
> disabled unless the connection string says `Foreign Keys=True`, so `Restrict` never fires and the delete
> succeeds silently — see [`entities.setup.md`](./entities.setup.md) → P3 for the connection string.

### Step 9: Primers (Optional)

Run during `SaveChanges()` via EF Core interceptors; can inspect other modified entities in the same transaction. The interceptor is auto-wired by `UseDefaults()`; without it, select `e.WireDbContext(DbContextWiring.PrimerInterceptors)`.

> ⚠️ **A primer runs on _every_ `SaveChanges()` (it's an EF interceptor); a prepper runs only on the `IEntityService` write path.** So a primer that restores a server-owned field from `entry.OriginalValues` also fires on — and reverts — a domain/workflow service's raw-`DbContext` write. When a second writer legitimately owns a field (a status/state machine), guard it with a **prepper** (`EntityPrepperBase<T>`; `original` is `null` on create, the stored row on update), not a primer. Full treatment: [`entities.patterns.md`](./entities.patterns.md) → Server-owned / immutable fields on update.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Primers

### Step 10: Mapping & AfterMappers (Optional extra configuration)

**Mapping configuration is optional.** With Mapster (the default), `TEntity ↔ TDto`/`TInputDto` mapping — **including nested objects and child collections** — works by convention whenever the DTO shape resembles the entity. Most entities need no mapping statements at all (the Category, Product and Order examples register none and still round-trip their nested collections). Add configuration only for the specific cases below.

- **`UseMapping<TDto, TInputDto>()`** — call it when you need to attach an **after-mapper** or customise the top-level mapping. It registers `TEntity→TDto` + `TInputDto→TEntity` and returns a builder you chain the after-mapper onto:
  - `.After(...)` — enrich the output DTO after `Entity→DTO` mapping (computed properties, URLs)
  - `.AfterInput(...)` — modify the entity after `InputDto→Entity` mapping
  - `.After<TAfterMapper>()` — separate class when DI is needed; `options.AddAfterMapper<T>()` registers a global one. The class-based overload returns the **untyped** builder (drops `TDto`/`TInputDto`), so `.AfterInput(...)` no longer compiles after it (CS1061) — chain the typed `.After(...)`/`.AfterInput(...)` inline when you need both (e.g. a renamed DTO collection — see [`entities.patterns.md`](./entities.patterns.md) — Renamed DTO property)
- **`AddMapping<TSource, TTarget>()`** — an escape hatch. Register an explicit mapping for a specific (usually nested/child) type pair **only** when Mapster's convention produces the wrong result — e.g. a child DTO whose shape diverges from the entity, or a child input type that needs a custom mapping. It is **not** required to project nested related collections; Mapster does that by convention.

> ⚠️ **An after-mapper runs for the read root only** (§Step 7): the same DTO nested inside another entity's projection is mapped without it. So a computed field that must also appear on, say, a `PersonCoreDto` carried by every `TicketDto` cannot come from an after-mapper — put it on the entity as a `[NotMapped]` getter and let Mapster project it. The failure is silent in the worst way: the field populates on `/people` and is blank on every ticket row, board card and comment, and nothing — compiler, DI validation, or a single-page smoke test — reports it.

> **Empty nested collection in a response?** That is almost always a missing **`Includes`** (the navigation was never loaded from the database), **not** a missing mapping — see §Step 4 and Troubleshooting. Check `e.Includes(...)` before adding any `AddMapping`.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Mapping / AfterMapper

---

## Steps 11–15 — Register, expose and verify

*Never skipped. Steps 12–13 are where the generics must line up (register = N, controller = N+2, inject = N) and Step 15 is the only place a wrong pipeline actually shows.*

### Step 11: Update DbContext

Add `DbSet<YourEntity>` and configure any relationships in `OnModelCreating`.

> **Any `IArchivable` entity? Nothing to add here** — `UseEntities<TContext>(e => e.UseDefaults())` wires the archived query filter into the context's options (Step 12), and it is applied after everything `OnModelCreating` configured. ⚠️ Two cases still need a call: a `DbContext` you construct yourself (`new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()…Options)` — tests, design-time factory, seeding tool) takes `.AddArchivedQueryFilter()` on those options, and a setup that opted out of `DbContextWiring.ArchivedQueryFilter` ends `OnModelCreating` with `modelBuilder.SetArchivedQueryFilter()` (`Regira.Entities.EFcore.Extensions`) instead — after your own `HasQueryFilter(...)` calls, exactly once. Startup validation reports a model that ends up without the filter as an error naming the entity. Full round-trip: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete.

> **Then expect an EF warning (`net10.0`) — and read it before suppressing it.** Because a real query filter is installed there, EF logs one `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` per relationship whose required principal is `IArchivable` — *"Entity 'Order' has a global query filter defined and is the required end of a relationship with 'OrderLine'"*. On **`net8.0` it cannot appear at all** (no filter is installed there), so don't go looking for it.
>
> ⚠️ **The warning is benign for an aggregate parent and a silent data bug for reference data.** The filter propagates into `Include(...)`, and where the navigation is required EF composes it as an inner join — so the dependents drop out of **items** while the **count** query (no includes, no join) still counts them. For `Order` → `OrderLine` that is the intent. For a `Category` that fifty separately-registered `Asset` rows point at, archiving one silently removes those fifty from every list while `/search` keeps reporting them: short pages, no error, nothing logged. **Reference data behind a required FK should not be `IArchivable`** — use a real `DELETE` with `OnDelete(Restrict)` (→ 409 while in use), or make the FK optional. Startup validation warns on that shape; the aggregate-parent case is the one the Step 15 "no warnings" checkpoint excuses. Full decision table: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete.
>
> Once you have confirmed it is an aggregate parent, silence it where the context is registered (the options builder, not `OnModelCreating`):
>
> ```csharp no-compile
> using Microsoft.EntityFrameworkCore.Diagnostics; // CoreEventId
>
> builder.Services.AddDbContext<AppDbContext>(opt => opt
>     .UseSqlite(cs)
>     .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning)));
> ```
>
> A dependent you also query **directly** (an aggregate over `OrderLine`, not through `Order`) needs one more thing: give it a matching `HasQueryFilter(x => !x.Order!.IsArchived)` in `OnModelCreating`. That is what keeps its own count and items in agreement — and it never collides with the named archived filter, because the archived filter only touches `IArchivable` types.
>
> ⚠️ That filter also hides the rows from the **parent's own** aggregate recompute, which runs while the parent is still archived — so restoring the parent zeroes a computed total, with a 200 and no error. Any prepper summing such a dependent needs `IgnoreQueryFilters()`, scoped by the parent FK: [`entities.patterns.md`](./entities.patterns.md) § Aggregates over a non-owned child collection.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — DbContext

### Step 12: Configure Entities

Use `.For<TEntity, ...>(...)` to register each entity and configure its services. The generic type arguments determine which features are enabled and which base controller to use.
Child entities configured with `e.Related()` don't need their own `.For<>()` registration.

Before writing the controller, verify the `.For<>()` registration and controller pairing in [`entities.signatures.md`](./entities.signatures.md). The controller must mirror the registration generics exactly.

> **→ See:** [`entities.examples.md`](./entities.examples.md) (All entities)

### Step 13: Configure Web Endpoints

Use `EntityControllerBase`. The generic type arguments on the controller must **exactly match** the type arguments used in `.For<>()`. 
The controller can add `TDto` and `TInputDto` on top. A wrong arity **compiles** — it only surfaces at
startup DI validation (enable `ValidateOnBuild`, see `entities.setup.md`), so copy the pairing from the
table below rather than reasoning it out.

> **`EntityControllerBase` lives in `Regira.Entities.Web.Controllers.Abstractions`.** A bare
> `using Regira.Entities.Web.Controllers;` is **not** enough — the base class is in the `.Abstractions`
> child namespace and the controller won't resolve (CS0246) without it. See
> [`entities.web.namespaces.md`](../../Entities.Web/ai/entities.web.namespaces.md) for the exact `using` set.

> **Keep controller routes resource-relative** — `[Route("[controller]")]` or the resource name (e.g. `[Route("products")]`) — and apply a shared `api` base **once** at host/app level so it stays configurable (`app.UsePathBase`, reverse proxy, or a global route-prefix convention). See [`entities.setup.md`](./entities.setup.md) — API route prefix.

| `.For<>()` registration | Required controller base | Inject as (outside a controller) |
|---|---|---|
| `.For<TEntity>()` | `EntityControllerBase<TEntity, TDto, TInputDto>` | `IEntityService<TEntity>` |
| `.For<TEntity, TKey>()` | `EntityControllerBase<TEntity, TKey, SearchObject<TKey>, TDto, TInputDto>` | `IEntityService<TEntity, TKey, SearchObject<TKey>>` |
| `.For<TEntity, TKey, TSearchObject>()` | `EntityControllerBase<TEntity, TKey, TSearchObject, TDto, TInputDto>` | `IEntityService<TEntity, TKey, TSearchObject>` |
| `.For<TEntity, TSearchObject, TSortBy, TIncludes>()` | `EntityControllerBase<TEntity, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>` | `IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>` |
| `.For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>()` | `EntityControllerBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>` | `IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>` |

> ⚠️ `.For<>()` takes **N** type arguments; the matching controller base takes **N+2** (it appends optional `TDto` and `TInputDto`). A wrong count compiles but fails at DI startup validation. 
> Controllers resolve `IEntityService<>` from `HttpContext.RequestServices` at runtime — not via constructor injection. The "Inject as" column applies when resolving the service manually outside a controller, e.g. `scope.ServiceProvider.GetRequiredService<IEntityService<...>>()`.

**Alignment card — the three generic lists must match (register = N, controller = N+2, inject = N).** Copy one tier:

```csharp no-compile
// ── SIMPLE (no TSortBy/TIncludes) ──
e.For<Product, int, ProductSearchObject>(/* … */);                                  // register (N=3)
class ProductController : EntityControllerBase<Product, int, ProductSearchObject, ProductDto, ProductInputDto>; // controller (N+2)
IEntityService<Product, int, ProductSearchObject>                                   // inject (N)

// ── COMPLEX (typed TSortBy + TIncludes) ──
e.For<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes>(/* … */);                                  // register (N=5)
class OrderController : EntityControllerBase<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes, OrderDto, OrderInputDto>; // controller (N+2)
IEntityService<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes>                                   // inject (N)
```

> **`IEntityService<TEntity, TKey>` is always registered** by every `.For<>()` overload (alongside the fully-typed interface in the table). It is the safe, universal interface to inject or resolve manually when seeding or resolving outside a controller — e.g. `scope.ServiceProvider.GetRequiredService<IEntityService<Supplier, int>>()`. When unsure which interface a registration exposes, use `IEntityService<TEntity, TKey>`.
>
> **Bare `IEntityService<TEntity>` shortcut (no `TKey`)** is registered **only** by `.For<TEntity>()` and the complex int `.For<TEntity, TSearchObject, TSortBy, TIncludes>()`. Do **not** rely on it for `.For<TEntity, int>()` or `.For<TEntity, int, TSearchObject>()` registrations — those expose `IEntityService<TEntity, int>` (and the fully-typed interface), not the bare shortcut. Attempting to resolve `IEntityService<TEntity>` there throws `No service for type IEntityService<TEntity>` at runtime.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Controllers

**Endpoints provided out of the box:**

| Method | Route | Action | Availability |
|---|---|---|---|
| `GET` | `/{id}` | Details | All |
| `GET` | `/` | List | All |
| `GET` | `/search` | Search (with count) | All |
| `POST` | `/list` | List (body, batch) | **Complex only** ¹ |
| `POST` | `/search` | Search (body, batch) | **Complex only** ¹ |
| `POST` | `/save` | Save (upsert) | All |
| `POST` | `/` | Create | All |
| `PUT` | `/{id}` | Modify — full update, all fields required | All |
| `PATCH` | `/{id}` | Patch — partial update (JSON Merge Patch, RFC 7386) | All |
| `DELETE` | `/{id}` | Delete — ⚠️ **soft-delete on `IArchivable`** ² | All |

² An `IArchivable` entity has no hard-delete endpoint: `DELETE /{id}` sets `IsArchived = true` and the row survives, with the same 200 and the same affected count. Archived rows then disappear from `GET /` and `GET /search`, and `GET /{id}` 404s — so **restore depends on keeping `IsArchived` on `TInputDto`** (Step 5). Full route contract: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete.

¹ The **body/batch** overloads (accept a `TSearchObject[]`) plus typed `?includes=`/`?sortBy=` exist only on *complex* bases (`EntityControllerBase` with `TSortBy` + `TIncludes`). `GET /search` — single search object, returns `count` — is on **every** base, so a simple entity can page too.

> **Response envelope.** Responses are wrapped, not bare DTOs: Details → `{ "item": {…} }`; List → `{ "items": [...] }`; Search → `{ "items": [...], "count": N }` (each also carries `duration` in ms). Unwrap `item`/`items` client-side.

> **PATCH vs PUT:** Use `PUT` when the client has the full entity. Use `PATCH` when only a subset of fields should change. The PATCH implementation deserializes the incoming JSON Merge Patch into `TInputDto`, so only fields declared on the input model can be modified. `TInputDto` property names must match `TEntity` property names (camelCase in JSON, PascalCase in C#, which is the STJ default). Related collections absent from the body are left intact, and a scalar field **declared on `TInputDto`** but omitted from a PATCH body is preserved too — Merge Patch writes only the keys you send. The trap is the opposite case: a field the DTO **never declares** maps as `null`/default on every PATCH *and* PUT — see ⚠️ below.

> **⚠️ A field absent from `TInputDto` maps as `null`/default on PATCH *and* PUT.** Server-owned/immutable
> values (`OwnerId` FKs, generated codes, computed totals) silently reset — a `[Required]` column 500s, a
> computed `Total` zeroes. Restore them in a **primer** branching on `EntityState` (the Step 5 recipe; full
> version, plus when a prepper is the right choice instead, in
> [`entities.patterns.md`](./entities.patterns.md) → Server-owned / immutable fields on update).
> **Synced collections invert the failure:** an `Attachments` (or `Related()`) collection the DTO never
> declares maps as `null`, which the sync reads as "not sent" — edits are *ignored*, not reset. Silent
> either way; declare the collection on the DTO (§Attachments step 3).

### Step 14: Setup and add Entity services to DI

> **→ See:** [`entities.setup.md`](./entities.setup.md) — P4: Create the DI Extension Method

### Step 15: Verify at runtime (required)

After wiring up a new entity, always run the application — not just `dotnet build`.
`dotnet build` catches compilation errors. Startup DI validation catches mismatches between `.For<>()` type arguments, `EntityControllerBase<>` generics, and `IEntityService<>` injections — a wrong generic parameter throws at startup rather than silently failing on the first request.

**Runtime checklist (Scalar / `curl`) — catches what a build cannot:**
1. The app **starts** without a `LicenseException` or DI-validation throw, and logs
   `Regira.Entities: {n} simple / {n} complex registered → tier = free` at `Information`
   (`EntityLicenseStartupLogger`). Those two counts are the **only** confirmation that your §Step 0 tally
   matches what was actually registered — check them before anything else.
2. `GET /{entity}/{id}` returns a **fully populated** DTO — confirm every field serializes (catches field-vs-property DTO bugs) and that nested related collections are present (catches missing `Includes`).
3. `GET /{entity}` and `GET /{entity}/search` return results, and any relation you eager-load appears on **List/Search rows** (not only on `Details`); on complex entities also check the batch `POST /list` / `POST /search`.
4. `POST`/`PUT` round-trips, including any child collections synced via `Related()`.

**Golden path — prove server-owned/computed fields survive a partial update** (the failure a build never catches):

```bash
BASE=http://localhost:5000/orders
# 1. create — note the returned id, Code, and Total
curl -s -X POST $BASE -H 'Content-Type: application/json' \
     -d '{"customerId":"<guid>","orderLines":[{"productId":1,"quantity":2}]}'
# 2. PATCH only status — send nothing else
curl -s -X PATCH $BASE/1 -H 'Content-Type: application/json' -d '{"status":"Shipped"}'
# 3. re-read
curl -s $BASE/1
```

Expected: `Code` and `Total` are unchanged from step 1 (not `null`/`0`) and `status` is now `Shipped` — if either reset, a server-owned field is missing its restore primer (Step 5). Verify seeded data **through the API** (`GET /{entity}/search`), never by the `.db` file size.

**Assert your seed data's domain invariants with a query, not by eyeballing a page.** Name each rule the data must satisfy ("every asset whose status is *In use* has a holder"; "every event has at least one session"), then prove it with a search that must return `count: 0` — `GET /assets/search?statusId=3&isAssigned=false`. This is the one class of bug a green build, a green type-check *and* a passing round-trip all miss: the generator loop that skips a case leaves data that is individually valid and collectively wrong, and it only shows up as something looking odd on a page nobody scrolled to.

**Then check the *distributions*, not only the invariants.** An invariant catches the rule you thought of; a ratio catches the one you didn't. Count each state your UI visualises — a bucket sitting at **0 % or 100 % of its population is a generator bug**, however plausible each individual row is. The classic shape is a date derived from a uniformly-spread `Created` against a much shorter SLA or due window: every row is defensible and every open item is overdue, which makes the badge, the filter and the dashboard tile meaningless. Derive such dates relative to each row's own window instead, and re-count.

Rows scoped to a user/tenant, or endpoints gated by role? One more check applies — §Security & Authorization → *Verify per identity*.

---

## Seeding via IEntityService

Seed through the services, not the DbContext (no controller, so the usual gotchas are yours to handle):

> **Three traps:** name the token — `List(null, token: token)` — or it binds as the *search object* and filters nothing; `SaveChanges()` clears the change tracker (saved entities detach); `e.Related()` owned/join rows have no service of their own.

- **Seed between `builder.Build()` and `app.Run()`.** `app.Run()` blocks until shutdown, so seeding code placed after it never executes — and the app looks empty with no error anywhere.
- Every `.For<>()` registers `IEntityService<TEntity, TKey>` — resolve that shape (e.g. `IEntityService<Product, int>`) in a scope for seeding/jobs, whatever the builder overload.
- On that universal interface, `List`/`Count` take `object? so` **first** — a positionally-passed `CancellationToken` binds as the *search object* and silently filters nothing. Name the token: `List(null, token: token)`.
- It does **not** auto-persist — call `await service.SaveChanges()` yourself.
- Bulk: loop `await service.Add(item)` (⚠️ **preppers run per item**, so a DB-touching prepper makes the loop N+1 — batch its lookups), then `SaveChanges()` **once** — see [`entities.patterns.md`](./entities.patterns.md) → Bulk insert / update. Standard EF auto-increment rules apply, so flush a parent batch before assigning `child.ParentId = parent.Id`.
- This `SaveChanges()` **clears the change tracker** (unlike stock EF Core) — saved entities detach. To touch an earlier wave again, re-`Modify()` it first.
- ⚠️ **Re-read that earlier wave detached and without navigations** — `db.Invoices.AsNoTracking().ToListAsync()`, then `Modify` each. **`Details(id)` is the wrong reader here:** it applies every registered `Includes(...)`, so each row arrives carrying its own copy of a shared principal, and attaching the second one throws *"The instance of entity type 'X' cannot be tracked because another instance with the same key value is already being tracked."* This is the natural last wave of any seed that has a derived aggregate to settle (§ Aggregates over a non-owned child collection), so it is where the crash lands.
- **Reference earlier waves by foreign key, not by navigation object.** Those rows are detached once the tracker clears, so `item.Shopper = shopper` re-adds that instance as a *new* graph member and the wave dies on `UNIQUE constraint failed: Shoppers.Id`. Read the keys you need (`.AsNoTracking()`, select the id) and assign `item.ShopperId = id`. Reserve navigation objects for children genuinely created in the same wave.
- **Owned/join rows have no service of their own** — a collection managed by `e.Related()` (m2m join, hierarchy join) can't be seeded through the service loop. Seed it through the owning parent's navigation, or via the `DbContext` for a standalone join — see [`entities.patterns.md`](./entities.patterns.md) → *Seeding owned / join rows*.
- **Uniqueness checks within one wave must be in-memory.** Rows still queued in the change tracker are invisible to a database query — an `AnyAsync(...)` dedupe check passes and the wave then dies on a `UNIQUE` constraint at `SaveChanges()`. Dedupe a wave with a `HashSet` of keys instead.
- **Counts within one wave too** (capacity caps, quota fills): a service `Count()` cannot see queued rows either, and `DbSet.Local` with an `Id == 0` filter miscounts once earlier flushes assigned ids. Count pending rows with `dbContext.ChangeTracker.Entries<T>().Count(e => e.State == EntityState.Added)` plus the persisted count.
- **The tracker clears on success only.** A **failed** `SaveChanges()` (e.g. `EntityConstraintException`) leaves every entry tracked — stock EF Core semantics — so you can fix or remove the offending entity and retry the same call; entities from the failed wave are **not** silently dropped. Only a successful save clears the tracker (the deliberate deviation from stock EF Core).

---

## Custom Entity Services

### Using EntityWrappingServiceBase

- Delegates all calls to an inner `IEntityService`; override only what you need
- Register via `e.UseEntityService<MyCustomEntityService>()` to replace the default repository
- ⚠️ Prevent circular dependencies when injecting the parent `EntityService`
- `Save(item)` routes to this service's own `Add`/`Modify` (based on `IEntity.IsNew()`), so put create/update wrapping logic in `Add`/`Modify` — the controller write path calls `Save()`, not `Add()` directly

**Who flushes what, when.** `Add`/`Modify`/`Save` only **track** changes; the controller flushes once, calling `service.Save(item)` then `service.SaveChanges()` (PUT, PATCH and POST all end in that same pair). Inside an override:

- ⚠️ **`base.Modify(item)` returns the *detached pre-modification original*** (the write service detaches it to attach `item` in its place). Read it for old values; mutating it persists nothing. Mutate `item` instead — it is the tracked instance.
- **Side effects on other rows need tracked entities.** Every framework read (`Details`, `List`) is no-tracking, so "load a sibling via the service, change it, rely on the controller's flush" silently persists nothing. Query the injected `DbContext` directly (tracked), or call `await SaveChanges()` yourself after staging the extra change.
- `SaveChanges()` flushes **and clears the tracker** (success only) — anything staged after a flush needs its own flush.

Examples:
- Caching: Wrap the default `EntityRepository` with `IMemoryCache`.
  - Override `Details(id)` — check the cache first, then call `base.Details(id)` and store the result
  - Override `Save(item)` (and `Remove(item)` if needed) — call base, then invalidate the cache entry
- Security: e.g. modify the SearchObject using business rules to automatically filter results based on user permissions, without needing to add extra filters on every endpoint.
- Validation

**Registration:**
- `e.AddTransient<IProductService, ProductService>()` — enables typed injection by interface
- `e.UseEntityService<ProductService>()` — replaces the default `EntityRepository` as `IEntityService` for the entity

> **→ See:** [`entities.signatures.md`](./entities.signatures.md) — EntityWrappingServiceBase
> **→ See:** [`entities.examples.md`](./entities.examples.md) — Order + OrderLine entities (OrderManager)

---

## Global Services

- Global services apply to **all entities implementing a given interface**.
- They are registered on the `EntityServiceCollectionOptions` (inside `UseEntities()`)
- Global services execute before entity-specific services — order matters

### Global Services (→ see [`entities.examples.md`](./entities.examples.md))

- Filter query builders → Additional Patterns > Global filter query builder
- Preppers (inline) → Setup
- Primers → Additional Patterns > Primers

### UseDefaults() — What It Registers

`options.UseDefaults()` is a convenience method that registers, in one call:

- **Paging defaults** — `DefaultPageSize = 10`, `MaxPageSize = 100` (override either afterwards). An omitted `pageSize` uses the default; a `pageSize <= 0` opts out and falls back to the max; every request is capped by `MaxPageSize`.
- **UTC date handling** — on by default, one policy per process (`Regira.Utilities.DateTimeDefaults.UseUtc`): timestamps are written as UTC and client-supplied dates/filter inputs are normalized. Disable with `e.UseUtc(false)` → values are used as given with `DateTime.Now` timestamps, and the UTC convention's converter goes inert automatically.
- **Default primers** — `HasCreatedDbPrimer`, `HasLastModifiedDbPrimer`, `ArchivablePrimer` (timestamps + soft-delete stamping).
- **Automatic DbContext wiring** (`AddDefaultInterceptors()`) — `UseEntities<TContext>()` contributes the primer/normalizer/auto-truncate interceptors, the UTC date convention and the archived query filter to the context's options itself, so `AddDbContext` only needs the provider and the `DbContext` needs no Regira call. Matches by assignability: an abstract-base registration (`UseEntities<AppContextBase>()`) also wires derived provider-specific contexts, in any registration order. Fine-grained control via `e.WireDbContext(DbContextWiring …)`: `None` opts out; without `UseDefaults()` use `e.AddDefaultInterceptors()` for the full set or pick pieces à la carte (e.g. `DbContextWiring.PrimerInterceptors`).
- **Default global query filters** — `FilterIdsQueryBuilder`, `FilterArchivablesQueryBuilder`, `FilterHasCreatedQueryBuilder`, `FilterHasLastModifiedQueryBuilder`. These are int-keyed; for full key-typed filtering of a non-int entity also call `AddDefaultGlobalQueryFilters<TKey>()`. The query builder runs one variant per filter family and prefers the key-matching one, so a non-int entity's key-agnostic defaults (the archived opt-ins, timestamp/`Q` filtering) still apply when only the int variant is registered.

> **Hiding archived rows is part of `UseDefaults()`** (`DbContextWiring.ArchivedQueryFilter`): the `e => !e.IsArchived` EF query filter is wired into the context's options, so archived rows are hidden on every list/count *and* inside included collections without a line in the `DbContext`. The registered query builder translates the opt-ins on top of it — a caller opts in per request with `?archived=included` (both) or `?archived=only` (the recycle bin); flip the app-wide default with `DefaultArchivedFilter = ArchivedFilter.Included` on `UseEntities()`. ⚠️ The wiring reaches contexts resolved from DI only: a hand-constructed `new AppDbContext(options)` takes `.AddArchivedQueryFilter()` on its options builder. Startup validation errors out naming the entity when a model ends up without the filter. Full round-trip: [`entities.patterns.md`](./entities.patterns.md) → Soft Delete.
- **Default entity normalizer** — `DefaultEntityNormalizer` (processes `[Normalized]`).
- **The normalized-content `Q` filter** — `FilterHasNormalizedContentQueryBuilder` — **but only on the parameterless path.** `UseDefaults()` registers it; `UseDefaults(cfg => …)` (passing a normalizing configure callback) does **not**, so register it yourself in that case.

> **→ See:** §Quick Reference: Built-in Services (this file) for the full list of registered classes.

---

## Startup validation (Development)

`UseEntities()` registers a hosted service that validates the entity registrations once at host start —
in the Development environment by default. It catches, with actionable messages:

- **Controller ↔ `For<>()` arity mismatches** — an `EntityControllerBase<…>` subclass whose generic
  arguments match no registered `IEntityService<…>` fails startup listing the registered alternatives
  (enabled by `ConfigureDefaultJsonOptions()` or `ValidateEntityControllers()`), plus a missing `IEntityMapper`.
- **Unwired interceptors** — primers/normalizers registered in DI while the `DbContext` options lack
  the matching interceptor (they would silently never run). Only applies to setups without `UseDefaults()`
  (which auto-wires the interceptors) that also skipped `e.WireDbContext(...)`.
- **Ignored `?q=`** (warning) — entities without `IHasNormalizedContent` and without a custom filter.
- **Two write paths** (warning) — an entity synced by a parent's `Related()` that also has its own `.For<>()`.
  Supported when the parent's input DTO omits the collection; the validator can't see DTO shapes, so it always
  reports the pairing. Detects top-level `Related()` calls, not ones nested inside a `configure` builder.
- **Attachments the input DTO cannot carry** (warning) — an `IHasAttachments` entity whose `UseMapping`
  input DTO declares no `Attachments` collection. Every parent write then maps the collection to `null`
  ("not sent"), so attachment adds/removes/reorders through the entity controller are silently ignored
  (§Attachments step 3).
- **Null attachment `Uri`** (warning) — an attachment controller is mapped while the null resolver is in
  place, i.e. `UseAttachmentUris()` was omitted or set on a different options instance.
- **Archivable reference data behind a required FK** (warning, net10) — an `IArchivable` principal that
  separately registered entities reference through a required FK. Archiving such a row drops the dependents
  from list results while `/search` keeps counting them; the message works the mirrored-filter remedy
  ([`entities.patterns.md`](./entities.patterns.md) → Soft Delete).
- **Out-of-scope global filter** (warning) — a registered global filter whose `TEntity` no registered entity
  satisfies, so it never runs. Usually a scope that names an interface the entity does not implement; when the
  filter guards row access, the rows it was meant to hide are being returned. The built-in defaults are
  reported as *information* instead: `UseDefaults()` registers the whole set whether or not the app has an
  `IArchivable` (or timestamped, or normalized-content) entity, so an inert one carries no signal.
- **Missing archived query filter** (**error**) — an `IArchivable` entity whose model carries no archived
  filter, from either route: the options wiring (`DbContextWiring.ArchivedQueryFilter`, on by default) or an
  explicit `modelBuilder.SetArchivedQueryFilter()`. `DELETE` flags those rows and nothing hides them. Reached
  by a context the wiring misses — a non-generic `UseEntities()`, or `WireDbContext(...)` without the flag.
  Suppress it deliberately with `o.DefaultArchivedFilter = ArchivedFilter.Included` if archived rows are meant
  to stay visible. It inspects contexts resolved from DI, so a `DbContext` constructed by hand is outside its
  reach as well as the wiring's.

Configure via `UseEntities(o => o.ConfigureValidation(v => { v.Enabled = true; v.ThrowOnError = false; }))`:
`Enabled` — `null` (default) = Development only, `true` = always (Production opt-in), `false` = never.
Diagnostic code `REGIRA0001` marks the obsolete `EntityPrimerContainer(DbContext, IServiceCollection)` constructor, which builds a second service provider to resolve primers — use `RegisterPrimerContainer<TContext>()` (the `IEnumerable<IEntityPrimer>` constructor) instead.

---

## Paging defaults

`PagingInfo.PageSize` is nullable, so List/Search distinguish three cases (enforced at the HTTP boundary; `UseDefaults()` sets `DefaultPageSize = 10`, `MaxPageSize = 100`):

| Requested `pageSize` | Effective page size |
|---|---|
| omitted (`null`) | `DefaultPageSize` (or the max when no default is configured) |
| `0` or negative | `MaxPageSize` — opts out of paging, capped at the max |
| positive `n` | `n`, clamped to `MaxPageSize` |

- **Enforced at the HTTP boundary only** — `EntityControllerBase` List/Search apply one shared rule (`EntityListOptionsExtensions.ApplyPagingDefaults`), which any other HTTP surface can reuse so `MaxPageSize` cannot be escaped. Direct `IEntityService` calls are unaffected — they apply the `PagingInfo` you pass as-is (`PageSize` null or `<= 0` → everything, uncapped), so the service layer keeps full control.
- `MaxPageSize` is always the ceiling; `null` for either option turns that aspect off.

**Global — inside `UseEntities()`:**
```csharp no-compile
services.UseEntities<AppDbContext>(options =>
{
    options.DefaultPageSize = 50;   // applied when the request omits pageSize
    options.MaxPageSize = 200;      // caps every request; also what a pageSize <= 0 opt-out returns
});
```

**Per-entity override — inside `.For<>()`** (fully replaces the global values for that entity):
```csharp no-compile
.For<Product>(e => e.SetPageSize(defaultPageSize: 25, maxPageSize: 100))
.For<LogEntry>(e => e.SetPageSize());   // opt out entirely: omitted / pageSize <= 0 returns every row
```

---

## Normalizing

### The normalize contract

`DefaultNormalizer.Normalize(string?)`, in order. The **query side runs the same transform** — `QKeywordHelper.ApplyNormalize` defaults to `true` — so a stored value matches `?q=` only if both sides survive it identically.

| # | Rule | `Café A.C.M.E. #1` → |
|---|---|---|
| 1 | diacritics removed (`RemoveDiacritics = true`) | `Cafe A.C.M.E. #1` |
| 2 | anything outside `a-z A-Z 0-9`, whitespace and `- _ , ! ; & '` is **deleted, not replaced by a space** | `Cafe ACME 1` |
| 3 | `- , ! ; & '` → space (`_` is kept as-is) | `Cafe ACME 1` |
| 4 | whitespace collapsed and trimmed | `Cafe ACME 1` |
| 5 | `Transform` — default `NoChanges`, so **case is preserved, never uppercased** | `Cafe ACME 1` |

⚠️ **Rule 2 is why raw text in `NormalizedContent` can never be found.** `A.C.M.E.` stored raw stays `A.C.M.E.`, while `?q=ACME` and `?q=A.C.M.E.` both normalize to `ACME` — no match, no error. Always write `NormalizedContent` through `[Normalized]` or an injected `INormalizer`; never by string concatenation.

### Attribute-Based (Recommended)

⚠️ **The one that populates `NormalizedContent` goes on the *property*.** The attribute is legal on a class
too, but the two placements take **different options**, and only the property form has a `SourceProperty` /
`SourceProperties` to normalize *from*. Put `[Normalized(SourceProperties = [...])]` on the class and it
compiles, binds nothing, and leaves `NormalizedContent` `null` for every row — so `?q=` matches nothing and
every search returns empty, at HTTP 200, with no error and no warning.

```csharp no-compile
public class Category : IEntity<int>, IHasNormalizedContent
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    [MaxLength(1024), Normalized(SourceProperties = [nameof(Title), nameof(Description)])]
    public string? NormalizedContent { get; set; }
}
```

**Pick short, identifying sources — not body text.** `NormalizedContent` is a `[MaxLength(1024)]` search key,
not a copy of the record: feed it titles, codes, names and short summaries. A description or free-text body
overflows it, and the normalizing primer then truncates on every save with a
`Truncating X.NormalizedContent from N to 1024 characters` warning — harmless but noisy, and the tail that
falls off is silently unsearchable. Widen `[MaxLength]` if you genuinely need more.

**`[Normalized]` attribute options:**

| Property | Valid on | Purpose |
|---|---|---|
| `SourceProperty` | property | Single source property name |
| `SourceProperties` | property | Array of source property names (concatenated with space) |
| `Recursive` | **class** | Walk nested objects as well (default: `true`) |
| `Normalizer` | either | Custom `INormalizer` (property) or `IObjectNormalizer` (class) type |

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity

### Custom Normalizer

- Register per entity: `e.AddNormalizer<ProductNormalizer>()`
- Register globally: `options.AddNormalizer<IHasPhone, PhoneNormalizer>()`
- When `IsExclusive = true`, no other normalizer runs for that entity

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Global normalizer

### Filtering with Normalized Content and IQKeywordHelper

Use `IQKeywordHelper.Parse(q)` to parse `Q` into keywords with wildcard support (e.g. `"blue*"` → `"blue%"`). Use `keyword.QW` with `EF.Functions.Like`.

**Searching a few explicit columns** — for an entity that is *not* `IHasNormalizedContent`, `FilterQ` takes the field selectors and builds the predicate (each keyword must match at least one field, so `"acme 2024"` matches a row whose code carries one term and whose related shopper carries the other). Without this or a custom filter, `?q=` is silently ignored — startup says so.

`IQKeywordHelper` is injected, so this is a **registered builder class**, not an inline `e.Filter(...)` lambda (a lambda has no DI — see §Step 6):

```csharp no-compile
public class OrderQueryBuilder(IQKeywordHelper qHelper) : FilteredQueryBuilderBase<Order, int, OrderSearchObject>
{
    public override IQueryable<Order> Build(IQueryable<Order> query, OrderSearchObject? so)
        => query.FilterQ(qHelper.Parse(so?.Q), x => x.Code, x => x.Shopper!.Name);
}

// registration
e.AddFilter<OrderQueryBuilder>();
```

**Or use the built-in global filter** (applies to all `IHasNormalizedContent` entities):

```csharp no-compile
options.AddGlobalFilterQueryBuilder<FilterHasNormalizedContentQueryBuilder>();
```

> **⚠️ For cross-field/cross-relation `Q` search, fold the related data into `NormalizedContent` — don't add a second `Q` filter.**
> The global normalized-content filter already AND-s a `Q` predicate for every `IHasNormalizedContent`
> entity, so a *second*, custom `Q` filter is AND-ed on top of it and silently narrows (or empties) the
> result. The idiomatic fix is to make the parent's `NormalizedContent` already contain the searchable text
> from the related entities — register a custom `EntityNormalizer` (or use
> `[Normalized(SourceProperties = [...])]`) that includes those properties — so the single global filter
> covers everything.
> - **Keep it fresh:** `NormalizedContent` is computed when the parent is saved, so re-normalize the parent
>   whenever the related data it embeds changes (e.g. via a prepper/primer, or by re-saving the parent).
> - **⚠️ Same-save timing:** if the normalizer queries related rows from the database, standard EF rules
>   apply — children added in the *same* `SaveChanges` aren't committed yet, so the normalizer won't see them.
>   Reading the parent's in-memory navigation collection instead avoids this.
> - **Live-join fallback:** only when you genuinely need a live join rather than denormalized content should
>   you write your own `Q` filter — and then **don't** implement `IHasNormalizedContent` on that parent (so
>   the global filter doesn't apply) and OR the conditions yourself, otherwise the two predicates AND.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > IQKeywordHelper — Q full-text search

### Enable Normalizer Interceptors

Normalizers run automatically when saving: `UseDefaults()` wires the normalizer interceptor into the
DbContext options; without `UseDefaults()`, select `e.WireDbContext(DbContextWiring.NormalizerInterceptors)`.

> **→ See:** [`entities.patterns.md`](./entities.patterns.md) — DbContext Interceptors — Quick Reference

---

## Attachments

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Attachments
> **→ See:** [`entities.signatures.md`](./entities.signatures.md) — Attachments
> **→ See:** [`io.storage`](../../Common.IO.Storage/ai/io.storage.instructions.md) — the `IFileService` file-store contract behind attachments (read before wiring a store)

1. Create a class inheriting the **`EntityAttachment`** base and set `ObjectType` in the constructor:
   `public class ProductAttachment : EntityAttachment { public ProductAttachment() => ObjectType = nameof(Product); }`.
   **One subclass per owner entity** — the class *is* the join table, and its constructor pins a single
   `ObjectType`, so a second owner needs its own subclass, `DbSet`, controller and registration. Budget it as
   one extra simple slot per owner, not one for the whole app.
2. Implement `IHasAttachments` and `IHasAttachments<TAttachment>` on the owning entity (`Attachments` property needs explicit interface implementation)
3. **Mapped owner (`UseMapping`)? Declare the collection on the input DTO:** `public ICollection<EntityAttachmentInputDto>? Attachments { get; set; }` (or your derived attachment input DTO). Without it the convention map drops the incoming collection on every save and the sync reads that as "attachments not sent" — adds, removes and reorders through the parent are silently ignored (200 OK, no error; the `/{objectId}/attachments` sub-routes still work, which masks it). Startup validation warns. Mirror on the read DTO with `ICollection<EntityAttachmentDto>?`.
4. Create a controller inheriting `EntityAttachmentControllerBase<TAttachment>` — **name it after the attachment type** (`ProductAttachmentController` or `ProductAttachmentsController` for a `ProductAttachment`; any other name makes `Uri` unresolvable, see 7) and set the class route to the **owner base path**, e.g. `[Route("products")]` (resource-relative — see the route-prefix note in §Step 13). The base controller appends the sub-routes `{objectId}/attachments`, `attachments/{id}`, `{objectId}/files`, ….
5. Add `DbSet<Attachment>` and `DbSet<TAttachment>` to DbContext; configure relationship in `OnModelCreating`
6. Register **two** things: `.WithAttachments(_ => new BinaryFileService(...))` for the shared `Attachment` entity + file store + bytes→file primer, **and** `.For<Product>(e => e.HasAttachments<AppDbContext, Product, ProductAttachment>(x => x.Attachments))` for the typed per-owner services + link prepper + DTO mapping. `HasAttachments` is an extension on the **base** `EntityServiceBuilder`, so it chains on every `For<>()` tier — a complex owner registers it exactly like the simple one shown here.
7. *(web apps)* Call `options.UseAttachmentUris()` (before registering entities, on the **same** `UseEntities` options instance) and register `AddHttpContextAccessor()` so attachment DTOs resolve a `Uri` linking to the attachment controller's `GetFile` action.

> ⚠️ **Owner is `IArchivable`?** The link entity is separately registered and has no navigation back to its owner, so archiving the owner leaves its attachments visible to `/{ownerId}/attachments`. Startup validation flags the shape; the working model configuration is in [`entities.patterns.md`](./entities.patterns.md) → Soft Delete > *Attachments on an archivable owner*.

> **Reads: eager-load the owner's `Attachments`, or the file metadata comes back null.** `HasAttachments`
> wires the write side and the second hop (`EntityAttachment → Attachment`) on the attachment service — but the
> owner's own List/Details must register the **first** hop. Add it to the owner's includes with
> `e.Includes((q, _) => q.IncludeEntityAttachments())` (`Regira.Entities.EFcore.Attachments`), i.e.
> `.Include(x => x.Attachments!).ThenInclude(a => a.Attachment)`. Including only `x.Attachments` loads the join
> rows but leaves `attachment` null on the DTO — so `fileName`, `contentType` and `length` are all missing.
> `uri` still resolves: with no `Attachment.FileName` to build the filename route from, the resolver falls
> back to the by-id `files/{id}` link.
>
> ⚠️ **The link DTO nests the file.** `id`, `objectId`, `attachmentId`, `objectType`, `sortOrder` and `uri` are
> flat; `fileName`, `contentType`, `length` and the timestamps live one level down under `attachment`
> (`EntityAttachmentDto.Attachment`, an `AttachmentDto`). Reading `link.fileName` yields `undefined`, not an
> error — a payload is in §Response Types.
>
> **Ordered by `SortOrder`? Write the two hops out.** `IncludeEntityAttachments()` takes no ordering argument,
> so a UI that lets the user drag attachments into an order needs the filtered include instead of the helper —
> the same two hops, plus the `OrderBy`. `HasAttachments` assigns `SortOrder` from the incoming array position
> on every parent save, so this is what makes the saved order survive a reload:
> `e.Includes((q, _) => q.Include(x => x.Attachments!.OrderBy(a => a.SortOrder)).ThenInclude(a => a.Attachment))`.

> ⚠️ **`HasAttachment` serializes `null` — nothing populates it.** The `?hasAttachment=` filter queries
> `Attachments.Any()`, not the property. Set it yourself (a primer, or a mapped projection) or leave it off
> the DTO; a UI indicator bound to it (a paperclip icon) is otherwise always empty.

> **The `Uri` is `null`, never an error.** All four causes: the option was omitted, or set on a different
> `UseEntities` options instance than the one the entity was registered on (both leave the
> `NullAttachmentUriResolver` in place); no controller named after the attachment type is mapped, or its
> download route was replaced by a custom endpoint; or there is no active request (seeding, background
> work). Startup validation warns for the first two whenever an attachment controller is mapped, and the
> resolver itself warns for the third naming the controller names it tried. A stable alternative is to
> compose `{ownerRoute}/files/{id}` client-side and skip the option entirely.

> **`FileName` carries the client's virtual folders; storage never does.** `FileName` is the client's own
> value and may be a path (`folder1/folder2/report.pdf`) — that is how attachments are organised. The
> identifier is generated independently (`{Owner}/Attachments/{ObjectId}/{slug}-{guid}{ext}`), so **re-filing
> is just writing a different `FileName`**: the download URL changes, the bytes never move. Downloads use a
> catch-all (`{objectId}/files/{*fileName}`) so a multi-segment name resolves. Empty and relative segments
> are dropped on write, since the value is echoed into URLs.

> **Reading bytes in custom code:** inject `IAttachmentFileService<Attachment, int>` and call `GetBytes(item)`. Consuming code references files by `Identifier` (the public storage key, populated when you load through the entity service); `Path` is internal and isn't mapped to DTOs — clients get a download `Uri` instead.

> **Ordering & extending the DTOs.** Attachment order travels by **array position**, not by a client-sent
> value: on every parent save the pipeline assigns `SortOrder = index` over the incoming collection
> (`SetSortOrder()`, wired by `HasAttachments`), so the input DTO carries no `SortOrder` on purpose.
> `EntityAttachment` and the read `EntityAttachmentDto` expose it — order the eager-load
> (`x.Attachments!.OrderBy(a => a.SortOrder)`) so a round-trip is stable. The DTOs are `record`s, so a
> derived DTO must also be declared `record` (CS8865). Extra client-supplied fields belong on a derived
> **input** DTO; a derived **read** DTO works too (the `Uri` after-mapper matches attachments by the
> `EntityAttachmentDto` base type).

> **Images in `<img>` tags 401 on a secured API** — the browser sends no `Authorization` header for
> `src` URLs. Expose the download anonymously (uploads/deletes stay guarded):
> [`entities.patterns.md`](./entities.patterns.md) → Public (anonymous) attachment downloads.

---

## Error Handling

### EntityInputException (returns HTTP 400)

Controllers automatically catch `EntityInputException` and return `BadRequest (400)`.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Order + OrderLine entities (OrderManager)

### EntityConstraintException (returns HTTP 409)

`SaveChanges()` wraps a database **integrity-constraint** violation (unique index, FK, NOT NULL, check —
detected per provider: SQLSTATE class 23, SQLite error 19, SQL Server 547/515/2601/2627) in
`EntityConstraintException`; every write surface (controller bases, attachment controllers) returns
**409 Conflict**. The response detail is generic — the provider's
constraint message can leak index names and other users' values, so it is logged server-side (warning) by
the write service instead. Transient faults (deadlocks, timeouts, concurrency conflicts) are **not**
wrapped and keep surfacing as 500s for alerting. When the client can fix the input, prefer an explicit
check in a prepper + `EntityInputException` — a field-level 400 beats a generic 409.

---

## Common Patterns

### Master-Detail (Order + OrderItems)

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Order + OrderLine entities

### Many-to-Many Relations

**Treat Many-to-Many as two One-to-Many relations** using a middle/join table with an explicit join entity. Always create an explicit join entity — even if the join table carries no extra properties, having a dedicated entity makes the collection easier to manage via `e.Related()`.

- Use an explicit join entity and manage the collection via `e.Related()`
- Always configure the relationship in `DbContext.OnModelCreating`
- Use a prepper (or `.Related()`) to synchronize join table changes when updating
- Child entities registered via e.Related() do NOT need a standalone IEntityService<T> registration — add one only
  when the join row needs its own endpoints, and then keep the collection off the parent's input DTO (if the child
  is also sortable, see entities.patterns → Owned children that are both sortable and individually togglable)

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Product entity

### Feature recipes → [`entities.patterns.md`](./entities.patterns.md)

Load that file when implementing one of these:

- **Bulk insert / update** — batch many rows through a single `SaveChanges()`; includes **multi-wave seeding** (Id/change-tracker timing).
- **Single-field PATCH / state toggle** — flip `IsActive` (or any one field) via `PATCH /{id}`; covers toggling owned join rows.
- **Server-owned / immutable fields on update** — restore `OwnerId`/codes from `entry.OriginalValues` in a primer (or from a prepper's `original` when a second writer owns the field) so PUT/PATCH can't null or re-mint them.
- **Server-generated sequential codes** — mint `REQ-2026-00001` from a primer on `Added` and restore it on `Modified`; includes when that primer has to be a prepper instead, and why the counter is primed from the highest code.
- **Cross-entity aggregates & report endpoints** — a dashboard controller belongs to no entity, so it **bypasses the pipeline**: global filter row security does not apply unless you repeat the predicate. Also **domain actions on an entity resource** (`POST /{id}/approve`) and **role-gated transitions**.
- **Aggregates over a non-owned child collection** — a parent total rolled up from children that own their own FK. Eventually consistent, seeding needs a second pass, and a child query filter can zero it on restore.
- **Role-gated write authorization filter** — one global filter mapping controller → required role, keyed on the generated write actions because the controllers serve reads over `POST` too.
- **Writing to a related entity from a prepper** — the typed `e.Prepare(entity, dbContext)` overload; `EntityInputException<T>` must name the *serviced* entity or it escapes as a 500.
- **Renamed DTO property** — wire both directions on the typed `UseMapping` chain when a DTO name differs from the entity's (Mapster maps by name only).
- **Public (anonymous) attachment downloads** — serve images to `<img>` on a secured API (`[AllowAnonymous]` override of `GetFile`).
- **Soft delete** — the full `IArchivable` round-trip: `DELETE` archives instead of erasing, which routes see archived rows, and what restore requires.
- **Owned children that are both sortable and individually togglable** — who owns `SortOrder` vs a per-row flag.
- **Audit trail** — stamp `CreatedBy`/`ModifiedBy` via a global primer.
- **Hierarchical data** — self-referencing parent/children (single- or multi-parent).
- **Auto truncate** — clip strings to `[MaxLength]` before save.
- **DbContext interceptors** — which `AddXInterceptors()` to register.

### Domain blueprints → [`entities.blueprints.md`](./entities.blueprints.md)

Ready-to-copy **feature slices** (complete models + DbContext config + registration + DTOs), proven in the Regira reference apps. Load that file when the task matches one of these:

- **Stakeholders** — parties (person/organization TPH) with contact data, addresses and typed party-to-party relations; polymorphic DTOs; optional user-account link.
- **EntityLabels** — free-form label/tag rows on any entity (per-owner subclass tables), searchable via the owner's `Q`.
- **Multi-tenancy** — `IHasTenantId` marker + one global filter (scope every read) + one primer (stamp every write); tenant claim in the JWT.
- **Recursive entities** — whole-subtree filters (`AncestorId`/`OffspringId`) via mapped recursive-CTE table-valued functions, plus tree endpoints with `Regira.TreeList`.
- **Identity users as entities** — a custom `IEntityRepository` over `UserManager<TUser>` so users get the standard entity endpoints.
- **Virtual entity** — read-only reference data (countries, …) served through `IEntityService` without a table.

---

## Response Types

All base controller endpoints return typed wrappers (`DetailsResult`, `ListResult`, `SearchResult`, `SaveResult`, `DeleteResult`). Wire format (camelCase):

| Endpoint | Body |
|---|---|
| `GET /{id}` | `{ "item": { … }, "duration": 5 }` |
| `GET /` (List) | `{ "items": [ … ], "duration": 5 }` — no count |
| `GET /search`, `POST /search` | `{ "items": [ … ], "count": 42, "duration": 5 }` |
| `POST` / `PUT` / `PATCH` (Save) | `{ "item": { … }, "isNew": true, "affected": 1, "duration": 5 }` |
| `DELETE` | `{ "item": { … }, "duration": 5 }` |

Populated, so a client can be typed against it without calling the API first. `item`/`items` carry your DTO
verbatim; everything around them is the wrapper:

```jsonc
// GET /api/products/search?q=lamp&pageSize=2
{ "items": [ { "id": 12, "code": "LMP-001", "title": "Desk lamp", "categoryId": 3,
               "category": { "id": 3, "title": "Lighting" },        // nested DTO — see the warning below
               "created": "2026-08-13T09:12:44Z", "lastModified": null } ],
  "count": 42, "duration": 5 }

// POST /api/products/save  → 200
{ "item": { "id": 13, "code": "LMP-002", "title": "Floor lamp" }, "isNew": true, "affected": 1, "duration": 7 }

// any save that fails validation → 400. ⚠️ The keys are whatever your EntityInputException used, echoed
// verbatim — they go through ModelState, and the web JSON defaults camelCase properties but NOT dictionary
// keys. nameof(Product.CategoryId) therefore surfaces as "CategoryId"; pass the DTO's camelCase spelling if
// the client indexes the map by field name.
{ "title": "One or more validation errors occurred.", "status": 400,
  "errors": { "categoryId": ["Category 99 does not exist"], "code": ["Code is required"] } }

// GET /api/products/7/attachments — a List endpoint, so no "count"; the file metadata is NESTED
{ "items": [ { "id": 5, "objectId": 7, "attachmentId": 91, "objectType": "Product", "sortOrder": 0,
               "uri": "https://localhost:5001/api/products/7/files/manual.pdf",
               "attachment": { "id": 91, "fileName": "manual.pdf", "contentType": "application/pdf",
                               "length": 20481, "created": "2026-08-13T09:12:44Z", "lastModified": null } } ],
  "duration": 4 }
```

⚠️ **Nest a Core/summary DTO and its collections are absent, not empty.** A UI reading `status.transitions`
off a nested `StatusCoreDto` gets `undefined`, with no error — and on the front end `fromPool` rehydrates it
into the real model *class*, so it looks right in a debugger. Widen the nested DTO, or load that entity's
own endpoint.

> **→ See:** [`entities.signatures.md`](./entities.signatures.md) — Response Types

---

## Quick Reference: Built-in Entity Interfaces

| Interface | Properties | Related Services |
|---|---|---|
| `IEntity<TKey>` | `Id (TKey)` | `FilterIdsQueryBuilder` |
| `IEntityWithSerial` | `Id (int)` | *(same as `IEntity<int>`)* |
| `IHasCode` | `Code (string?)` | Normalizers |
| `IHasTitle` | `Title (string?)` | Normalizers, `FilterTitle` |
| `IHasDescription` | `Description (string?)` | Normalizers |
| `IHasNormalizedContent` | `NormalizedContent (string?)` | `FilterHasNormalizedContentQueryBuilder` |
| `IHasCreated` | `Created (DateTime)` | `HasCreatedDbPrimer`, `FilterHasCreatedQueryBuilder` |
| `IHasLastModified` | `LastModified (DateTime?)` | `HasLastModifiedDbPrimer`, `FilterHasLastModifiedQueryBuilder` |
| `IHasTimestamps` | `Created, LastModified` | Both timestamp services |
| `IArchivable` | `IsArchived (bool)` | `ArchivablePrimer`, `FilterArchivablesQueryBuilder`, archived query filter (`DbContextWiring.ArchivedQueryFilter`) |
| `ISortable` | `SortOrder (int)` | `RelatedCollectionPrepper`, `EntityExtensions.SetSortOrder` |
| `IHasStartDate` | `StartDate (DateTime?)` | *(none — contract only)* |
| `IHasEndDate` | `EndDate (DateTime?)` | *(none — contract only)* |
| `IHasStartEndDate` | `StartDate, EndDate` (both `DateTime?`) | *(none — contract only)* |
| `IHasObjectId<TKey>` | `ObjectId (TKey)` | Attachments |
| `IHasAttachments` | `HasAttachment, Attachments` | Attachments module |

> **The date interfaces are contracts, not query support.** `StartDate`/`EndDate` are **nullable** and no built-in `QueryExtensions` helper or global filter consumes them — implement one for the shared shape, not to get filtering. If your period is mandatory, plain non-nullable `Start`/`End` properties of your own are the better fit, and you write the range filter either way.

> **Nullability convention:** `IHasCode`/`IHasTitle`/`IHasDescription`/`IHasNormalizedContent` declare their strings as `string?` — a contract convention, not enforcement. Need non-null? Add `[Required]` on the implementing property; the `string?` declaration stays — ❌ `public string Code { get; set; } = null!;` narrows the interface setter and warns `CS8767`, ✅ `[Required] public string? Code { get; set; }`.

## Quick Reference: Built-in Services

Registered by `options.UseDefaults()`; can also be registered manually.

### Global Filter Query Builders

| Class | Applies to | Filters on |
|---|---|---|
| `FilterIdsQueryBuilder` | All entities | `Id`, `Ids`, `Exclude` |
| `FilterArchivablesQueryBuilder` | `IArchivable` | `Archived` — `null` falls back to `EntityQueryOptions.DefaultArchivedFilter` (`Excluded`). Translates the **opt-ins** only; hiding archived rows is the archived query filter (auto-wired by `UseDefaults()`) |
| `FilterHasCreatedQueryBuilder` | `IHasCreated` | `MinCreated`, `MaxCreated` (interpreted as UTC) |
| `FilterHasLastModifiedQueryBuilder` | `IHasLastModified` | `MinLastModified`, `MaxLastModified` (interpreted as UTC) |
| `FilterHasNormalizedContentQueryBuilder` | `IHasNormalizedContent` | `Q` keyword search |

Each of these has a `TKey`-generic variant; the parameterless names are int-keyed. A global filter only
accepts search objects matching its own key type, so non-int entities need the matching variant registered
(`AddDefaultGlobalQueryFilters<TKey>()` does this automatically).

### Primers

| Class | Applies to | Behaviour |
|---|---|---|
| `HasCreatedDbPrimer` | `IHasCreated` | Sets `Created` (UTC) on insert; normalizes client-supplied values to UTC |
| `HasLastModifiedDbPrimer` | `IHasLastModified` | Sets `LastModified` (UTC) on update |
| `ArchivablePrimer` | `IArchivable` | Soft-delete: sets `IsArchived = true` |
| `AutoTruncatePrimer` | All entities | Truncates strings to `[MaxLength]` |

### Normalizer Services

| Interface | Implementation | Role |
|---|---|---|
| `INormalizer` | `DefaultNormalizer` | Normalizes a string value |
| `IObjectNormalizer` | `ObjectNormalizer` | Processes `[Normalized]` attributes |
| `IEntityNormalizer` | `DefaultEntityNormalizer<IEntity>` | Orchestrates entity normalization |
| `IQKeywordHelper` | `QKeywordHelper` | Parses Q search strings with wildcard support |

---

## Security & Authorization

Generated endpoints ship **anonymous** — no controller base carries `[Authorize]`. Every scaffolded endpoint, including delete and attachment download, is public until the app adds authorization.

- Put `[Authorize]` on your controller subclass (use `[AllowAnonymous]` per action for exceptions): `[Authorize] public class ProductController : EntityControllerBase<Product, ProductDto, ProductInputDto>;`
- **Row-level scoping:** register a global filter query builder that applies the caller's scope (tenant/owner) to every query — inject `IHttpContextAccessor` in its constructor and filter on the claim. The claim reaches the principal the same way whichever scheme authenticated the caller (bearer token, cookie session, API key), so the filter needs no knowledge of which one is in use. The filter pipeline runs on **every controller path**: List, Search, `Details(id)` (the id goes through the same filters), and the write endpoints' existence checks — so `PUT`/`PATCH`/`DELETE` on a foreign row 404 as well.
- **What a scoping filter cannot do:** validate **create** (the client supplies the FK — stamp/verify `OwnerId` from the claim in a prepper, never trust the body) or guard **direct `IEntityService` calls** in custom code, which bypass the controller's filtered existence checks.
- **Scope before any early return.** The idiomatic query-builder shape opens with `if (so == null) return query;` — for a security filter that is a hole, because `Details(id)` and the write existence checks can run with a null search object and would skip the scoping entirely. Derive from `GlobalFilteredQueryBuilderBase<TEntity>` (it runs on every query and takes no search object), apply the ownership predicate unconditionally, and return `query.Where(_ => false)` when no identity resolves — an anonymous or stale-token call must see nothing, not everything.
- **Multiple global filters accumulate (AND).** Every registered filter whose `TEntity` the entity satisfies runs, and their predicates compose — so an `IOwnedEntity`-wide filter and a `ShoppingList`-specific one both apply. `TEntity` may be an interface, a base class, **or the concrete entity type**. The one case that does *not* stack is the key variants of a single filter family (`FilterArchivablesQueryBuilder` vs `<Guid>`): one variant runs, preferring the key-matching one. Two filters deriving separately from `GlobalFilteredQueryBuilderBase<>` are always distinct families and never suppress each other. A filter scoped to a type **no registered entity satisfies** never runs at all — startup validation warns about this, which is your signal that a security filter is inert.
- **Role/permission tiers** (admin vs editor): declare claim policies (`AddAuthorization(o => o.AddPolicy("EditorOnly", p => p.RequireClaim(...)))`) and gate the baseline with `MapControllers().RequireAuthorization("AdminOrEditor")`. For "everyone reads, some roles write", one global filter carries the tier — worked recipe with the traps in [`entities.patterns.md`](./entities.patterns.md) § Role-gated write authorization filter. ⚠️ Gate that filter on an allow-list of your own controllers, and remember `POST /{entity}/search` and `POST /{entity}/list` are reads. ⚠️ `RequireClaim`/`RequireRole` and any hand-written claim read must use the spelling the *validated* principal carries, and getting it wrong costs rows, not errors (next bullet). The claim contract is one lookup away in `security.instructions` → *Claims emitted per scheme* and *Claim normalization*. The schemes do **not** all agree on the role claim type (`role`, Entra's `roles`, and the long `ClaimTypes.Role` URI are all in play), so read roles with `User.FindRoles()` and scopes with `User.HasScope()` rather than a single `HasClaim`; on a normalized principal — every scheme except the API key — the canonical `sub`/`name`/`email`/`role` spellings are present alongside the provider's, so `RequireClaim("role", …)` does hold.
- **Verify per identity, not per endpoint.** Log in as each role (and each tenant) and compare `GET /{entity}/search` totals: an administrator sees more than an owner, a second tenant sees none of the first's. A filter that never ran, a role claim that did not survive validation, and a scope matching no registered entity all answer **200 with fewer rows** — invisible to a build, to DI validation, and to a single-user smoke test. Do this once per app after the first scoped entity works, then whenever a filter or claim changes.
- Attachment uploads store the client-supplied `Content-Type` as-is (downloads are served with `X-Content-Type-Options: nosniff`); whitelist types/extensions at the app level when accepting uploads from untrusted users.

---

## Troubleshooting

*Grouped by symptom — fetch one group, not the whole table. If nothing matches: signatures → [`entities.signatures.md`](./entities.signatures.md), namespaces → [`entities.namespaces.md`](./entities.namespaces.md), a working example → [`entities.examples.md`](./entities.examples.md).*

### Troubleshooting — reads: data missing from responses

| Problem | Likely Cause | Fix |
|---|---|---|
| Navigation properties not loaded | Missing `Includes` config or wrong flag | Check `e.Includes(...)` and that the client sends the correct `includes` flag |
| Nested collection comes back **empty** in an API response | The navigation was never eager-loaded — almost never a mapping problem | Add the eager-load in `e.Includes(...)` (never `Filter(...)` — filters pick rows, includes load navigations). Only add `AddMapping<…>()` if the child DTO shape genuinely diverges from the entity (§Step 10) |
| A relation loads, then vanishes from responses after adding another `Includes(...)` | `Includes(...)` registrations are **last-write-wins** — the second call replaced the first (single `IIncludableQueryBuilder`) | Merge all eager-loads into **one** `Includes` lambda (§Step 4) |
| Filter not applied | Query builder not registered or wrong `SearchObject` property name | Verify `e.AddFilter<>()` or `e.Filter(...)` and check property names |
| `?q=` returns nothing although the text is clearly in the row | `NormalizedContent` holds raw text while the query term is normalized — punctuation the normalizer deletes never matches | Write `NormalizedContent` through `[Normalized]` or `INormalizer.Normalize(...)` (§Normalizing — the normalize contract) |
| A row vanished after `DELETE` and no payload brings it back | The entity is `IArchivable`: the row is archived, lists hide it and `GET /{id}` 404s | Keep `IsArchived` on `TInputDto` and send `false`; read the row with `?archived=included` (or list the recycle bin with `?archived=only`) ([`entities.patterns.md`](./entities.patterns.md) → Soft Delete) |
| Mapping errors | Mapster/AutoMapper not configured or property name mismatch | Ensure `options.UseMapsterMapping()` is called; check DTO property names |

### Troubleshooting — writes: not persisting, or silently overwritten

| Problem | Likely Cause | Fix |
|---|---|---|
| Save not persisting | `SaveChanges()` not called | Base controllers call it automatically; direct `IEntityService` callers (services, jobs, seeding) must `await service.SaveChanges()` themselves. |
| A related collection gets emptied or its rows reassigned after saving the parent | The parent's DTO **carried** the collection, so `Related()` diffed it as *owned* and overwrote it to match — an empty list deletes every row (only `null` is a no-op). Common when the element entity is also independently managed via its own `.For<>()`/`IEntityService<T>` | First check the parent's `TInputDto`: dropping the collection from it makes the sync a no-op and lets both coexist. If the parent must send it, pick one authority — drop the `.For<>()`, or drop the `Related()` and load via `Include()` in the query builder (§Relationship Patterns — Decision Table) |
| A computed total zeroes after a PATCH that didn't touch the lines | The prepper collapsed `null` (collection not sent) and `[]` (delete-all) into one branch and summed an absent collection | Branch on `null` and re-read the persisted children before summing (§Step 5; [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Prepper) |
| Edits to entities saved in an earlier `SaveChanges` wave don't persist | EF change tracker is cleared after every `SaveChanges()` — the entity is now detached; later mutations are silently dropped | Re-attach with `await service.Modify(entity)` (or `Save`), then call `await service.SaveChanges()` again |
| `DELETE` archives the row but it still shows up in lists, `GET /{id}` and included collections | No archived query filter on the model — the registered builder translates the opt-ins only. Either the wiring never reached the context (non-generic `UseEntities()`, or `WireDbContext(...)` without `ArchivedQueryFilter`), or the `DbContext` was constructed by hand and never saw the service collection | Register with `UseEntities<TContext>(e => e.UseDefaults())`; for a hand-built context add `.AddArchivedQueryFilter()` to its options ([`entities.patterns.md`](./entities.patterns.md) → Soft Delete). Startup validation reports the DI case as an error |
| Restore 404s / a repeated `DELETE` 404s, only on an entity with a custom read service | The service implements `Details(id, ct)` only and inherits the default `Details(id, archived, ct)`, which cannot see archived rows | Override **both** `Details` overloads on the custom read service / repository |
| `DELETE` erases the row instead of archiving it | `IArchivable` not implemented, or `ArchivablePrimer` not registered | Implement `IArchivable`; use `UseDefaults()` |
| A `Restrict` FK lets the parent delete anyway (no 409) | SQLite enforces foreign keys only when the connection string sets `Foreign Keys=True` | Add it to the connection string ([`entities.setup.md`](./entities.setup.md) → P3) |
| Save/delete returns **409 Conflict** on a valid-looking payload | A DB constraint rejected the change — required FK points at a nonexistent parent, duplicate unique key, or a delete under `Restrict` (§Error Handling); the response detail is generic — the constraint name is in the server log (warning) | Fix the data, or validate in a prepper and `throw new EntityInputException<TEntity>(…)` → field-level 400 (parameterize by the *serviced* entity) |
| A dashboard/report endpoint answers **500 with an empty body** on a green build; the log says *"The LINQ expression … could not be translated"* or *"Translating this query requires the SQL APPLY operation"* | An untranslatable construct in the query: a record constructor in the projection, a correlated `SelectMany` (`CROSS APPLY`), **a method of your own called on the row**, or a provider-specific `EF.Functions` member (`DateDiff*` is SQL Server only) | The full list with the translating alternative for each is in [`entities.patterns.md`](./entities.patterns.md) § Cross-entity aggregates & report endpoints |

### Troubleshooting — compiler errors

| Problem | Likely Cause | Fix |
|---|---|---|
| CS0246: type or namespace name could not be found | Namespace guessed or copied from wrong source | Look up the exact namespace in [`entities.namespaces.md`](./entities.namespaces.md) |
| CS0246 on `FilteredQueryBuilderBase<>` / `IFilteredQueryBuilder<>` / `ISortedQueryBuilder<>` / `IIncludableQueryBuilder<>` | Reached for `Regira.Entities.EFcore.QueryBuilders` (which holds the *concrete* implementations) | These bases and interfaces live in **`Regira.Entities.QueryBuilders.Abstractions`** — the EFcore namespace only holds the concrete `QueryBuilder<>`/`SortedQueryBuilder<>` types you rarely reference directly |
| CS1593: wrong number of args on `SortBy(...)` | Using two-arg lambda `(query, sortBy) =>` with a simple `For<>` builder that has no `TSortBy` type parameter | Simple builders take a 1-arg lambda: `e.SortBy(query => ...)`. The 2-arg form `(query, sortBy) =>` is only available with complex builders — `For<TEntity, TSearchObject, TSortBy, TIncludes>()` or `.Complex<TSortBy, TIncludes>()` |
| CS1061: no `.AfterInput` after `.After<TImplementation>()` | The class-based `After<T>()` returns the untyped builder — `TDto`/`TInputDto` are dropped | Chain the typed `.After(...)`/`.AfterInput(...)` inline on the `UseMapping<TDto, TInputDto>()` builder; use `.After<T>()` only when nothing typed follows |
| `List(null)` compiler error | Ambiguous overload between typed and untyped variants | Omit the argument (`service.List()`) or cast: `service.List((TSearchObject?)null)`. **`Count` differs by interface:** the typed `IEntityService<…, TSearchObject>` has a parameterless `Count()`, but the universal `IEntityService<TEntity, TKey>` exposes only `Count(object? so)` — call `await service.Count(null)` when you hold the universal interface (e.g. seeding). |
| `SetSortOrder()` won't resolve on a child collection | The extension targets `IEnumerable<ISortable>`; it resolves on `ICollection<T>`/`IEnumerable<T>` only when the element type `T` statically implements `ISortable` (covariance) | If `T : ISortable`, call it directly: `items.SetSortOrder()`. If the element type isn't statically `ISortable`, cast first: `(items as IEnumerable<ISortable>)?.SetSortOrder()` |
| Wrong method name, parameters, or return type | Signature guessed or assumed | Look up the exact signature in [`entities.signatures.md`](./entities.signatures.md) |

### Troubleshooting — startup, DI and registration

| Problem | Likely Cause | Fix |
|---|---|---|
| Normalizer not running | Interceptor not wired — no `UseDefaults()` and no `WireDbContext(NormalizerInterceptors)` | Call `UseDefaults()` (or `e.WireDbContext(DbContextWiring.NormalizerInterceptors)`) — startup validation fails fast on this in Development |
| Primers not running | Interceptor not wired — no `UseDefaults()` and no `WireDbContext(PrimerInterceptors)` | Same as above |
| `EntityControllerBase<>` constructor errors / DI fails to resolve controller | Explicit constructor injecting `IEntityService<>` declared inside the controller class | Remove the constructor — `EntityControllerBase<>` resolves its service internally via the framework; no constructor is needed or expected |
| `EntityWrappingServiceBase` — infinite loop | Inner service is the wrapper itself | Ensure `UseEntityService<T>()` registers the wrapper; `AddTransient` registers the interface |
| Custom Mapster mapping silently ignored in one module | Several DI modules each call `UseMapsterMapping()` — each registers a `TypeAdapterConfig` singleton and DI resolves the **last** one | Register the module whose custom mappings must win **last** (or consolidate all `AddMapping`/`MapWith` config into a single `UseMapsterMapping()` call) |
| Startup throws a `LicenseException` naming entities | A registration bucket is full (free tier: 5 simple + 2 complex) | Compare the startup log line `N simple / N complex registered → tier =` against your §Step 0 tally, then apply an overflow remedy from §Step 0 |

### Troubleshooting — packages and provider versions

| Problem | Likely Cause | Fix |
|---|---|---|
| `dotnet restore` fails with "Detected package downgrade" | Project targets a framework that is lower than what a dependency requires | Use latest `<TargetFramework>` in the `.csproj` |
| Clean build, then `System.MissingMethodException` (e.g. `RelationalQueryCompilationContext..ctor`) on the **first query** | The EF Core **provider** package major doesn't match the EF Core version Regira targets for your TFM (Regira's EF Core packages multi-target) | Pin the provider (`Microsoft.EntityFrameworkCore.Sqlite`/`.SqlServer`/`Npgsql.EntityFrameworkCore.PostgreSQL`) to the matching major: `net8.0`/`net9.0` → `9.x`, `net10.0` → `10.x` |

---

## See Also

- [Entities Examples](./entities.examples.md) - Code examples and patterns (incl. query-extensions reference)
- [Entities Patterns](./entities.patterns.md) - Feature recipes (soft delete, audit, hierarchy, bulk, interceptors, auto-truncate)
- [Entities Blueprints](./entities.blueprints.md) - Domain blueprints (stakeholders, entity labels, multi-tenancy, recursive entities, identity users, virtual entities)
- [Entities Namespaces](./entities.namespaces.md) - Namespace reference
- [Entities Signatures](./entities.signatures.md) - Exact method signatures for all interfaces and classes
