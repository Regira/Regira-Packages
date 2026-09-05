# Regira Entities — Namespace Reference

> **AI Agent Rule**: You MUST use the exact namespaces listed in this file.
> You are NOT allowed to guess, invent, or assume any namespace.
> If a type is not listed here, look it up in the codebase before using it.

---

## Entity Interfaces & Base Models

| Namespace | Types |
|---|---|
| `Regira.Entities.Models` | `SearchObject<>`, `EntitySortBy`, `EntityInputException<>`, `EntityIncludes`, `ArchivedFilter` *(`Excluded`/`Included`/`Only` — `ISearchObject.Archived`, bound from `?archived=`)* |
| `Regira.Entities.Models.Abstractions` | `IEntity<>`, `IEntityWithSerial`, `ISearchObject<>`, `IHasTimestamps`, `IHasCreated`, `IHasLastModified`, `IHasTitle`, `IHasNormalizedTitle`, `IHasDescription`, `IHasCode`, `IHasNormalizedContent`, `IHasLastNormalized`, `IArchivable`, `ISortable`, `IHasObjectId<>`, `IHasAggregateKey`, `IHasDefault<>`, `IHasParentEntity<>`, `IHasPassword`, `IHasEncryptedPassword`, `IHasSlug`, `IHasStartDate`, `IHasEndDate`, `IHasStartEndDate`, `IHasUri`, `IHasUserId` |

---

## Services

| Namespace | Types |
|---|---|
| `Regira.Entities.Services.Abstractions` | `IEntityService<>`, `IEntityReadService<>`, `IEntityWriteService<>`, `IEntityRepository<>`, `IEntityManager<>`, `EntityWrappingServiceBase<>` |
| `Regira.Entities.Services` | `EntityManager<>` |
| `Regira.Entities.EFcore.Services` | `EntityRepository<>`, `EntityReadService<>`, `EntityWriteService<>` |

---

## Query Builders

| Namespace | Types |
|---|---|
| `Regira.Entities.QueryBuilders.Abstractions` | `IQueryBuilder<>`, `IFilteredQueryBuilder<>`, `IGlobalFilteredQueryBuilder<>`, `ISortedQueryBuilder<>`, `IIncludableQueryBuilder<>`, `FilteredQueryBuilderBase<>`, `GlobalFilteredQueryBuilderBase<>` |
| `Regira.Entities.EFcore.QueryBuilders` | `QueryBuilder<>`, `EntityQueryFilter<>`, `SortedQueryBuilder<>`, `IncludableQueryBuilder<>` |
| `Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders` | `FilterIdsQueryBuilder`, `FilterArchivablesQueryBuilder`, `FilterHasCreatedQueryBuilder`, `FilterHasLastModifiedQueryBuilder`, `FilterHasNormalizedContentQueryBuilder` |
| `Regira.Entities.EFcore.Extensions` | `QueryExtensions` *(EF Core LINQ helpers: `FilterId`, `FilterIds`, `FilterExclude`, `FilterCode`, `FilterTitle`, `FilterNormalizedTitle`, `FilterCreated`, `FilterLastModified`, `FilterTimestamps`, `FilterQ`, `FilterArchivable`, `FilterHasAttachment`, `FilterIsActiveOn`, `SortQuery`, `OrderOrThenBy`, `OrderOrThenByDescending`)*, `ModelBuilderExtensions` *(`SetArchivedQueryFilter()` — the `OnModelCreating` route to the archived filter, optional since `UseDefaults()` wires it; `ArchivedQueryFilterName` = `"Regira:Archived"`)*, `DbContextOptionsBuilderExtensions` *(`AddArchivedQueryFilter()` — the same filter from an options builder, for a `DbContext` constructed outside DI)* |
| `Regira.DAL.Paging` | `QueryExtensions` *(`PageQuery`)* |

---

## Processors

| Namespace | Types |
|---|---|
| `Regira.Entities.Processing.Abstractions` | `IEntityProcessor<>` |
| `Regira.Entities.EFcore.Processing` | `EntityProcessor<>` |

---

## Preppers

| Namespace | Types |
|---|---|
| `Regira.Entities.Preppers.Abstractions` | `IEntityPrepper<>`, `EntityPrepperBase<>` |
| `Regira.Entities.Preppers` | `ServerOwnedPrepper<,>`, `AutoServerOwnedPrepper` |
| `Regira.Entities.EFcore.Preppers` | `EntityPrepper<>`, `RelatedCollectionPrepper<>` |
| `Regira.Entities.Attributes` | `ServerOwnedAttribute` (`[ServerOwned]`) |

---

## Primers (EF Core SaveChanges Interceptors)

| Namespace | Types |
|---|---|
| `Regira.Entities.EFcore.Primers` | `ArchivablePrimer`, `HasCreatedDbPrimer`, `HasLastModifiedDbPrimer`, `AutoTruncatePrimer`, `AutoNormalizingPrimer`, `EntityPrimerContainerInterceptor` |
| `Regira.Entities.EFcore.Primers.Abstractions` | `IEntityPrimer<>`, `EntityPrimerBase<>` |
| `Regira.Entities.DependencyInjection.Primers` | `ServiceCollectionPrimerExtensions` *(`AddPrimer<>()`, `AddDefaultPrimers()`, `AddAutoTruncatePrimer()`, `AddDefaultEntityNormalizerPrimer()` — on `IServiceCollection` and `EntityServiceCollectionOptions`)* |

---

## Normalizing

| Namespace | Types |
|---|---|
| `Regira.Normalizing` | `[NormalizedAttribute]`, `ObjectNormalizer`, `DefaultNormalizer`, `NormalizingDefaults` |
| `Regira.Normalizing.Abstractions` | `INormalizer`, `IObjectNormalizer` |
| `Regira.Normalizing.Models` | `NormalizingOptions` |
| `Regira.Entities.EFcore.Normalizing` | `DefaultEntityNormalizer`, `EntityNormalizerContainerInterceptor` |
| `Regira.Entities.Normalizing.Abstractions` | `IEntityNormalizer<>`, `EntityNormalizerBase<>`, `EntityNormalizingOptions` |
| `Regira.Entities.DependencyInjection.Normalizers` | `ServiceCollectionNormalizerExtensions` *(`AddNormalizer<>()`, `AddDefaultEntityNormalizer()`, `AddDefaultQKeywordHelper()` — on `IServiceCollection` and `EntityServiceCollectionOptions`)*, `EntityDefaultNormalizingOptions` |
| `Regira.Entities.Keywords` | `QKeywordHelper`, `QKeywordHelperOptions`, `QKeyword`, `ParsedKeywordCollection` |
| `Regira.Entities.Keywords.Abstractions` | `IQKeywordHelper` |

---

## Dependency Injection

| Namespace | Types |
|---|---|
| `Regira.Entities.DependencyInjection.Extensions` | `ServiceCollectionExtensions` *(`UseEntities<TContext>()` — on `IServiceCollection`; `GetServices<TContext>()` — on `IEntityServiceCollection<TContext>`, returns the underlying `IServiceCollection`)*, `EntityServiceCollectionExtensions` *(`UseDefaults()` — on `EntityServiceCollectionOptions`)* |
| `Regira.Licensing.DependencyInjection` | `ServiceCollectionExtensions` *(`UseRegira(configuration)` / `UseRegira(params string?[] licenseKeys)` — on `IServiceCollection`; comes transitively via `Regira.Entities.DependencyInjection`)* |
| `Regira.Entities.DependencyInjection.ServiceCollections` | `EntityServiceCollection<>` |
| `Regira.Entities.DependencyInjection.ServiceBuilders` | `EntityServiceBuilder<>`, `EntityIntServiceBuilder<>`, `EntitySearchObjectServiceBuilder<>`, `ComplexEntityServiceBuilder<>`, `ComplexEntityIntServiceBuilder<>` |
| `Regira.Entities.DependencyInjection.ServiceCollections.Models` | `EntityServiceCollectionOptions` |
| `Regira.Entities.DependencyInjection.ServiceCollections.Abstractions` | `IEntityServiceCollection<>` |
| `Regira.Entities.DependencyInjection.QueryBuilders` | `ServiceCollectionQueryFilterExtensions` *(`AddFilter<>()`, `AddGlobalFilterQueryBuilder<>()`, `RemoveGlobalQueryFilters()`, `AddDefaultGlobalQueryFilters()` — on `IServiceCollection` and `EntityServiceCollectionOptions`)* |
| `Regira.Entities.DependencyInjection.Preppers` | `ServiceCollectionPrepperExtensions` *(`AddPrepper<>()` — on `IServiceCollection` and `EntityServiceCollectionOptions`)* |
| `Regira.Entities.DependencyInjection.Processors` | `ServiceCollectionProcessorExtensions` *(`AddProcessor<>()` — on `IServiceCollection`; the per-entity `e.AddProcessor<>()` verb rides the `For<>()` builder)* |
| `Regira.Entities.DependencyInjection.Mapping` | `ServiceCollectionMappingExtensions` *(`AddMapping<>()`, `AddAfterMapper<>()`, `AfterMap<>()` — on `EntityServiceCollectionOptions`)*, `MappedEntityServiceBuilder<>` |
| `Regira.Entities.Web.DependencyInjection` | `EntityServiceCollectionJsonExtensions` *(`ConfigureDefaultJsonOptions()` — extension on `IServiceCollection`; applies cycles/nulls/enum-names to both the MVC and `Http.Json` options, and registers the entity-exception filter — see `entities.setup` → P3)*, `EntityServiceCollectionExceptionExtensions` *(`MapEntityExceptions()` — the filter on its own, for a host configuring JSON itself)* |

---

## Mapping

| Namespace | Types | Notes |
|---|---|---|
| `Regira.Entities.Mapping.Mapster` | `UseMapsterMapping()` | **Default** mapping provider |
| `Regira.Entities.Mapping.AutoMapper` | `UseAutoMapper()` | Alternative mapping provider |
| `Regira.Entities.Mapping.Abstractions` | `IEntityMapper`, `IEntityAfterMapper<>`, `IEntityMapConfigurator`, `EntityAfterMapperBase<>`, `EntityAfterMapper<>` |  |
| `Regira.Entities.Mapping.Models` | `AttachmentDto<>`, `AttachmentInputDto<>`, `EntityAttachmentDto<>`, `EntityAttachmentInputDto<>` | DTO classes for mapping |
| `Regira.Entities.Attachments.Mapping.Abstractions` | `IEntityAttachmentInput<>` | Attachment input contracts |

---

## EF Core Extensions & Interceptors

| Namespace | Types |
|---|---|
| `Regira.DAL.EFcore.Extensions` | `SetDecimalPrecisionConvention(int precision, int scale)`, `SetUtcDateTimeConvention()` *(ModelBuilder / ModelConfigurationBuilder extensions)*, `AddUtcDateTimeConvention()` *(DbContextOptionsBuilder extension — standalone EF; auto-wired by `UseDefaults()`)* |
| `Regira.DAL.EFcore.Conversions` | `UtcDateTimeConverter` *(DateTime round-trips as UTC while the policy is enabled)* |
| `Regira.Utilities` | `DateTimeDefaults` *(process-wide UTC date policy — honored by primers, filters and the converter)* |
| `Regira.DAL.EFcore.Services` | `AddAutoTruncateInterceptors()` *(DbContextOptionsBuilder extension)* |
| `Regira.DAL.Paging` | `PagingInfo` |

---

## Attachments

| Namespace | Types |
|---|---|
| `Regira.Entities.Attachments.Abstractions` | `IAttachment<>`, `IEntityAttachment<>`, `IHasAttachments<>`, `IAttachmentService<>`, `IAttachmentFileService<>`, `IAttachmentSearchObject<>`, `IEntityAttachmentSearchObject<>`, `IAttachmentUriResolver<>`, `IFileIdentifierGenerator` |
| `Regira.Entities.Attachments.Models` | `Attachment<>`, `EntityAttachment<>`, `AttachmentSearchObject<>`, `EntityAttachmentSearchObject<>` |
| `Regira.Entities.Attachments` | `EntityAttachmentUriAfterMapper<>`, `NullAttachmentUriResolver<>` |
| `Regira.Entities.EFcore.Attachments` | `ITypedAttachmentService`, `TypedAttachmentService<>`, `AttachmentFilteredQueryBuilder<>`, `EntityAttachmentFilteredQueryBuilder<>`, `AttachmentProcessor<>`, `EntityAttachmentProcessor<>`, `AttachmentPrimer`, `EntityAttachmentPrimer`, `DefaultFileIdentifierGenerator<>` |
| `Regira.Entities.DependencyInjection.Attachments` | `EntityAttachmentServiceBuilder<>`, `EntityServiceBuilderExtensions` *(`HasAttachments<>()` — on `EntityServiceBuilder<>`)*, `IEntityAttachmentServiceBuilder<>` |
| `Regira.Entities.DependencyInjection.Attachments.Abstractions` | `IAttachmentUriResolverRegistrar` |
| `Regira.Entities.Web.Attachments.Abstractions` | `EntityAttachmentControllerBase<>` |
| `Regira.Entities.Web.Attachments.DependencyInjection` | `AspNetAttachmentUriResolverRegistrar`, `UseAttachmentUris()` *(on `EntityServiceCollectionOptions`)* |
| `Regira.IO.Storage.Abstractions` | `IFileService` |
| `Regira.IO.Storage.FileSystem` | `BinaryFileService`, `FileSystemOptions` |
| `Regira.IO.Storage.Azure` | `BinaryBlobService`, `AzureOptions`, `AzureCommunicator` |
| `Regira.IO.Storage.SSH` | `SftpService`, `SftpCommunicator`, `SftpConfig` |

---

## Extensions

| Namespace | Types |
|---|---|
| `Regira.Entities.Extensions` | `EntityExtensions` *(`IsNew<TKey>()` — on `IEntity<TKey>`; `SetSortOrder()` — on `IEnumerable<ISortable>`)* |

---

## Controllers

| Namespace | Types |
|---|---|
| `Regira.Entities.Web.Controllers.Abstractions` | `EntityControllerBase<>` |
| `Regira.Entities.Web.Models` | `SaveResult<>`, `DeleteResult<>`, `DetailsResult<>`, `ListResult<>`, `SearchResult<>` *(the generated actions' return types — needed to override one)* |
| `Microsoft.AspNetCore.Mvc` | `[ApiController]`, `[Route]`, `ControllerBase` |

---

## Common .NET / EF Core Namespaces

| Namespace | Types |
|---|---|
| `System.ComponentModel.DataAnnotations` | `[Required]`, `[MaxLength]`, `[Range]` |
| `Microsoft.EntityFrameworkCore` | `DbContext`, `DbSet<T>`, `ModelBuilder`, `EntityState`, `EF.Functions.Like(...)`, `Include(...)`, `ThenInclude(...)`, `OrderBy(...)` |
| `Microsoft.EntityFrameworkCore.ChangeTracking` | `EntityEntry` |
| `Microsoft.Extensions.DependencyInjection` | `IServiceCollection`, `IServiceProvider` |

---

## Grouped by Use Case (Quick Lookup)

### Setting up a new project
```
Regira.Licensing.DependencyInjection                             → UseRegira(configuration) (on IServiceCollection)
Regira.Entities.DependencyInjection.Extensions                   → UseEntities<TContext>() (on IServiceCollection)
Regira.Entities.DependencyInjection.Extensions                   → UseDefaults() (on EntityServiceCollectionOptions)
Regira.Entities.Mapping.Mapster                                  → UseMapsterMapping()
Regira.Entities.DependencyInjection.ServiceCollections.Models    → DbContextWiring (à-la-carte flags for e.WireDbContext(...))
Regira.DAL.EFcore.Services                                       → AddAutoTruncateInterceptors()    (standalone EF — auto-wired by UseDefaults())
Regira.DAL.EFcore.Extensions                                     → AddUtcDateTimeConvention()       (standalone EF — auto-wired by UseDefaults())
Regira.DAL.EFcore.Extensions                                     → SetDecimalPrecisionConvention()
Regira.Entities.EFcore.Extensions                                → AddArchivedQueryFilter()        (DbContextOptionsBuilder — auto-wired by UseDefaults(); needed on a hand-built DbContext)
Regira.Entities.EFcore.Extensions                                → SetArchivedQueryFilter()        (OnModelCreating — optional alternative to the wiring)
```

### Creating an entity
```
Regira.Entities.Models.Abstractions          → IEntityWithSerial, IHasTimestamps, IHasTitle,
                                               IHasDescription, IHasCode, IArchivable,
                                               ISortable, IHasNormalizedContent
Regira.Entities.Attachments.Abstractions     → IHasAttachments
Regira.Normalizing                           → [NormalizedAttribute]
System.ComponentModel.DataAnnotations        → [Required], [MaxLength], [Range]
System.ComponentModel.DataAnnotations.Schema → [NotMapped], [Table], [Column]
```

### Creating SearchObject / SortBy / Includes

*When no custom implementation is provided, a default implementation is used.*

| Type | Default |
|---|---|
| SearchObject | `SearchObject<TKey>` (`SearchObject` = `SearchObject<int>`) |
| SortBy | `EntitySortBy` |
| Includes | `EntityIncludes` |

```
Regira.Entities.Models   → SearchObject<>, EntitySortBy, EntityIncludes
```

### Full-text search (Q / keywords)
```
Regira.Entities.Keywords            → QKeyword, ParsedKeywordCollection, QKeywordHelper
Regira.Entities.Keywords.Abstractions → IQKeywordHelper
Regira.Entities.EFcore.Extensions   → QueryExtensions (FilterQ)
```

### Building a query builder
```
Regira.Entities.QueryBuilders.Abstractions          → IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
                                                       ISortedQueryBuilder<TEntity, TKey, TSortBy>
                                                       IIncludableQueryBuilder<TEntity, TKey, TIncludes>
                                                       FilteredQueryBuilderBase<TEntity, TKey, TSearchObject>
                                                       GlobalFilteredQueryBuilderBase<TEntity, TKey>
Regira.Entities.EFcore.Extensions                  → QueryExtensions (FilterId, FilterQ, etc.)
Regira.DAL.Paging                                  → QueryExtensions (PageQuery)
Regira.Entities.Keywords.Abstractions              → IQKeywordHelper
Microsoft.EntityFrameworkCore                      → EF.Functions.Like(...)
```

### Creating a processor
```
Regira.Entities.Processing.Abstractions   → IEntityProcessor<TEntity, TIncludes>
```

### Creating a prepper
```
Regira.Entities.Preppers.Abstractions   → EntityPrepperBase<TEntity>, IEntityPrepper<TEntity>
```

### Marking a field server-owned
```
Regira.Entities.Attributes   → ServerOwnedAttribute   // [ServerOwned] on the entity property
```

### Creating a primer
```
Regira.Entities.EFcore.Primers.Abstractions   → EntityPrimerBase<T>, IEntityPrimer<T>
Microsoft.EntityFrameworkCore.ChangeTracking  → EntityEntry
Microsoft.EntityFrameworkCore                 → EntityState
```

### Creating a normalizer
```
Regira.Entities.Normalizing.Abstractions   → EntityNormalizerBase<T>, IEntityNormalizer<T>
Regira.Normalizing.Abstractions            → INormalizer
Regira.Normalizing.Models                  → NormalizingOptions
```

### Creating a wrapping service
```
Regira.Entities.Services.Abstractions   → EntityWrappingServiceBase<...>, IEntityService<...>
Regira.Entities.Models                  → EntityInputException<T>
Regira.DAL.Paging                       → PagingInfo
```

### Creating a controller
```
Regira.Entities.Web.Controllers.Abstractions   → EntityControllerBase<...>
Microsoft.AspNetCore.Mvc                       → [ApiController], [Route]
```

### Registering an entity in DI
```
Regira.Entities.DependencyInjection.ServiceCollections              → EntityServiceCollection<TContext> (returned by UseEntities())
Regira.Entities.DependencyInjection.ServiceCollections.Models       → EntityServiceCollectionOptions
Regira.Entities.DependencyInjection.ServiceCollections.Abstractions → IEntityServiceCollection<TContext>
Microsoft.EntityFrameworkCore                                       → DbContext (constraint)
```

### Setting up attachments
```
Regira.Entities.Attachments.Abstractions            → IHasAttachments, IHasAttachments<TEntityAttachment>, IEntityAttachment, IAttachmentFileService<TAttachment, TKey>
Regira.Entities.Attachments.Models                  → EntityAttachment (base; = EntityAttachment<int,int,int,Attachment>), Attachment, Attachment<TKey>
Regira.Entities.DependencyInjection.Attachments     → HasAttachments<…>() (extension on the For<>() builder), EntityServiceBuilderExtensions
Regira.Entities.DependencyInjection.ServiceCollections → WithAttachments()   // instance method on EntityServiceCollection<TContext> (the UseEntities return) — no extra using
Regira.Entities.Mapping.Models                      → EntityAttachmentDto, EntityAttachmentInputDto
Regira.Entities.Web.Attachments.Abstractions        → EntityAttachmentControllerBase<TEntity>
Regira.IO.Storage.FileSystem                        → BinaryFileService, FileSystemOptions
Regira.IO.Storage.Azure                             → BinaryBlobService, AzureOptions, AzureCommunicator
Regira.IO.Storage.SSH                               → SftpService, SftpCommunicator, SftpConfig
Regira.IO.Storage.Abstractions                      → IFileService
Regira.Entities.Web.Attachments.DependencyInjection → UseAttachmentUris()   // web apps only — wiring: instructions.md §Attachments
```

---

## Naming convention — avoid entity ↔ namespace-segment collisions

> ⚠️ **Don't name an entity the same as a namespace segment it lives under.** An entity `ShoppingList` inside
> a namespace like `ShoppingList.Api.Entities` (or any namespace whose segment is literally `ShoppingList`)
> makes the type name and the namespace segment collide. The compiler then resolves `ShoppingList` to the
> *namespace* in many positions — generic arguments (`For<ShoppingList>()`, `EntityControllerBase<ShoppingList, …>`),
> `DbSet<ShoppingList>`, `using` aliases — producing confusing CS0118 ("namespace used like a type") errors,
> and breaks IntelliSense navigation.
>
> Fix by renaming the **type** (e.g. `ShoppingListEntity`, `ShoppingCart`) or by nesting it under a differently
> named folder/namespace segment (e.g. `Shopping.Entities.ShoppingList`). Renaming the type is the simplest.
