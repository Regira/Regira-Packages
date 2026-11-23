# Guid keys: child rows, client-made keys and v7

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `4b55647`, and the `Regira-Modules` repository, `main` at `4a26ae6` (*Release 6.6.3*). The results under *Measured* come from two scratch consoles run on 2026-11-19 and 2026-11-23 against Packages `src/` at that commit, on `net10.0` with EF Core 10.0.15: the service cases on in-memory SQLite with the standard `UseEntities<…>(o => o.UseDefaults())` wiring, the HTTP cases through `TestServer` with a controller and with the minimal-API endpoints, and the key generators of the SqlServer provider 10.0.15 and Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3 without a database. How SQL Server, PostgreSQL and MySQL order a key is taken from their documentation as remembered. Everything else is read from the code. Nothing on this page has been built.

**Status: proposal, not scheduled. Four decisions are open (see *Decisions*).**

## Recommendation

Let a Guid-keyed child row carry its real key from the moment it is added. The Vue collection editors mint a version 7 Guid for it where they mint a negative temp id for an int-keyed row today, and the parent's save sends it like any other field. The back end already stores such a key as sent, at every depth, and refuses one that belongs to another parent's row (*Measured*). An int-keyed child keeps its negative temp id, and nothing changes for it.

The gap this closes: a Guid-keyed child works on the server but not in Regira's own editors. `createOwnedCollection` and its siblings accept only `{ id: number }` rows, and the `-1` they mint is a 400 in a Guid field (*Measured*). The front-end guide routes apps around it today: "Owned child rows stay int-keyed".

Around that change:

- **A new top-level row keeps getting its key from the server.** Client-made top-level keys are left out (see *Considered and left out*).
- **Server-made v7 keys get a guide section, not package code** (decision 1). PostgreSQL already makes v7 keys, SQL Server should keep EF's own sequential Guids, and a SQLite app adds a five-line generator.
- **One fix on the way:** for a body that names a row that does not exist, the controller's `Create` answers 204 with an empty body, where the minimal API answers 404.
- **The attachment web layer stays int-keyed**, and the guides say so (decision 4).

The steps, in order:

1. Back-end tests that pin today's behaviour for Guid-keyed children.
2. The `Create` fix.
3. The front end: `newGuidV7()`, string keys in the collection editors, and a way to tell an added row.
4. Front-end contract tests.
5. Guides, docs, the owned-slice template and the changelogs in both repositories.

## What exists today

### Back end (Regira-Packages)

| What | Where | Effect |
|---|---|---|
| Every layer takes the key type | `IEntity<TKey>`, `For<TEntity, TKey>()`, `EntityControllerBase<TEntity, TKey, …>`, the minimal-API endpoints, `SearchObject<TKey>` | A Guid-keyed entity is registered and served like an int-keyed one |
| Keyed variants of the default filters | `EnsureGlobalKeyedFilters` in `src/Entities.DependencyInjection/ServiceCollections/EntityServiceCollection.cs:362` | The id, archived, timestamp and `?q=` filters work for a Guid key |
| A child collection of any key type, at any depth | `Related<TRelated, TRelatedKey>` (`EntityServiceBuilder.cs:650`) and its nested twin on `RelatedEntityBuilder` | A Guid child is registered with both type arguments; CS0411 otherwise, as the guide says |
| How the sync classifies a child row | `UpdateRelatedCollection` in `src/Entities.EFcore/Extensions/DbContextExtensions.cs:49` | New: a default key, or a key that matches no stored row. Deleted: a stored row the incoming list leaves out. Updated: the rest |
| Temp keys | `IsTempKey` (`:137`), cleared by `ResetTempKey` (`:94`) and `ResetTempKeysBelow` (`:107`) | Only a negative signed number is a placeholder. A Guid is never cleared: a key the client chose is kept |
| "New" for a top-level row | `IsNew()` (`src/Common.Entities/Extensions/EntityExtensions.cs:7`); `EntityHandlerServices.Save` (`src/Entities.Mediator/Handlers/EntityHandlerServices.cs:54`) | A row is new only when its key is the default. A body with any other key is an update, and one whose row does not exist returns `null` |
| What the web layers make of that `null` | Minimal API: `Ok` (`src/Entities.Web/Endpoints/EntityEndpointMapper.cs:445`) answers 404. Controllers: `Modify` and `Save` add `?? NotFound()`; `Create` (`EntityControllerBase.cs:78` and `:165`) uses `!` | The two web layers answer differently (*Measured*) |
| The default sort | `SortQuery` (`src/Entities.EFcore/Extensions/QueryExtensions.cs:277`): normalized title, else title, else `Id` (`:289`); `EntitySortBy.Id` and `IdDesc` | An entity without a title lists in key order. With version 4 keys that order is random |
| Attachments | `HasAttachments` has a key-generic overload (`src/Entities.DependencyInjection/Attachments/EntityServiceBuilderExtensions.cs:39`). `EntityAttachmentControllerBase` requires `IEntityAttachment<int, int, int, Attachment>`, and the minimal-API attachment routes bind `int objectId` and `int id` (`EntityEndpointMapper.cs:387`, `:425`, `:433`) | A Guid-keyed owner gets attachments in its services but not over the stock routes |
| The guide's sync table | `src/Common.Entities/ai/entities.instructions.md:578` | Describes negative temp ids and says a Guid key "is never cleared". No row covers a Guid child |
| Tests | `RelatedIntShortcutTests` (a Guid parent with int children), `NonIntKeyedFiltersTests`, `GlobalFilterBehaviorTests`, `SoftDeleteLeakRegressionTests` | No test has a Guid-keyed child |

### Front end (Regira-Modules)

| What | Where | Effect |
|---|---|---|
| "Unsaved" | `isNewEntity` (`src/core/entities/abstractions/IEntity.ts:13`): `null`, `undefined`, `"new"`, `""`, or a number ≤ 0 | Any non-empty string reads as saved |
| The top-level insert | `insert()` (`src/core/entities/abstractions/EntityServiceBase.ts:148`) drops an unsaved `id` from the payload | A Guid model with `id: string = ""` gets its key from the server. Nested rows are sent as they are |
| The collection editors | `createCollection`, `createOwnedCollection`, `createListInput` and `createListItemInput` (`src/core/entities/form/collections.ts:25`–`116`); the Vue wrappers `useOwnedCollection`, `useListInput`, `useListItemInput` and `useOwnedModal` | All require `{ id: number }` |
| Temp ids | `append()` (`collections.ts:35`) gives a row `min(ids, 0) - 1`; the add-row gets `id = 0`; the add-row is recognised by `!id` (`:125`) or `id == 0` (`src/vue/entities/form/ownedModal.ts:36`) | `-1`, `-2`, … until the parent's save |
| One editor is key-agnostic already | `InputSelectorInline.vue:58` tracks a row added this session by identity (a `WeakSet` of `toRaw` rows), because "a string id may be client-minted" | The precedent for the design below. Its `exclude` list is typed `Array<number>` (`:39`, `inputSelectorInline.ts:30`) |
| Guid generation | `newGuid()` (`src/utilities/string-utility.ts:78`): `crypto.randomUUID()` | Version 4 only |
| `patch()` | `EntityServiceBase.ts:124` refuses an unsaved id on the client | A row added this session cannot be patched, as the guide says (`entities.patterns.md:928`) |
| The guide and the scaffold | `src/vue/entities/ai/entities.instructions.md:692`; `_template/owned-slice/Entity.ts:6` (`id: number = 0`) | "Owned child rows stay int-keyed", even under a Guid-keyed parent |
| Tests | `__tests__/contract/collections.test.js` | Pins `-1` for the first appended row (`:87`) |

## Measured

Service level: `For<Order, Guid>`, `Related<Line, Guid>`, and under it `Related<Note, Guid>`.

| Case | Result |
|---|---|
| A new order with lines and notes, every key `Guid.Empty` | Saved. EF made every key (version 4 on SQLite) |
| An existing order: one line left out; a line with `Guid.Empty` added; a line with a client v7 key added, holding a note with a v7 key; under the kept line, one note with `Guid.Empty` and one with a v7 key | Saved. The left-out line was deleted, and every v7 key was stored as sent, at both depths |
| The stored graph saved again unchanged | Saved without error |
| An existing order given a "new" line whose key is another order's line | `EntityConstraintException` (409). The other order's line is unchanged |
| `Save()` of a new order carrying a client v7 key | No error and no row: read as the update of a row that does not exist |
| `Add()` of the same order | Inserted with that key |

HTTP, the same model:

| Request | Controller | Minimal API |
|---|---|---|
| `POST`, no keys | 200; the server made every key | Same |
| `PUT` adding a line with a client v7 key | 200; the line has that key | Same |
| `PUT` adding a line with `"id": -1` | 400: the JSON value cannot be converted to `Guid` | 400 |
| `PUT` adding a line with `"id": ""` | 400 | 400 |
| `PUT` adding a line without `id` | 200; the server made the key | Same |
| `POST` with a client v7 key on the order | **204, empty body** | 404 |

EF Core 10.0.15 key generation for a `Guid` key, `ValueGeneratedOnAdd` on all three:

| Provider | Keys made |
|---|---|
| SQLite | `Guid.NewGuid()`: version 4 |
| SQL Server | EF's sequential Guid: version-4-shaped, ordered for SQL Server's comparison (the last group fixed, the fourth group counting up) |
| PostgreSQL (Npgsql 10.0.3) | Version 7 |

## Design

### A Guid-keyed child gets its real key when it is added

The collection controllers take a key minter:

```ts
type CollectionInput<T extends { id: number | string }> = {
    // …as today…
} & (T["id"] extends number
    ? { newId?: never } // an int row: a negative temp id, as today
    : { newId: () => string }) // a Guid row: its final key, e.g. newGuidV7
```

- `append()` gives the row `newId()` when there is one, and the next negative number otherwise.
- The add-row's sentinel follows the key: `""` with `newId`, `0` without. `createEmptyItem` sets `""` instead of `0`, and `createListInput` starts from `{ id: "" }`.
- The type makes `newId` required for a row whose `id` is a string, so a Guid row without one fails to compile instead of failing with a 400 at the save.
- The Vue wrappers pass `newId` through.
- `useOwnedModal`'s `id == 0` already holds for `""`, because JavaScript coerces it, but it reads as an int check. It becomes `!id`, as in `createListItemInput`. Not `isNewEntity`: that is true for `-1` as well, and an unsaved row reopened in the modal would be appended a second time.
- `InputSelectorInline`'s `exclude` widens to `Array<number | string>`.

Why a key from the client rather than none (sending no `id` works too, *Measured*):

- **The row keeps its key through the save.** The `:key` a list renders by is the final key, and nothing swaps a temp id for the saved one.
- **A retried save does not duplicate rows.** When a save's response is lost after the rows were inserted, a retry with negative temp ids inserts them again. With their own keys, the retry finds them and updates them. This holds for the children of a saved parent; a new parent is made again by either retry.
- **Nothing on the save path strips nested ids.** `insert()` strips only the top-level `id`, and `""` in a child's Guid field is a 400 (*Measured*).

### `newGuidV7()`

A new export beside `newGuid()` in `src/utilities/string-utility.ts`:

```ts
/** A version 7 UUID (RFC 9562): 48 bits of Unix time in ms, then random bits, so keys sort by creation time */
export function newGuidV7(): string {
    const b = crypto.getRandomValues(new Uint8Array(16))
    let ms = Date.now()
    for (let i = 5; i >= 0; i--) {
        b[i] = ms % 256
        ms = Math.floor(ms / 256)
    }
    b[6] = (b[6]! & 0x0f) | 0x70 // version 7
    b[8] = (b[8]! & 0x3f) | 0x80 // RFC variant
    const h = Array.from(b, (x) => x.toString(16).padStart(2, "0")).join("")
    return `${h.slice(0, 8)}-${h.slice(8, 12)}-${h.slice(12, 16)}-${h.slice(16, 20)}-${h.slice(20)}`
}
```

`crypto.getRandomValues` works outside a secure context, where `crypto.randomUUID` does not, so the function needs no fallback branch. A Guid-keyed app that does not want v7 passes `newId: newGuid`; the server does not care which version it gets.

### Telling an added row

A child that has its real key reads as saved to `isNewEntity`. Two things need to know that a row was added this session:

- an app that toggles a row through `patch()`: a v7 row not yet saved reaches the server and answers 404, where a negative id is refused on the client;
- an editor that removes an added row outright instead of marking it `_deleted`.

The controllers record each appended row by identity and expose `isAdded(row)`, as `InputSelectorInline` already does. The Vue wrapper passes `toRaw` as the identity, because a deep-reactive parent hands back proxies. When the parent's save replaces the model with the saved rows, those are new objects, so `isAdded` turns false by itself (decision 3).

### Server-made v7 keys

A Regira app gets v7 keys from the server without any Regira code, because EF Core takes a value generator per property. The guide section says, per provider:

- **PostgreSQL:** nothing to do. Npgsql makes v7 keys (measured on 10.0.3; the `net8.0` build's EF Core 9 pairs with Npgsql 9.x, which was not measured here).
- **SQL Server:** keep EF's default. SQL Server compares a `uniqueidentifier` from its last six bytes, so v7 keys, whose time is in the first six, land on random index pages just as version 4 keys do. EF's sequential Guid is built for that order.
- **SQLite, and MySQL when the key is stored as text (`char(36)`):** set a generator. A key stored as text sorts by its leading characters, so v7 keys sort by creation time.

<!-- no-compile -->
```csharp
public sealed class GuidV7Generator : ValueGenerator<Guid>
{
    public override bool GeneratesTemporaryValues => false;
    public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
}

// OnModelCreating, once per Guid key
modelBuilder.Entity<Order>().Property(x => x.Id).HasValueGenerator<GuidV7Generator>();
```

`Guid.CreateVersion7()` needs .NET 9 or later. The Regira-specific reason to mention any of this is the default sort: an entity without a title lists in key order, and v7 makes that creation order.

The client-made keys of the first section are v7 whatever the server makes. A SQLite table that mixes both is correct, only not uniformly ordered.

### The controller's `Create`

`Create` in both controller bases gets `?? NotFound()`, as `Modify` and `Save` already have, so a body that names a row that does not exist answers 404 on both web layers. The Packages changelog gets a Fix bullet.

## Considered and left out

- **Client-made keys for a top-level row.** "New" would stop meaning "the key is the default" on both sides. `POST` would have to insert any key it is given, `isNewEntity` would have to tell an unsaved Guid from a saved one, and a form whose first save lost its response could not tell whether to `POST` or `PUT`. The server already makes the key on the first save, and the form opens the row by the key the save returns. Revisit if offline creation becomes a goal. Server-side code that wants its own key, such as a seed or an import, already has `IEntityService.Add` (*Measured*).
- **A server check that a client key is v7.** The version decides nothing on the server. What protects other rows is uniqueness: a key that belongs to another row is a 409 (*Measured*).
- **v7 keys from `UseDefaults()`.** It would change the keys of every existing Guid-keyed app on upgrade, and on SQL Server it would make them worse.
- **Inferring the key kind at runtime** from the add-row's `id`. `createListInput` has no model to look at, and `createOwnedCollection` has one only when the app passes `createRow`. The `newId` option is one line, and the type enforces it.

## Decisions

1. **Server-made v7: a guide section, or package code?**
   - Option 1: the guide section above, and nothing in the packages.
   - Option 2: a `GuidV7ValueGenerator` and an opt-in `AddGuidV7KeyConvention()` in `Entities.EFcore`, wired through `ConventionOptionsExtension` like `ConcurrencyTokenConvention`, and never part of `UseDefaults()`. The `net8.0` build needs its own v7 code under `#if !NET9_0_OR_GREATER`, since `Guid.CreateVersion7` is .NET 9 or later, and the convention needs tests on both targets.

   Recommendation: option 1. The generator is plain EF Core, and the only Regira-specific part of option 2 is the `net8.0` fallback, for a runtime whose support ended on 2026-11-10.
2. **How the editors learn the key kind: an explicit `newId`, or inferred?** Recommendation: explicit, with the type making it required for a string `id` (see *Considered and left out*).
3. **How an added row is told: an identity set, or a `_new` field on the row?** `prepareItem` strips `_` fields only at the top level, so a `_new` on a child would reach the server, which ignores it, and it would stay `true` until something cleared it. Recommendation: the identity set, as `InputSelectorInline` already does.
4. **Attachments on a Guid-keyed owner: document the limit, or widen the web layer?** Widening means making `EntityAttachmentControllerBase` and the minimal-API attachment routes key-generic, with tests per key type. Recommendation: one row in the attachments guide now (the stock attachment routes need an int-keyed owner; a Guid-keyed owner registers `HasAttachments` with its key types and serves the files through its own endpoints), and widen when an app needs it.

## Steps

1. **Back-end tests.** A fixture in `tests/Entities.Testing` with the service cases under *Measured*. In `tests/Entities.Web.Testing`, on both hosts: a child sent with a client v7 key keeps it; `"id": -1` in a Guid field is a 400; and, after step 2, a `POST` that names a missing row is a 404.
2. **`Create`.** `?? NotFound()` in both controller bases, and a Fix bullet in the Packages changelog.
3. **Front end.** `newGuidV7()` and its export (`string-utility.ts:450`, `src/utilities/README.md:12`); the collection controllers and their Vue wrappers as under *Design*; `useOwnedModal` on `!id`; `InputSelectorInline`'s `exclude`. The packaging snapshot (`__tests__/packaging/__snapshots__/exports.test.js.snap`) gains `newGuidV7`.
4. **Front-end tests**, in `__tests__/contract/collections.test.js` so that every driver runs them: a Guid row gets its key from `newId`; an int row still gets `-1`, then `-2`; `isAdded` is true for an appended row and false once the model is replaced; two appends in one tick keep both rows, with distinct keys. Plus a unit test for `newGuidV7()`: the format, the version and variant bits, and two keys made a millisecond apart sorting in order.
5. **Guides and docs.**
   - Packages `entities.instructions.md:578`: a row for a Guid child (no key or `Guid.Empty`: the server makes one; a key that matches no stored row: inserted with that key; another row's key: 409), the server-made v7 section (decision 1), and the attachments row (decision 4).
   - Modules `entities.instructions.md`: the temp-id paragraph (`:152`) names both key kinds, and the "Owned child rows stay int-keyed" warning (`:692`) becomes how to key a Guid child (`id: string = ""` and `newId: newGuidV7`).
   - Modules `entities.patterns.md:801`, `:894`, `:902` and `:928` (patching only saved rows: `isAdded`), `entities.signatures.md`, `entities.card.md:128`, `docs/abstractions.md:20` and `docs/services.md:80`.
   - `_template/owned-slice/Entity.ts:6`: the comment names the Guid form.
   - A changelog bullet in each repository.

## Risks

- **The int path must not move.** Int-keyed children are what every app has today. Step 4 keeps their tests as they are and adds the Guid ones beside them.
- **The client's clock sets the key's time.** Keys made in different browsers sort by those browsers' clocks, so key order is approximate. Owned rows that need an order already have `SortOrder` (`ISortable`), and the editors show rows in the order the collection holds them.
- **A v7 key shows when its row was made**, to the millisecond, to anyone who sees the key. A child row's key is seen by those who can read its parent.
- **The `toRaw` identity.** If a wrapper passes a proxy where the controller recorded the raw row, `isAdded` reads false. `InputSelectorInline` has the same dependency; step 4 pins it for the controllers.
