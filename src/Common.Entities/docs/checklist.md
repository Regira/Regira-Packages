# Checklist

## Setup

- [ ] Install required packages
- [ ] **Configure license key (optional)** — without a key the free tier applies automatically (5 simple / 2 complex entity registrations). *Simple* = `For<>()` without `TSortBy`/`TIncludes` type parameters; *complex* = with them. For paid limits, add a `Regira:LicenseKeys` array to `appsettings.json` and call `services.UseRegira(configuration)` before `UseEntities()`. A single key can cover multiple products; add more keys to the array to combine them — the system picks the best per product. Obtain a key at [https://regira.com/licensing](https://regira.com/licensing).
- [ ] Create/configure **DbContext** (inherit from `DbContext`)
    - DbSets
    - Model configuration
- [ ] Configure interceptors on DbContext (if needed)
    - Primers
    - Normalizers
- [ ] Setup Entities using `.UseEntities()`
    - [ ] Configure **Mapping** library (AutoMapper/Mapster) when using DTOs
    - [ ] Register global filters, primers, preppers (optional)
    - [ ] Set a default/maximum **page size** (`options.DefaultPageSize` / `options.MaxPageSize`, or per-entity `e.SetPageSize(...)`) so List/Search endpoints don't return the full set (optional)
- [ ] Configure the FileService in `.WithAttachments()` when using attachments


## Add & configure a new Entity

When implementing a new entity in an application:

*Required*
- [ ] Create entity **Model(s)** 
    - Use appropriate interfaces
    - Use Data annotations (*Required*, *MaxLength*, ...)
    - Prefer using `SetDecimalPrecisionConvention` in DbContext over setting precision on each property
- [ ] Configure **DbContext**
    - Add DbSet collection
    - Configure relationships
    - Prefer Data Annotations over Fluent API when possible
    - Soft delete needs nothing here: the archived query filter is auto-wired by `UseEntities<TContext>(e => e.UseDefaults())`. A `DbContext` you construct yourself takes `.AddArchivedQueryFilter()` on its options builder — startup validation fails with an error naming the entity when a model ends up without the filter
    - UTC dates need nothing here: the UTC date convention is auto-wired by `UseEntities(e => e.UseDefaults())` — `DateTime` values round-trip as UTC (JSON `Z` suffix)
- [ ] Configure Entity in DI using `.For<TEntity>()`
- [ ] Add **Web Endpoints** *(when using API)*
    - Create an `EntityControllerBase` controller for each entity
    - Add custom actions only when necessary, otherwise rely on built-in CRUD actions
    - Prefer extending SearchObject to extend filtering over adding extra endpoints
- [ ] Initialize the database
    - Default SQLite starter/test setup: call `Database.EnsureCreated()` and keep the local database disposable
    - Migration-based setup or mature database provider: create/apply an EF migration

*Recommended (when using API)*

- [ ] Create **DTOs** (output DTO, input DTO)
- [ ] Configure **Mapping** (+ Aftermappers when needed)

*Optional*

- [ ] Create SearchObject (+SortBy & Includes enums)
- [ ] Implement query filters
- [ ] Add Processors
- [ ] Add Preppers
- [ ] Add Primers
- [ ] Configure child properties with Related method — an owned child (order lines, join rows) normally needs **no** own `.For<>()` registration or controller; it rides on the parent's endpoints. Adding one for a dedicated route is fine provided the parent's input DTO leaves that collection `null`
- [ ] If the child is **sortable**, `SortOrder` must travel on the parent DTO (position drives `SetSortOrder()`), so the collection can't be omitted — guard any per-row field with a `Prepare` hook, and keep the child's FK on its input DTO

*Extra*
- [ ] Add Attachments
    - Ensure the file store is set up: `.WithAttachments(factory)` (registers the shared `Attachment` entity + store + primer)
    - Define the `EntityAttachment` subclass (inherit `EntityAttachment`, set `ObjectType` in the constructor)
    - Implement `IHasAttachments` / `IHasAttachments<TAttachment>` on the owning entity
    - Add `DbSet<Attachment>` + `DbSet<TAttachment>` and configure relationships in DbContext
    - Register the typed link: `.For<Owner>(e => e.HasAttachments<TContext, Owner, TAttachment>(x => x.Attachments))`
    - Add a controller `: EntityAttachmentControllerBase<TAttachment>` with the owner base `[Route]` only
- [ ] Add Normalizers
    - Ensure normalizers are set up
    - Decorate entity properties with Normalized attribute
    - Or implement custom (global) normalizer for entity


## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. **[Checklist](checklist.md)** — Step-by-step guide for common tasks
1. [Practical Examples](examples.md) — Complete implementation examples
