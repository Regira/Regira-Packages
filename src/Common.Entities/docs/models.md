# Entity Models

## Creating Entity Models

- Implement `IEntity<TKey>` (if entity has a serial int ID, use `IEntityWithSerial`))
- Have a primary key property named `Id`
- Implement relevant marker interfaces based on properties (see [Built-in Features: Entity Interfaces](built-in-features.md#entity-interfaces))
- Be a POCO (Plain Old CLR Object) - data only, minimal behavior
- Use data annotations (MaxLength, Required, ...) directly on entity properties.

```csharp
public interface IEntity;
public interface IEntity<TKey> : IEntity
{
    public TKey Id { get; set; }
}
```

## Referencing one of your own children

An owner with an optional foreign key to one of its own child rows — while the child's foreign key back is
required, and therefore cascades — makes the two tables reference each other. Prefer marking the child instead:
a flag or a rank column identifies the same row with no foreign key. Startup validation warns about the shape.

Where the reference has to stay, two things need handling:

- **The schema.** SQL Server rejects a migration with two cascade paths between the same two tables
  ("may cause cycles or multiple cascade paths"). Map the reference `DeleteBehavior.ClientSetNull` — `NO ACTION`
  in the database, EF nulls the reference on the tracked owner. SQLite does not enforce this.
- **The delete.** Both rows are deleted in one save, and EF Core cannot order them: it refuses with
  "a circular dependency was detected in the data to be saved". A primer cannot fix it — the delete order comes
  from the **original** foreign-key values — so the reference has to be dropped in an `UPDATE` of its own.

`SaveChangesBreakingDeleteCycles` / `SaveChangesBreakingDeleteCyclesAsync` (`Regira.Entities.EFcore.Extensions`)
do that. Call them from the context's own overrides — both of them, or synchronous callers stay broken:

```csharp
public override int SaveChanges(bool acceptAllChangesOnSuccess)
    => this.SaveChangesBreakingDeleteCycles(base.SaveChanges, acceptAllChangesOnSuccess);

public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken token = default)
    => this.SaveChangesBreakingDeleteCyclesAsync(base.SaveChangesAsync, acceptAllChangesOnSuccess, token);
```

Pass `acceptAllChangesOnSuccess` to the extension and `base.SaveChanges` itself as the delegate: the extension
decides what each phase may accept. It always accepts the reference-dropping `UPDATE`, because EF reads the
delete order back from those entries; your `false` is honoured on the final save, so the deletes stay pending
until you call `AcceptAllChanges()`. A lambda that closes over the flag instead re-raises the circular
dependency the extension exists to prevent.

They null the optional side, save, and delete in a second save, inside one transaction. A save without such a
pair is a single round trip and opens no transaction. Already inside a transaction of your own — or a
`TransactionScope` — the two saves join it rather than opening a second one, which is what lets the pattern
work under `EnableRetryOnFailure()` inside EF's own `CreateExecutionStrategy().Execute(...)` recipe. A bare
`BeginTransaction()` under a retrying strategy is refused by EF's own `SaveChanges`, extension or not.

## SearchObject

Use SearchObject for filtering entities.

- Created by the Controller using Model Binding from QueryString or JSON body
- Derive custom SearchObject from the `SearchObject<TKey>` class (or `SearchObject` when TKey is of type int)
- Prefer using `ICollection<TKey>` when filtering on key-properties for flexibility
- Timestamp filters (`MinCreated`, `MaxCreated`, `MinLastModified`, `MaxLastModified`) are interpreted as UTC; local kinds are converted, unspecified kinds are assumed UTC

```csharp
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

**Archived Property (soft delete)**:
- `ArchivedFilter` — `Excluded` (archived rows invisible), `Included` (live + archived), `Only` (archived only)
- Bound from `?archived=`; `null` falls back to `DefaultArchivedFilter` configured on `UseEntities()`
- Read by the built-in `IArchivable` filter only — other global filters (tenant/owner scoping) keep applying
- See: [*Built-in Features*](built-in-features.md) — Soft delete

**Q Property (General Text Search)**:
- The `Q` property serves as a general text search field
- Typically used when entity implements `IHasTitle` or `IHasDescription`
- Developers can add custom filtering logic in query filters
- Use `QKeywordHelper` for wildcard support (*) in search queries
- See: [*Normalizing Entities*](normalizing.md) for more info

## SortBy Enum

- The default SortBy enum can be a replaced by a custom one. 
- If none is configured, `EntitySortBy` is used.
- Sorting can be done using a collection of SortBy enum values. The enum values will be applied in the given order.
- Handled by `ISortedQueryBuilder` implementations
 
```csharp
public enum EntitySortBy
{
    Default,
    Id,
    IdDesc,
    Created,
    CreatedDesc,
    LastModified,
    LastModifiedDesc,
}
```

## Includes Enum

- Use a bitmask enum to enable multiple includes as one value.
- If none is configured, an very basic `EntityIncludes` is used.

```csharp
[Flags]
public enum MyEntityIncludes
{
    Default = 0,
    // Add custom options here
    Option1 = 1 << 0,
    Option2 = 1 << 1,
    All = Option1 | Option2
}
```

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. **[Entity Models](models.md)** — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
