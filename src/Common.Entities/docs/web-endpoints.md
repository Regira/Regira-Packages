# Web Endpoints

Expose entity CRUD operations as HTTP endpoints using controllers:

| Package | Description |
|---------|-------------|
| `Regira.Entities.Web` | MVC attribute model via `EntityControllerBase` |

---

## Controllers

Controllers provide a more traditional, attribute-based approach using `EntityControllerBase`. Use this when you need full customisation, a per-entity pipeline with DTO mapping, or advanced sorting and includes.

### Controller Selection

```csharp
// basic (not recommended)
EntityControllerBase<TEntity>
EntityControllerBase<TEntity, TKey>
// basic (using DTOs, recommended)
EntityControllerBase<TEntity, TDto, TInputDto>
EntityControllerBase<TEntity, TSearchObject, TDto, TInputDto>
EntityControllerBase<TEntity, TKey, TSearchObject, TDto, TInputDto>
// complex (advanced operations)
EntityControllerBase<TEntity, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>
EntityControllerBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>
```

### Route prefix

Best practice: 
Keep controller `[Route]` attributes **resource-relative** — `[Route("[controller]")]`, or the resource name (e.g. `[Route("products")]`). Apply a shared `api` base **once**, in a single configurable place:

- **At the host:** an IIS virtual directory / reverse-proxy path, or `app.UsePathBase("/api")`.
- **In the app:** a global route-prefix convention (the prefix can come from configuration):

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

public sealed class RoutePrefixConvention(string prefix) : IApplicationModelConvention
{
    private readonly AttributeRouteModel _prefix = new(new RouteAttribute(prefix));
    public void Apply(ApplicationModel application)
    {
        foreach (var controller in application.Controllers)
            foreach (var selector in controller.Selectors)
                selector.AttributeRouteModel = selector.AttributeRouteModel is { } existing
                    ? AttributeRouteModel.CombineAttributeRouteModel(_prefix, existing)
                    : _prefix;
    }
}

// Program.cs — register once; every controller is served under /api/...
builder.Services.AddControllers(options =>
    options.Conventions.Add(new RoutePrefixConvention("api")));
```

### Standard Endpoints

Simple and complex controller bases expose different endpoint sets. **Simple** bases (no `TSortBy`/`TIncludes`) expose Details, List (GET), `GET /search`, Save/Create/Modify/Patch/Delete. **Complex** bases additionally expose `POST /list` and `POST /search`.

#### Fetch Endpoints

**Details (all bases):**
```csharp
// GET /{entities}/{id} - Single entity
Details(id) -> DetailsResult
```

**List (all bases):**
```csharp
// GET /{entities} - Basic List
List() -> ListResult

// GET /{entities}?q={search}&page=1&pageSize=10 - List
List(searchObject, pagingInfo) -> ListResult

// Complex bases only — typed ?includes= and ?sortBy= bind on complex bases; simple bases ignore them
// GET /{entities}?categoryId=1&includes=Category&sortBy=CreatedDesc&sortBy=Title
List(searchObject, pagingInfo, includes[], sortBy[]) -> ListResult
```

**Search (all bases):**
```csharp
// GET /{entities}/search?q={keyword}&page=1 - List + Count combined
// SearchResult carries a total Count alongside the items — use it to drive paging.
Search(searchObject, pagingInfo) -> SearchResult
```

**Complex POST endpoints — complex bases only:**
```csharp
// POST /{entities}/list (collection of SearchObjects in body)
List([FromBody] searchObject[], pagingInfo, includes[], sortBy[]) -> ListResult

// POST /{entities}/search (collection of SearchObjects in body)
Search([FromBody] searchObject[], pagingInfo, includes[], sortBy[]) -> SearchResult
```

*The SearchObject items return queries that are inclusive (using Union).*

#### Paging

List and Search endpoints accept optional `page` and `pageSize` query parameters. By default, when no `pageSize` is sent, the **full set** is returned. You can configure a default and/or maximum page size so endpoints page automatically:

```csharp
// Global — applies to every entity controller
services.UseEntities<AppDbContext>(options =>
{
    options.UseDefaults();
    // make sure to put this after UseDefaults()
    options.DefaultPageSize = 50;   // used when the request omits pageSize
    options.MaxPageSize = 200;      // any larger requested pageSize is clamped to this
    // or
    options.SetPageSize(pageSize: 50, maxPageSize: 200);
});

// Per-entity override — fully replaces the global values for that entity
services.For<Product>(e => e.SetPageSize(defaultPageSize: 25, maxPageSize: 100));

// Opt out — this entity is never force-paged, even when a global default is set
services.For<AuditLog>(e => e.SetPageSize());
```

- Both values are optional; `null` means that aspect is off.
- The default only fills in when the request has no positive `pageSize`; an explicit larger `pageSize` is honoured unless `MaxPageSize` clamps it; `page` is preserved.
- **Enforced at the HTTP boundary only** — on every HTTP surface (MVC controllers and FastEndpoints alike), so `MaxPageSize` cannot be escaped by picking a different surface. Calling `IEntityService.List(...)` directly (without `PagingInfo`) still returns the full set — the service layer keeps full control.

#### Save (Add/Modify/Patch)

```csharp
// POST /{entities} - Create
Create(inputDto) -> SaveResult

// PUT /{entities}/{id} - Full update
Modify(id, inputDto) -> SaveResult

// PATCH /{entities}/{id} - Partial update (JSON Merge Patch, RFC 7386)
Patch(id, partialJson) -> SaveResult

// POST /{entities}/save - Upsert
Save(inputDto) -> SaveResult
```

> **PATCH behaviour:**
> - Accepts a JSON object containing only the fields to change; omitted fields are left untouched.
> - Setting a field to `null` clears it (RFC 7386 semantics).
> - The merge base is the current entity serialized to JSON and then deserialized as `TInputDto`, so only properties declared on the input model can be modified — audit/computed fields on `TEntity` are automatically excluded.
> - Related collections not included in the patch body are left intact (the entity is fetched without includes, so `null` collections are treated as absent, not as "remove all").
> - Assumes `TInputDto` property names match the corresponding `TEntity` property names.

#### DELETE Endpoint

```csharp
// DELETE /{entities}/{id} - Delete
Delete(id) -> DeleteResult
```

### Notes

- ⚠️ **Generated endpoints ship anonymous.** No controller base carries `[Authorize]`, so every scaffolded
  endpoint — including delete and attachment download — is public until the application adds authorization.
  Apply it globally when mapping (`MapControllers().RequireAuthorization()`), or put `[Authorize]` on each
  controller subclass and `[AllowAnonymous]` on the individual actions that must stay public. For row-level
  scoping (tenant or owner), register a global filter query builder rather than relying on endpoint attributes.
- A controller reads/writes entities using an `IEntityService`
- The controller's generic types must match the service's generic types (DTOs excluded)
- It's **not necessary to inject** the service in the constructor — the base controller resolves it via `HttpContext.RequestServices`
- Responsible for mapping to/from DTO models using `IEntityMapper`
- **Error status codes:** `EntityInputException` → **400** with the field errors as `ModelState`; a database
  constraint violation (`EntityConstraintException`) → **409 Conflict** with a generic `ProblemDetails`
  detail (the provider message is logged server-side); a missing entity → **404**. See
  [Built-in Features → Constraint Exceptions](built-in-features.md#constraint-exceptions)

---

## Response Types

Both approaches return the same standardised result wrappers:

```csharp
public record DetailsResult<TDto>
{
    public TDto Item { get; set; }
    public long? Duration { get; set; } // Execution time in ms
}

public record ListResult<TDto>
{
    public IList<TDto> Items { get; set; }
    public long? Duration { get; set; }
}

public record SearchResult<TDto>
{
    public IList<TDto> Items { get; set; }
    public long Count { get; set; } // Total count for pagination
    public long? Duration { get; set; }
}

public record SaveResult<TDto>
{
    public long? Duration { get; set; }
    public bool IsNew { get; set; }
    public int Affected { get; set; }
    public TDto Item { get; set; }
}

public record DeleteResult<TDto>
{
    public TDto Item { get; set; } // The deleted item
    public long? Duration { get; set; }
}
```

---

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. **[Web Endpoints](web-endpoints.md)** — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
