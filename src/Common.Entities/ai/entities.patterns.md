# Regira Entities — Feature Patterns

Per-feature recipes, loaded on demand. The always-loaded spine ([`entities.instructions.md`](./entities.instructions.md)) keeps only a one-line trigger per pattern; the detail lives here. Core modeling patterns (Master-Detail, Many-to-Many) and the relationship decision table stay in the spine.

## Bulk insert / update (batching through `IEntityService`)

The write service exposes single-item `Add`, `Modify`, `Save`, and `Remove` plus a single `SaveChanges()` (`IEntityWriteService<TEntity, TKey>`). Those per-item calls only **track** the change — no INSERT or UPDATE is issued until `SaveChanges()`. To insert or update many rows, loop the per-item calls and flush **once**:

```csharp no-compile
// Resolve the always-registered IEntityService<TEntity, TKey> outside a controller (see §Step 13).
var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();

foreach (var product in products)
    await service.Add(product);   // tracks only — no DB round-trip yet

await service.SaveChanges();      // one round-trip flushes the whole batch
```

> ⚠️ **Writes batch; preppers do not.** `Add()` is only free while nothing is registered against the entity — a prepper that queries the `DbContext` (FK validation, price lookup, a per-row `FindAsync`) runs **inside the loop**, once per item. A 500-row seed wave against an entity with a two-query prepper issues ~1,000 round-trips before the single flush, and nothing in the code shape shows it. Hoist the lookup into a dictionary built once before the loop.

Two timing facts drive how you order a bulk run:

- **Auto-increment `Id` is populated at `SaveChanges()`, not at `Add()`.** For `IEntityWithSerial` entities `product.Id` is `0` until the batch is saved. When a later step needs a parent's generated `Id` — whether as a foreign key (`child.ParentId = parent.Id`) **or as a dictionary/lookup key** (`map[parent.Id] = …`) — save the parents first, then read their `Id`s. Keying a lookup on `parent.Id` *inside* the insert loop collapses every not-yet-saved row onto `0`.
- **The change tracker is cleared after every `SaveChanges()`.** Entities saved in an earlier flush are detached; mutating one and calling `SaveChanges()` again persists nothing. Split very large runs into waves, and re-attach anything you touch in a later wave with `await service.Modify(entity)` before saving again.
- **Pre-set `Created`/`LastModified` survive — no SQL back-dating needed.** `HasCreatedDbPrimer` stamps `Created` only when it is `DateTime.MinValue`, and `HasLastModifiedDbPrimer` stamps only on update. Assign historical dates before the first `SaveChanges()` to seed realistic timelines.

### Multi-wave seeding

Applies the first fact directly: save parents, then assign their generated `Id`s to children.

```csharp no-compile
// Wave 1 — parents
foreach (var c in rootCategories) await categories.Add(c);
await categories.SaveChanges();                 // Ids now populated; tracker cleared

// Wave 2 — children referencing wave-1 Ids
foreach (var (parent, child) in childPairs) child.ParentId = parent.Id;
foreach (var child in childCategories) await categories.Add(child);
await categories.SaveChanges();
```

### Seeding owned / join rows (m2m, hierarchy joins)

A collection managed by `e.Related()` is *owned* and has **no** `IEntityService<T>` of its own (see the relationship decision table), so it can't go through the service loop above. Two ways in:

- **Through the parent's navigation** — set the owned collection before adding the parent; the parent's `Related()` prepper persists the join rows on `SaveChanges()`:
  ```csharp no-compile
  product.Categories = [cat1, cat2];   // ProductCategory join rows
  await products.Add(product);
  await products.SaveChanges();
  ```
- **Straight on the DbContext** — for a standalone/self-referencing join (e.g. `RelatedCategory`) with no parent write path, add the rows on the context:
  ```csharp no-compile
  db.RelatedCategories.Add(new RelatedCategory { ParentId = a.Id, ChildId = b.Id });
  await db.SaveChangesAsync();
  ```

Prefer the parent-navigation form when the join hangs off an entity you're already saving; reach for the DbContext only for joins with no owning write path.

> **You own de-duplication on the DbContext path.** Nothing dedupes rows you `Add` directly — if the join has a unique index (e.g. `(ParentId, ChildId)`), a repeated edge throws `UNIQUE constraint failed` on `SaveChanges()`. `Distinct()` your edge list (or skip pairs already present) before adding.

### Creating attachments in code

Resolve the per-owner **link** service registered by `HasAttachments` — `IEntityService<TEntityAttachment, int>` — and add a link that carries a **nested `Attachment`** with the bytes. The attachment write pipeline (bytes→file conversion + storage-key `Identifier` generation) runs on `SaveChanges()`, writing the file, filling `Path`/`Length`, and assigning `AttachmentId`:

```csharp no-compile
// IEntityService<ProductAttachment, int> — registered by HasAttachments.
// Not IAttachmentService<…>: that interface is for the shared Attachment base only, and a link
// entity is an IEntityAttachment (not an IAttachment), so the typed overload can't bind to it.
var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();

foreach (var product in products)
    await links.Add(new ProductAttachment
    {
        ObjectId = product.Id,
        Attachment = new Attachment { FileName = "spec.pdf", ContentType = "application/pdf", Bytes = bytes }
        // (set Attachment.Identifier to pick the storage key; otherwise it is derived from FileName)
    });

await links.SaveChanges();   // one flush; pipeline writes files, fills Path/Length, assigns AttachmentId
```

- The bytes→file step runs only inside this pipeline, **not** during an owner-graph cascade. Set the nested `Attachment` on the link, don't nest under `owner.Attachments` and save the owner.
- The `New*` fields (`NewBytes`/`NewFileName`/`NewContentType`) **replace** an existing attachment's content — they don't create one. Setting them without a nested `Attachment` leaves `AttachmentId` at `0` and fails the FK.

## In-code recipes (how_to)

Task-oriented answers for the in-code jobs that don't map onto a single type — the source of the MCP
`how_to` tool. Each recipe below carries a `<!-- how_to: key=… aliases=… -->` marker; the knowledge-base
builder parses these markers and the body under them (up to the next heading) into the `how_to` tool's
recipe set, so the tool and this guide never drift. To add or edit a recipe, edit the body here.

### Create an attachment in code (seeding / import)
<!-- how_to: key=create-attachment aliases=attachment,attachments,attach,file,files,upload,bytes,create,add -->
Resolve the **per-owner link** service registered by `HasAttachments` —
`IEntityService<TEntityAttachment, int>` — and add a link carrying a **nested `Attachment`**
with the bytes. The attachment write pipeline runs on `SaveChanges()`: it writes the file,
fills `Path`/`Length`, and assigns `AttachmentId`.

```csharp no-compile
// Registered by HasAttachments. NOT IAttachmentService<…>: that interface is for the shared
// Attachment base only, and a link entity is an IEntityAttachment (not an IAttachment).
var links = sp.GetRequiredService<IEntityService<ProductAttachment, int>>();

await links.Add(new ProductAttachment
{
    ObjectId = product.Id,
    Attachment = new Attachment { FileName = "spec.pdf", ContentType = "application/pdf", Bytes = bytes }
});
await links.SaveChanges(); // pipeline writes the file, fills Path/Length, assigns AttachmentId
```

- The bytes→file step runs only inside this pipeline, **not** during an owner-graph cascade.
- `New*` fields (`NewBytes`/`NewFileName`/`NewContentType`) **replace** an existing
  attachment's content — they don't create one. Without a nested `Attachment`, `AttachmentId`
  stays `0` and the FK fails.

**See:** `get_package("Regira.Entities", section: "patterns", heading: "Bulk insert / update")`
and `get_package("Regira.Entities", section: "examples", heading: "Attachments")`.

### Bulk insert / seed many rows
<!-- how_to: key=bulk-insert aliases=bulk,seed,seeding,import,addrange,batch,many,insert,loop -->
There is no `AddRange`. The per-item `Add`/`Modify`/`Save`/`Remove` calls only **track**
changes; nothing hits the database until a single `SaveChanges()`. Loop the per-item calls
and flush **once**:

```csharp no-compile
var service = sp.GetRequiredService<IEntityService<Product, int>>();
foreach (var product in products)
    await service.Add(product);   // tracks only — no DB round-trip yet
await service.SaveChanges();       // one round-trip flushes the whole batch
```

- Auto-increment `Id` is populated at `SaveChanges()`, not `Add()`. Save parents first, then
  assign child FKs.
- The change tracker is cleared after every `SaveChanges()`; re-attach with `Modify` before
  touching an already-saved entity in a later wave.

**See:** `get_package("Regira.Entities", section: "patterns", heading: "Bulk insert / update")`.

### Back-date Created / LastModified when seeding
<!-- how_to: key=back-date-timestamps aliases=timestamp,timestamps,created,lastmodified,date,dates,historical,backdate,seed -->
Pre-set timestamps survive — no raw SQL needed. `HasCreatedDbPrimer` stamps `Created` only
when it is `DateTime.MinValue`, and `HasLastModifiedDbPrimer` stamps only on update. Assign
historical dates before the first `SaveChanges()`:

```csharp no-compile
await service.Add(new Ticket { /* … */ Created = new DateTime(2024, 3, 1) });
await service.SaveChanges(); // the pre-set Created is kept; the primer does not overwrite it
```

**See:** `get_package("Regira.Entities", section: "patterns", heading: "Bulk insert / update")`.

### Which service is registered for an entity (incl. attachments)
<!-- how_to: key=registered-service aliases=registered,service,resolve,getrequiredservice,inject,injection,which,what -->
Every entity registered with `For<>()` resolves as `IEntityService<TEntity, TKey>` (and the
search-object / read / write / repository variants). Attachments specifically:

- **Shared `Attachment` base** (via `WithAttachments`) → `IEntityService<Attachment, int>`.
- **Per-owner link** (via `HasAttachments`) → `IEntityService<TEntityAttachment, int>`
  (e.g. `IEntityService<ProductAttachment, int>`).

`IAttachmentService` / `IAttachmentService<…>` are **not** registered for link entities — a
link is an `IEntityAttachment`, not an `IAttachment`, so that typed overload can't bind to it.
Use `IEntityService<…>`.

**See:** `get_package("Regira.Entities", section: "examples", heading: "Attachments")`.

### Sync nested owned collections (children of children)
<!-- how_to: key=nested-related aliases=nested,grandchild,grandchildren,sub-collection,subcollection,related,two-level,deep,graph -->
An owned child collection whose rows own a collection of their **own** (order → lines → line
discounts; party → relationships → relationship contact data) syncs in one registration — the
`configure` callback of `Related()` nests another `Related()`:

```csharp no-compile
e.Related(
    item => item.ChildRelationships, item => item.ChildRelationships?.Prepare(),   // level 1: parent-level prepare
    rel => rel.Related(r => r.ContactData, r => r.ContactData?.Prepare())          // level 2: per-row nested sync
);
```

The nested sync runs **per level-1 row**, matched to its original by `Id`, and diffs that row's
sub-collection the same way the outer sync diffs level 1 (rows of a *new* level-1 item are all
tracked as added). Deeper levels nest the same way. Neither level needs its own `.For<>()`,
controller, budget slot, or a hand-written prepper — write a prepper only for logic beyond
collection syncing.

**See:** `get_package("Regira.Entities", section: "blueprints", heading: "Stakeholders")` for the
full worked model, and §Step 8 in the instructions for the `Related()` signature.

## Soft Delete

**Opt-in, and often the wrong default.** Soft delete takes a large share of this guide's warning budget, which can read as "every deletable entity should be `IArchivable`". It should not. No `IArchivable` entity means no archived query filter, no `?archived=` machinery, and none of the hazards below apply — a plain `DELETE` is the simpler contract. Reach for it when the row's history must survive its removal (an `Asset`, an `Order`), not for reference data (see the decision table below) and not for "an employee left" — a plain `IsActive` flag keeps them out of pickers without pulling their rows out of the audit trail.

**One thing to write: implement `IArchivable` on the entity.** `UseEntities<TContext>(e => e.UseDefaults())` supplies the rest — `ArchivablePrimer`, `FilterArchivablesQueryBuilder`, and the `e => !e.IsArchived` EF query filter itself, wired into the context's options (`DbContextWiring.ArchivedQueryFilter`) and applied at model finalization. The `DbContext` needs no Regira call.

The filter lands on every `IArchivable` entity type — root types only, since EF takes a query filter on the root of a hierarchy (covering the derived types) and an owned type is configured through its owner. Being a real EF filter it also propagates into `Include(...)`. It is the *only* thing that excludes archived rows: the query builder translates the `?archived=` opt-ins alone, `Excluded` composes nothing. Archived filtering is therefore EF-only — on a non-EF `IQueryable`, `Excluded` filters nothing.

> ⚠️ **A `DbContext` you construct yourself never sees that wiring.** `new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()…Options)` — integration tests, an `IDesignTimeDbContextFactory`, a seeding tool — builds its model without consulting the service collection, so archived rows stay visible *there* while the host hides them. Add the filter to those options explicitly:
>
> ```csharp no-compile
> new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
>     .UseSqlite(connection)
>     .AddArchivedQueryFilter()          // Regira.Entities.EFcore.Extensions
>     .Options);
> ```

**Wiring it in the `DbContext` instead** is still supported — for a setup that opted out of `DbContextWiring.ArchivedQueryFilter`, or one that prefers the model to state it. It must come *after* your own `HasQueryFilter(...)` calls and only once; calling it while the automatic wiring is on is harmless (the convention skips an entity type that already carries the filter):

```csharp no-compile
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    // entity configuration — including any HasQueryFilter of your own — goes above
    modelBuilder.SetArchivedQueryFilter();   // Regira.Entities.EFcore.Extensions — last, exactly once
}
```

> ⚠️ **A model that ends up with no archived filter leaves archived rows visible everywhere** — lists, `GET /{id}`, included collections — while `DELETE` keeps flagging rows nobody hides. Startup catches it: `ArchivedQueryFilterValidator` raises a validation **error** naming the entity, which stops the host in Development. It inspects the built model, so it reports the outcome regardless of which route was supposed to install the filter.

### ⚠️ Before you make reference data `IArchivable` — read this

EF logs one `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` per relationship whose required principal is `IArchivable` — *"Entity 'Employee' has a global query filter defined and is the required end of a relationship with 'CreditAllocation'"*. **Whether that warning is noise or a data bug depends on one question: is the principal an aggregate parent, or reference data?**

| The principal is… | Archiving one means | Do |
|---|---|---|
| an **aggregate parent** — `Order` → `OrderLine`, an `e.Related()` child with no `For<>()` of its own | its children go with it, which is the intent | suppress the warning, and read on for the dependent-you-query-directly case |
| **reference data** — a category, a status, a type, a lookup any separately-registered entity points at | every row referencing it silently vanishes from list results | **do not make it `IArchivable`.** Delete it for real and let `OnDelete(Restrict)` return **409** while it is in use. Or make the FK **optional**, so the dependent survives with a null navigation |

**What "silently vanishes" means, precisely.** The archived filter is a real EF filter on the principal, so it propagates into `Include(...)`; where the navigation is **required** EF composes it as an inner join and the dependent rows drop out of the **items** projection. The **count** query carries no includes, hence no join, hence no elimination. So `/search` reports a total its own page does not contain:

```
baseline                                 count 495 | items 100   (pageSize=100)
after archiving one of 12 categories     count 495 | items  94   ← short page, no error
scoped to that category                  count  43 | items   0   ← 43 rows nobody can reach
```

Startup validation reports this shape (`ArchivableReferenceDataValidator`, a **warning** naming both entities) — it fires only for a dependent that is separately registered, since an `e.Related()` child is never queried on its own.

**If it really is an aggregate parent**, suppress the warning per context with `.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))` on the `AddDbContext` options. And for any dependent you **also query directly**, mirror the filter — `modelBuilder.Entity<OrderLine>().HasQueryFilter(x => !x.Order!.IsArchived)` — which is what keeps its count and items in agreement. A filter you add to a dependent that is **not itself `IArchivable`** never collides with the named archived filter: the archived filter only touches `IArchivable` types, so your filter on such a dependent is unopposed.

### Attachments on an archivable owner

The mirrored filter above needs a **navigation** on the dependent to bind to (`x.Order`), and a per-owner
attachment link has none: `EntityAttachment` carries `ObjectId` / `ObjectType` only, and the model
configuration in [`entities.examples.md`](./entities.examples.md) → Attachments maps it with a bare
`WithOne()`. So on an `IArchivable` owner the validator's remedy does not compile until you add the
navigation yourself. This is a common intersection, not an edge case: soft delete belongs on aggregate
parents, which are exactly the entities that own files, and the per-owner link is always separately
registered (it costs its own simple slot).

Three edits, in this order:

```csharp no-compile
public class TicketAttachment : EntityAttachment
{
    public TicketAttachment() => ObjectType = nameof(Ticket);
    public Ticket? Ticket { get; set; }          // exists so the archived filter has something to bind to
}
```
```csharp no-compile
modelBuilder.Entity<Ticket>(e =>
    e.HasMany(x => x.Attachments).WithOne(a => a.Ticket!)      // was WithOne() — now points at the navigation
        .HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id));

modelBuilder.Entity<TicketAttachment>(e =>
{
    e.HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
    e.HasQueryFilter(x => !x.Ticket!.IsArchived);             // your own filter — nothing to order it against
});
```

The link is not itself `IArchivable`, so this filter never collides with the named archived filter, and
`/tickets/{id}/attachments` then agrees with the parent: archiving the ticket hides its files from both
`count` and `items`.

> **Your own `HasQueryFilter(...)` on an `IArchivable` entity keeps applying.** The archived filter is a **named** filter (`"Regira:Archived"`), and EF Core 10 refuses to build a model that mixes anonymous and named filters — so an anonymous filter of yours on the same entity is re-registered under `"Regira:Model"`. It runs exactly as before and, being named, survives the `?archived=` opt-ins, which drop `"Regira:Archived"` and nothing else. The wired convention does this at model finalization, after everything `OnModelCreating` configured, so ordering is not yours to get right. It *is* yours to get right when you call `SetArchivedQueryFilter()` in the `DbContext`: call it before your own anonymous filter and the later one sits unnamed beside the named one, and the model fails to build.

| Route | Behaviour |
|---|---|
| `DELETE /{id}` | soft-delete — sets `IsArchived = true`, the row survives, real affected count, idempotent |
| `GET /`, `GET /search` | archived excluded by default; `?archived=only` → the recycle bin; `?archived=included` → both |
| `GET /{id}` | **404** for an archived row |
| `GET /{id}?archived=included` | resolves the archived row |
| write path (`PUT` / `PATCH` / `POST /save` / the `DELETE` lookup) | always archived-inclusive, so restore works with no client change |
| `IsArchived` on `TInputDto` | **keep it** — generated forms hide it and nothing excludes it. Absent from the DTO preserves the persisted value; an explicit `false` un-archives |
| included collections | archived children are excluded too — the filter is an EF query filter, so it propagates into every `Include()` (`net10.0`; see the `net8.0` note below) |
| custom read service (`UseReadService<>`/`HasRepository<>`) | ⚠️ override **both** `Details(id, ct)` **and** `Details(id, archived, ct)` — the second is a default interface member, so inheriting it leaves the write path unable to see archived rows and restore 404s |

One knob drives the read side: `ISearchObject.Archived` (`ArchivedFilter?`), bound from `?archived=`.

| `Archived` | Rows returned |
|---|---|
| `Excluded` | non-archived only |
| `Included` | both |
| `Only` | archived only — the recycle bin |
| `null` | falls back to `EntityQueryOptions.DefaultArchivedFilter` (`Excluded`), settable on `UseEntities()` |

On `net10.0` the two opt-ins suspend the archived filter **by name** and nothing else: a `HasQueryFilter(...)` of your own keeps applying, as does every `IGlobalFilteredQueryBuilder`. Row security is never widened by `?archived=`.

`?archived=` binds **case-insensitively** (standard ASP.NET Core enum binding), so `?archived=included`, `included`, `Included` and `INCLUDED` all resolve — the lowercase spelling used throughout these guides matches the front-end enum member, and the C# member is PascalCase.

> ⚠️ **On `net8.0` (EF Core 9) no archived query filter is installed at all** — neither route does anything there. EF Core 9 has no named query filters, and honouring the opt-ins would take the untargeted `IgnoreQueryFilters()`, which suspends *your* query filters too (and the write path resolves its row archived-inclusive on every update, so that would be a cross-tenant write). Archived rows are excluded by `FilterArchivablesQueryBuilder` at the root of the query instead: soft delete works, nothing is ever suspended — but archived rows are **not** filtered out of an `Include(...)`d collection.

`IsArchived` stays the **entity** property: what `DELETE` sets and what a restore clears. `Archived` is the **search** knob; the two never mix.

> ⚠️ **`DELETE` stops deleting the moment an entity implements `IArchivable`.** Same route, same 200, same affected count — the row is only flagged, and nothing in the response distinguishes the two. Decide per entity whether callers expect erasure; there is no hard-delete endpoint on an archivable entity.

⚠️ Restore is the reason `IsArchived` must stay on `TInputDto`. Excluding it as "server-owned" (it reads like an auto-generated flag) still leaves the row writable — the write path is archived-inclusive — but **no payload can clear the flag**, while lists hide the row and `GET /{id}` 404s.

⚠️ Preserving the persisted flag is DTO-only: on an entity-typed write surface (`EntityControllerBase<TEntity>`, or auto-endpoints without `UseMapping<TDto, TInputDto>()`) a `PUT` that omits `isArchived` deserializes it to `false` and un-archives the row — the entity always declares the property, so omitted and explicit `false` are indistinguishable.

## Single-field PATCH / state toggle

Flip one field (e.g. `IsActive`) without a full update: expose it on `TInputDto` and let the base `PATCH /{id}` map only the supplied fields — no custom action needed.

```bash
curl -X PATCH {base}/xs/{id} -H "Content-Type: application/json" -d '{ "isActive": false }'
```
```csharp no-compile
// Server-side (seeding/jobs) — there is no service.Patch; load, flip, Modify, persist:
var item = await service.Details(id);
item!.IsActive = false;
await service.Modify(item);
await service.SaveChanges();      // base controllers SaveChanges for you; direct callers must
```

> **Toggling a join/link row?** A row managed via `e.Related()` is *owned* by the parent, but it can still get its own PATCH route. Two supported ways:
>
> **1. Give the child its own `.For<>()` + controller** (costs a budget slot, gets the full endpoint set):
> ```csharp
> e.For<ProductTag, int, ProductTagSearchObject>(/* … */);
> // class ProductTagController : EntityControllerBase<ProductTag, int, ProductTagSearchObject, ProductTagDto, ProductTagInputDto>;
> // then: PATCH {base}/product-tags/42 -d '{ "isActive": false }'   (IsActive must be on ProductTagInputDto)
> ```
> ✅ Safe **as long as the parent's `TInputDto` does not carry the collection.** With it `null`, the parent's sync short-circuits and never touches these rows, so the toggle is authoritative. Startup validation still warns (it can't inspect DTO shapes) — expected here.
> ⚠️ If the parent *does* send the collection, its next save re-diffs and reverts the toggle. Remove the collection from the parent's input DTO, or drop one of the two registrations.
>
> **2. Hand-write a minimal PATCH controller, no `.For<>()`** — when you only need the toggle, not a whole endpoint set. No second registration means **no budget slot and no validator warning**; inject the `DbContext` directly:
> ```csharp no-compile
> [HttpPatch("product-tags/{id:int}/active")]
> public async Task<IActionResult> SetActive(int id, [FromBody] bool isActive)
> {
>     var row = await db.ProductTags.FindAsync(id);
>     if (row == null) return NotFound();
>     row.IsActive = isActive;
>     await db.SaveChangesAsync();   // no base controller here — save explicitly
>     return NoContent();
> }
> ```
> The same condition applies: the parent must not send the collection back, or it re-diffs over your write.
>
> ⚠️ **`[FromBody] bool` needs a content type the browser client does not send.** axios JSON-serializes objects
> and arrays, but passes a bare `true`/`false` through **without** `Content-Type: application/json`, so
> ASP.NET answers **415** — while `curl -H "Content-Type: application/json"` succeeds, which makes the API look
> fine and the SPA look broken. Either set the header at the call site
> (`axios.patch(url, isActive, { headers: { "Content-Type": "application/json" } })`) or take a one-property
> body (`{ "isActive": true }`), which serializes correctly with no special-casing.

## Cross-entity aggregates & report endpoints

A dashboard total (spend by month, counts per status, top suppliers) belongs to **no single entity**, so there
is no `SearchObject` that can express it and no entity service that should own it. Write a plain
`ControllerBase` alongside the entity controllers and query the `DbContext` directly:

```csharp no-compile
[ApiController, Route("dashboard")]                 // RoutePrefixConvention still prefixes it
public class DashboardController(AppDbContext db) : ControllerBase
{
    [HttpGet("spend-by-month")]
    public async Task<IActionResult> SpendByMonth(int year) =>
        Ok(await db.Interventions.AsNoTracking()
            .Where(x => x.Date.Year == year)
            .GroupBy(x => x.Date.Month)
            .Select(g => new { Month = g.Key, Total = g.Sum(x => x.Cost) })
            .ToListAsync());
}
```

This is the sanctioned shape, not a deviation — but keep it **read-only**, and know what you are outside of:

- ✅ `AsNoTracking()`, `GroupBy`/`Sum` server-side. Never fetch rows to aggregate in the client, and never
  aggregate in the SPA across several pooled services.
- ⚠️ It **bypasses the entity pipeline** — no processors, no normalizers, no `?includes=`, and none of the
  query builders. **`IGlobalFilteredQueryBuilder` row security (tenant/owner) does not apply unless you repeat
  the predicate in this query**, which on a multi-tenant or row-scoped app makes the omission a data leak
  rather than a missing feature. Archived rows are the one thing you get for free — but only on `net10.0`,
  where the archived filter is a real EF query filter; on `net8.0` no such filter exists and archived rows
  reach the aggregate, so add `.Where(x => !x.IsArchived)` there.
- ⚠️ No write path here. An aggregate endpoint that also mutates re-creates the dual-write problem the
  one-writer rule exists to prevent.
- ⚠️ **Project a `GroupBy` into an anonymous type, then shape the DTO after `ToListAsync()`.** A positional
  **record constructor** inside the projection (`.Select(g => new Bucket(g.Key, g.Count()))`) does not
  translate — EF throws *"The LINQ expression … could not be translated"*, which surfaces as a 500 with an
  empty body on a green build. The anonymous type above translates; map it to your DTO in memory.
- ⚠️ **Prefer a `join` over a correlated `SelectMany`.** `db.A.SelectMany(a => db.B.Where(b => b.AId == a.Id)…)`
  compiles to `CROSS APPLY`, and the SQLite provider these guides default to rejects it outright
  (*"Translating this query requires the SQL APPLY operation"*). The same shape in a **processor** that
  aggregates children into `[NotMapped]` fields (§Step 7) hits it for the same reason.
- On the client this is a plain `useAxios()` call with its own `useFeedback()` — not a slice, not a pooled
  store. See the front-end `entities.patterns` → *Custom endpoints on a service* and *Feedback for custom
  saves*.

### Domain actions on an entity resource

A state machine (submit / approve / reject / reopen) is neither a CRUD write nor a report. It belongs beside
the entity controller on the **same** resource route, as a second controller with distinct templates — this
is supported, and keeps the guard off the hot path where every ordinary PATCH would pay for it:

```csharp
public enum RequestStatus { Draft, Submitted, Approved }

public class CreditRequest : IEntity<int>
{
    public int Id { get; set; }
    public RequestStatus Status { get; set; }
    public DateTime? DecidedOn { get; set; }
}

public class DecisionInput { public string? Reason { get; set; } }

[ApiController, Route("credit-requests")]                                    // same prefix as the entity controller
public class CreditRequestWorkflowController(IEntityService<CreditRequest, int> service) : ControllerBase
{
    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> Approve(int id, [FromBody] DecisionInput input)
    {
        var item = await service.Details(id);
        if (item == null) return NotFound();
        if (item.Status != RequestStatus.Submitted)
        {
            ModelState.AddModelError(nameof(item.Status), "Only a submitted request can be approved.");
            return BadRequest(ModelState);
        }

        item.Status = RequestStatus.Approved;                                // …decide, stamp
        item.DecidedOn = DateTime.UtcNow;
        await service.Modify(item);
        await service.SaveChanges();                                         // no base controller here — save explicitly
        return Ok(item);
    }
}
```

- ⚠️ **Return the 400 yourself, via `ModelState` + `BadRequest`.** `EntityInputException<T>` becomes a
  field-level 400 only inside `ControllerExtensions.Save` (the path `EntityControllerBase` writes through)
  and `EntityAttachmentControllerBase`; nothing maps it globally, so from a plain `ControllerBase` it escapes
  unhandled as a **500**. That applies to a *prepper* too: preppers run in `EntityWriteService.PrepareItem`,
  reached from `Add`/`Modify` — so one throwing during the `service.Modify(item)` above escapes this action
  just the same. Only a write that goes **through** `ControllerExtensions.Save` is inside the catch. Wrap the
  call in `try`/`catch (EntityInputException<CreditRequest> ex)` and map `ex.InputErrors` into `ModelState`
  if a prepper is the one guarding.
- **Write through `IEntityService`** — keeps preppers, primers and row security in play, so the action and the
  CRUD route cannot diverge.
- ⚠️ **Keep the transitioned fields on `TInputDto`.** Excluding them makes every ordinary PATCH reset them to
  `null`/default (§Server-owned / immutable fields on update). Leave `Status`/`DecidedOn` on the DTO and let
  this controller be their real writer; take off only fields nothing outside the entity pipeline writes
  (`Code`, computed totals).
- Distinct route templates mean no collision — ASP.NET resolves `POST /credit-requests/{id}/approve` and the
  base controller's `PATCH /credit-requests/{id}` independently.

## Owned children that are both sortable and individually togglable

**Let write cardinality decide who owns each field.** A field only meaningful relative to its siblings is a *collection-level* write and rides the parent's `TInputDto`; a field that changes one row in isolation is a *per-row* write and gets its own endpoint.

| Field | Cardinality | Owner |
|---|---|---|
| `SortOrder` | affects many rows at once — positions are relative | parent's `TInputDto` (via `SetSortOrder()`) |
| `IsDone` / `IsActive` | affects exactly one row | custom PATCH route |

> ⚠️ **The sync rewrites whole rows, not just the fields you own at collection level.** Once the parent sends the collection, `UpdateRelatedCollection` marks every matched row `Modified` and writes **all** its values from the payload — so the per-row field resets to `default` on every list save unless a parent-level `e.Prepare(entity, dbContext)` hook (registered **before** `Related()`) re-stamps it from the store first.

> **→ See:** [`entities.examples.md`](./entities.examples.md) — *Additional Patterns > Sortable owned child with a per-row toggle* for the full slice: entity, both DTOs, the guard, the PATCH controller, and the registration-order and covariance caveats.

## Renamed DTO property

Mapster maps by name only — a DTO property renamed from its entity counterpart (e.g. `OrderDto.Lines` for `Order.OrderLines`) is silently left `null` in both directions. Wire both directions inline on the **typed** mapping builder:

```csharp no-compile
e.UseMapping<OrderDto, OrderInputDto>()
    .After((order, dto) => dto.Lines = order.OrderLines?
        .Select(l => new OrderLineDto { Id = l.Id, ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice })
        .ToList())
    .AfterInput((input, order) => order.OrderLines = input.Lines?
        .Select(l => new OrderLine { Id = l.Id, ProductId = l.ProductId, Quantity = l.Quantity })
        .ToList());
```

Keep both on the typed chain — the class-based `.After<TImplementation>()` returns the untyped builder, after which `.AfterInput(...)` no longer compiles (CS1061). Prefer keeping DTO property names aligned with the entity so convention mapping just works.

## Writing to a related entity from a prepper

The typed `e.Prepare(async (entity, dbContext) => …)` overload (Step 8) hands you the strongly-typed `DbContext`, so a prepper can mutate **related** rows, not just the entity's own fields — decrement stock, bump a denormalized counter. Rows you load are tracked, so the parent's `SaveChanges()` persists them in the same transaction:

```csharp no-compile
e.Prepare(async (order, dbContext) =>
{
    foreach (var line in order.Lines ?? [])
    {
        var product = await dbContext.Set<Product>().FindAsync(line.ProductId)
            ?? throw new EntityInputException<Order>($"Product {line.ProductId} not found");
        if (product.StockQuantity < line.Quantity)
            throw new EntityInputException<Order>($"Insufficient stock for {product.Title}");
        product.StockQuantity -= line.Quantity;   // tracked → saved with the order
    }
});
```

- Throw `EntityInputException<Order>` on a rule breach — parameterized by the **serviced** entity (`Order`, the one with the `.For<>()`/controller), **not** the related `Product`. The endpoint only catches `EntityInputException<TEntity>` for its own `TEntity`, so a mismatched type argument escapes as an unhandled **500** instead of a **400**.
- On **update** the decrement would compound — diff against the original quantities (prepper-with-original, or a primer branching on `EntityState.Modified`) and apply only the delta.

## Server-owned / immutable fields on update

⚠️ `TInputDto` deliberately omits server-owned fields (`OwnerId`-style FKs, generated codes, computed
totals) — so on PUT/PATCH they map onto the entity as `null`/default and are **written back that way**. This
is about **scalars and FKs**: a null/default value maps straight onto the column and is written. (Owned
*collections* are governed separately — the `Related()` sync leaves a `null` collection untouched and treats
`[]` as delete-all; keep them off the parent's input DTO or declare the property nullable and uninitialized,
per Step 5.) It compiles, returns 200, and corrupts data: a computed `Total` becomes `0` after a status-only PATCH, a
`[Required]`/NOT NULL column 500s on the first edit, and "generate if empty" logic re-mints values on every
save. This applies to PATCH too — the merge patch is applied through `TInputDto`, so a field the DTO does
not declare cannot survive it.

After Step 5, list every field you excluded from `TInputDto`; each needs restoring on update. The idiomatic
way is a **primer** — the same mechanism the built-in `HasCreatedDbPrimer` uses to protect `Created`: on a
`Modified` entry, copy the stored value back from `entry.OriginalValues`. Stamp on create, restore on update:

```csharp no-compile
public class ShoppingListOwnerPrimer(IHttpContextAccessor httpContextAccessor) : EntityPrimerBase<ShoppingList>
{
    public override Task PrepareAsync(ShoppingList entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Modified)                                  // update — restore the stored owner
            entity.OwnerId = (int?)entry.OriginalValues[nameof(entity.OwnerId)];
        else if (entry.State == EntityState.Added)                                 // create — stamp from the claim, never the body
            entity.OwnerId = httpContextAccessor.HttpContext?.User.FindUserId();
        return Task.CompletedTask;
    }
}
// Registration: e.AddPrimer<ShoppingListOwnerPrimer>();  (+ AddHttpContextAccessor() once)
```

`entry.OriginalValues` carries only scalars/FKs (never navigations) and already holds the stored values, so
the restore is authoritative and needs no query. The same shape guards any immutable value (created-by,
invoice number, source-system id).

**Computed from the stored graph?** A `Total` diffed against the original lines needs the whole prior row —
use a prepper instead: `EntityPrepperBase<T>.Prepare(modified, original, …)` hands you the full `original`
entity (`null` on create); register with `e.AddPrepper<T>()`.

## Aggregates over a non-owned child collection

The case above assumes the children ride the parent's DTO. When they don't — the **optional parent FK** row
of the Relationship Patterns decision table, `Invoice.Total` summed from the `Intervention`s that point at it
via `InvoiceId?` — there is no incoming collection to diff, and there must not be: the child owns that write.
Read the aggregate from the store instead:

```csharp no-compile
e.Prepare(async (invoice, dbContext) =>
{
    invoice.SubTotal = invoice.Id > 0                          // nothing points at an unsaved parent yet
        ? await dbContext.Interventions.AsNoTracking()
            .Where(i => i.InvoiceId == invoice.Id).SumAsync(i => i.TotalCost)
        : 0m;
    invoice.VatAmount = Math.Round(invoice.SubTotal * invoice.VatRate / 100m, 2);
    invoice.TotalAmount = invoice.SubTotal + invoice.VatAmount;
});
```

Three consequences follow from the child owning the write:

- The amounts stay **off `TInputDto`** — same rule as any server-owned field; a client must not be able to
  state a total.
- The value is **eventually consistent**: it settles when the parent is next saved, not when a child moves.
  Attaching an intervention to an invoice therefore has to save the invoice too, or the total lags.
- **Seeding needs a second pass over the parents.** At create time the total is necessarily `0`, so add a
  final wave that re-`Modify()`s every parent purely to re-run this hook — see §Bulk insert / update.
- ⚠️ **A query filter on the child also hides its rows from this recompute.** A dependent you query directly
  carries `HasQueryFilter(x => !x.Parent!.IsArchived)` (§Step 11) — and that filter applies here too, while
  the parent is *still archived at the moment the prepper runs*. A restore (`PATCH {"isArchived": false}`)
  therefore sums zero rows and writes the total to `0`, returns 200, and logs nothing. Add
  `IgnoreQueryFilters()` to the aggregate query, scoped by the parent FK so it can only ever see this
  aggregate's own children.

## Server-generated sequential codes

`Code = $"ORD-{Guid.NewGuid():N}"[..16]` is unique and unordered. A *sequential* code (`REQ-2026-00001`)
that also survives bulk seeding cannot count rows per item: queued rows are invisible to a query until
`SaveChanges()`, so a `COUNT(*)`-per-row generator is both N+1 and duplicate-prone — it hands the same
number to every row in the wave. Prime the counter once and increment in memory:

```csharp no-compile
public class RequestCodeGenerator(IServiceScopeFactory scopeFactory)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<int, int> _last = new();       // year -> last sequence issued

    public async Task<string> Next(int year)
    {
        var prefix = $"REQ-{year}-";
        await _lock.WaitAsync();
        try
        {
            if (!_last.TryGetValue(year, out var seq))
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                // The suffix is zero-padded to a fixed width, so the lexical max IS the numeric max —
                // one indexed row, and no client-side scan to prime from.
                var highest = await db.Requests.IgnoreQueryFilters()   // archived rows still hold their code
                    .Where(x => x.Code!.StartsWith(prefix))
                    .OrderByDescending(x => x.Code)
                    .Select(x => x.Code!)
                    .FirstOrDefaultAsync();
                seq = highest is null ? 0 : int.Parse(highest[prefix.Length..]);
            }
            _last[year] = ++seq;
            return $"{prefix}{seq:D5}";
        }
        finally { _lock.Release(); }
    }
}
// services.AddSingleton<RequestCodeGenerator>();  then inject it into the primer that stamps Code on create
```

⚠️ **Prime from the highest code, never from a row count.** `CountAsync()` reads as the same thing and is
wrong the moment a row is hard-deleted: with `00001`–`00005` on file and `00003` deleted, the count is 4 and
the next mint is `00005` — a duplicate that dies on the unique index. `IgnoreQueryFilters()` covers *archived*
rows; nothing covers deleted ones, so the sequence has to be derived from what the codes actually say.

Two constraints come with the shape:

- **Register it as a singleton** — one counter per process. The in-memory sequence is therefore only correct
  while a single process mints codes; behind two instances or a scale-out, the counters prime independently
  and then collide. A multi-writer deployment needs a database sequence (`HasSequence`) or an insert-time
  retry, not this.
- **The unique index is still the guarantee.** The counter only keeps a wave from colliding with itself;
  keep the index and let a genuine collision surface as a 409.

Stamp from a primer exactly as above: mint on `Added`, restore from `entry.OriginalValues` on `Modified`.

⚠️ **Primer vs prepper when a second writer exists.** A prepper runs only on the entity-service write path
(`IEntityService.Add`/`Modify`/`Save` — so `original` is `null` on create, the stored row on update). A
**primer is an EF `SaveChangesInterceptor`** — it *also* runs when a domain/workflow service saves through the
raw `DbContext`. So a primer that restores a field from `entry.OriginalValues` **reverts that service's own
legitimate write**. If a field is server-owned *and* transitioned by such a service (a status/state machine),
guard it with a **prepper**; reserve the primer for fields nothing outside the entity pipeline writes
(`Created`, `Code`).

## Public (anonymous) attachment downloads

`<img src>` / `<a href>` requests carry no `Authorization` header, so on a secured API
(`MapControllers().RequireAuthorization()` or `[Authorize]`) every image renders as a 401. The attachment
controller's download actions are `virtual` — re-expose **both** download overloads anonymously; uploads,
deletes, and the attachment CRUD stay guarded:

```csharp no-compile
[Authorize]
[Route("articles")] // the owner base path — the base declares no class route (§Attachments step 3)
public class ArticleAttachmentController : EntityAttachmentControllerBase<ArticleAttachment>
{
    // UseAttachmentUris() DTO Uris target THIS overload whenever the attachment has a FileName
    // (the normal case for uploads): GET articles/{objectId}/files/{fileName}
    [AllowAnonymous]
    public override Task<IActionResult> GetFile(int objectId, string fileName, bool inline = true)
        => base.GetFile(objectId, fileName, inline);

    // Fallback DTO Uri (no FileName) and direct id-based links: GET articles/files/{id}
    [AllowAnonymous]
    public override Task<IActionResult> GetFile(int id, bool inline = true) => base.GetFile(id, inline);
}
```

Overriding only the id overload is the trap: the generated `Uri` points at the filename route, which stays
guarded — the `<img>` still 401s. (Authorization is evaluated on the *routed* action only; the filename
action's internal call into the id action is a plain method call.) Reserve this for genuinely public assets
(product/article pictures) — the routes are guessable; sensitive documents stay on the authenticated path
(download them through the shared axios, which sends the bearer).

## Audit Trail with Custom Primer

Use a global `EntityPrimerBase<TInterface>` to stamp `CreatedBy`/`ModifiedBy` on every entity that implements a shared auditing interface. The primer runs inside EF Core's `SaveChanges` interceptor and resolves the current user via `IHttpContextAccessor`.

- Define an audit interface (e.g. `IAuditable`) with `CreatedBy` and `ModifiedBy` properties
- Implement `EntityPrimerBase<IAuditable>` — check `EntityState.Added` vs `Modified`
- Register globally via `options.AddPrimer<UserTrackingPrimer>()`
- Runs via the primer interceptor — auto-wired by `UseDefaults()`; without it, select `e.WireDbContext(DbContextWiring.PrimerInterceptors)`

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Additional Patterns > Primers

## Hierarchical Data (Self-referencing)

Add Parent, and Children navigation properties. Filter on `ParentId` or `ChildId` in the query builder; use `x.ParentId == null` to return only root items. This is the **single-parent tree** shape (one `ParentId` per node).

> **Multi-parent?** A node under several parents is a many-to-many self-reference, not a tree: use a self-referencing join entity (`ParentEntities`/`ChildEntities` collections) instead of `ParentId`. See the `RelatedCategory` recipe in [`entities.examples.md`](./entities.examples.md) — Category entity, with full wiring in [`entities.advanced.example.md`](./entities.advanced.example.md).

> **`QuerySplittingBehavior` warning.** Eager-loading two or more *collection* navigations in one query (e.g. `ParentEntities` + `ChildEntities`) trips EF Core's Cartesian-explosion warning. Harmless for small data; otherwise add `.AsSplitQuery()` inside `Includes(...)`, or set `UseQuerySplittingBehavior(SplitQuery)` on the provider.

> **Beyond direct relations** — *"everything under X, any depth"* filters (`AncestorId`/`OffspringId` on the SearchObject), tree endpoints, and in-memory tree assembly with `Regira.TreeList`: see [`entities.blueprints.md`](./entities.blueprints.md) — Recursive entities (mapped recursive-CTE table-valued functions composed inside query filters).

> **→ See:** [`entities.examples.md`](./entities.examples.md) — Category entity

## Auto truncate

Use `AddAutoTruncateInterceptors()` when registering DbContext to prevent string truncation exceptions

## DbContext Interceptors — Quick Reference

**`UseEntities<TContext>(e => e.UseDefaults())` wires all of these (plus the UTC date convention) into the
context's options automatically** — `AddDbContext` only needs the provider. Without `UseDefaults()`, take the
full set with `e.AddDefaultInterceptors()` or select pieces à la carte with `e.WireDbContext(DbContextWiring …)` flags.

| `DbContextWiring` flag | What it wires |
|---|---|
| `PrimerInterceptors` | Runs Primers during EF Core `SaveChanges` (timestamps, soft-delete, audit, custom primers) |
| `NormalizerInterceptors` | Runs entity normalizers during `SaveChanges` to populate `NormalizedContent` and other `[Normalized]` fields |
| `AutoTruncateInterceptors` | Silently truncates `string` values to their `[MaxLength]` before `SaveChanges` to prevent DB exceptions |
| `UtcDateTimeConvention` | Rounds all `DateTime` properties through the database as UTC |
| `ArchivedQueryFilter` | Applies the soft-delete filter (`e => !e.IsArchived`) to every `IArchivable` entity type — see §Soft Delete |

> **À-la-carte pattern (no `UseDefaults()`):**
> ```csharp
> builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
> builder.Services.UseEntities<AppDbContext>(e =>
> {
>     e.AddDefaultPrimers();
>     e.WireDbContext(DbContextWiring.PrimerInterceptors | DbContextWiring.UtcDateTimeConvention);
> });
> ```
>
> `AddAutoTruncateInterceptors()` and `AddUtcDateTimeConvention()` also exist as plain
> `DbContextOptionsBuilder` extensions (`Regira.DAL.EFcore`) for EF usage without the entities stack, as does
> `AddArchivedQueryFilter()` (`Regira.Entities.EFcore.Extensions`) — the one to reach for on a `DbContext`
> you construct yourself, which no service-collection wiring can reach.
