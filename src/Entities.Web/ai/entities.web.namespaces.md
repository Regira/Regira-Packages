# Regira.Entities.Web — Namespace Reference

> **Full entity documentation lives in `Regira.Entities`.**
> Call `get_package_toc id=Regira.Entities` to access all guides (setup, instructions, examples, signatures, namespaces).
> This file covers only the types specific to the `Regira.Entities.Web` package.

---

## Controllers

| Namespace | Types |
|---|---|
| `Regira.Entities.Web.Controllers.Abstractions` | `EntityControllerBase<>` (all overloads) |

> ⚠️ **The `.Abstractions` suffix is required.** `using Regira.Entities.Web.Controllers;` is NOT enough — the class lives one level deeper in `...Controllers.Abstractions`.

### `EntityControllerBase<>` overloads

```csharp no-compile
// int key, no custom SearchObject/SortBy/Includes, no DTOs — simple
EntityControllerBase<TEntity>
// int key, no custom SearchObject/SortBy/Includes — simple
EntityControllerBase<TEntity, TDto, TInputDto>
// custom key, no custom SearchObject/SortBy/Includes — simple
EntityControllerBase<TEntity, TKey, SearchObject<TKey>, TDto, TInputDto>
// int key, custom SearchObject only (no TSortBy/TIncludes) — simple
EntityControllerBase<TEntity, TSearchObject, TDto, TInputDto>
// custom key, custom SearchObject only — simple
EntityControllerBase<TEntity, TKey, TSearchObject, TDto, TInputDto>
// int key, custom SearchObject/SortBy/Includes (most common) — complex
EntityControllerBase<TEntity, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>
// custom key, custom SearchObject/SortBy/Includes (full) — complex
EntityControllerBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto, TInputDto>
```

See the `For<>` → controller pairing table in `entities.instructions.md` for which overload matches each `.For<>()` registration.

Simple and complex controller bases expose different endpoint sets:

| Method | Route | Action | Availability |
|---|---|---|---|
| `GET` | `/{id}` | Details | All |
| `GET` | `/` | List | All |
| `GET` | `/search` | Search (with count) | All |
| `POST` | `/list` | List (body, batch) | **Complex only** |
| `POST` | `/search` | Search (body, batch) | **Complex only** |
| `POST` | `/save` | Save (upsert) | All |
| `POST` | `/` | Create | All |
| `PUT` | `/{id}` | Modify (full update) | All |
| `PATCH` | `/{id}` | Patch (partial update — JSON Merge Patch, RFC 7386) | All |
| `DELETE` | `/{id}` | Delete | All |

*Complex* = bases with `TSortBy` + `TIncludes`. The single-object `GET /search` (with `count`) and basic `GET /?q=…` text search are on **every** base, so a simple entity can page. Complexity adds only the batch body overloads (`POST /list`, `POST /search`) and typed `?includes=`/`?sortBy=`.

> **PATCH notes:** Sends only the fields to change. Omitted fields are preserved; `null` clears a field. The merge base is the current entity projected through `TInputDto`, so audit/computed fields outside the input model cannot be patched. Related collections absent from the patch body are left untouched.

---

## Attachments

| Namespace | Types |
|---|---|
| `Regira.Entities.Web.Attachments.Abstractions` | `EntityAttachmentControllerBase<>`, `AttachmentControllerBase<>` |
| `Regira.Entities.Web.Attachments.Services` | `AttachmentUriResolver<>` (ASP.NET Core `IAttachmentUriResolver<>` impl) |
| `Regira.Entities.Web.Attachments.DependencyInjection` | `AspNetAttachmentUriResolverRegistrar`, `UseAttachmentUris()` *(on `EntityServiceCollectionOptions`)* |

---

## Dependency Injection

| Namespace | Types |
|---|---|
| `Regira.Entities.Web.Attachments.DependencyInjection` | `UseAttachmentUris()` — opt in to ASP.NET Core attachment `Uri` resolution |
| `Regira.Entities.Web.DependencyInjection` | `ConfigureDefaultJsonOptions()` — on `IServiceCollection` *and* `EntityServiceCollectionOptions` |

> **Web-specific registrations live in `Entities.Web`.** In a web host, call `o.UseAttachmentUris()` inside the
> `UseEntities` options block (before entities are registered) and register `AddHttpContextAccessor()` so attachment
> DTOs get a resolved `Uri`. Without it the `Uri` is `null` (via `NullAttachmentUriResolver<>`), not an error.

---

## Response Models

| Namespace | Types |
|---|---|
| `Regira.Entities.Web.Models` | `ListResult<>`, `DetailsResult<>`, `CountResult`, `SaveResult<>`, `DeleteResult`, `SearchResult<>` |
| `Regira.Entities.Web.Models.Abstractions` | `IEntityResult<>` |
