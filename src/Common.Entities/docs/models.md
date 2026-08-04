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
