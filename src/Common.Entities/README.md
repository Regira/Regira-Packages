# Regira Entities

Regira Entities is a generic, extensible framework for managing **data entities** in .NET applications. 
It provides a **standardized** way to handle CRUD operations, filtering, sorting, and includes, 
while allowing **customization** through generic type parameters, interfaces and specialized helper services.

## Core Concepts

### Generic Type Parameters

Understanding the generic type system is crucial:

| Type | Required | Purpose | Default (when omitted) | Example |
|------|----------|---------|---------|---------|
| TEntity | ✓ | The entity class | - | `Product` |
| TKey | ○ | Primary key type | `int` | `Guid`, `int` |
| TSearchObject | ○ | Advanced filtering | `SearchObject` | `ProductSearchObject` |
| TSortBy | ○ | Sorting enum | `EntitySortBy` | `ProductSortBy` |
| TInclude | ○ | Navigation properties enum | `EntityIncludes` | `ProductIncludes` |
| TDto | ○ | Read/display model (details & lists) | `TEntity` | `ProductDto` |
| TInputDto | ○ | Create/update model | `TEntity` | `ProductInputDto` |

### Architecture

- `IEntityService` is the central contract of this framework. Register an implementation for it — and all CRUD operations, filtering, sorting, and includes are handled through that single interface.
- `EntityRepository` provides the default implementation, but any custom class can be used instead.
- For APIs, using `EntityControllerBase` is sufficient: it registers the `IEntityService` automatically, requiring no additional wiring. The controller's generic type arguments must match those used when configuring the entity (see example below).

Main **functionality** of the service:

| Action  | Purpose |
|---------|---------|
| Details | Get a single item by ID, with all registered Navigation properties included (`RefetchAfterSave`, globally or via `SetReadBehavior(...)` per entity, tunes the save endpoints' response re-fetch) |
| List    | Get a (filtered, sorted & paged) collection of items, usually with limited or no Navigation properties |
| Save    | Create or Update an item, usually Navigation properties are excluded (when updating). However, child collections can be included |
| Remove  | Delete an item |

### Processing Pipeline

Assuming a `Repository` with a `DbContext` is being used.

**Read Pipeline:**

1. EntitySet
1. QueryBuilders 
   1. Filters
   1. Sorting
   1. Paging
   1. Includes
1. Processors
1. Mapping (+AfterMapping)*

**Write Pipeline:**

1. Input
1. Mapping (+AfterMapping)*
1. Preppers (Repository)
1. SaveChanges (DbContext)
   1. Primers (Interceptors)
   1. Submit changes

*\*: only executed when using API controllers*

**Pipeline Details:**
- **QueryBuilders**: Build IQueryable based on SearchObject, SortBy & Includes
- **Processors**: Modify entities after fetching (e.g. setting non-mapped properties)
- **Preppers**: Executed by the Repository before saving to prepare entities
- **Primers**: EF Core SaveChangesInterceptors triggered by DbContext when executing SaveChanges
- **AfterMapper**: Decorates DTOs or Entities after Mapper completes (e.g. calculating URIs)

## Dependency Injection

basic sample setup which whill register a `IEntityService` for Category, Product and Order entities, using the default `EntityRepository` implementation.

```csharp
builder.Services
    .UseRegira(LICENSE) // free tier and trial available
    .UseEntities<MyDbContext>(options => options.UseDefaults())
    .For<Category>()
    .For<Product, int, ProductSearchObject>(item => {
        // inline configuration
        item.SortBy(query => query.OrderBy(x => x.Title));
        item.Includes((query, _) => query.Include(x => x.Category));
        item.Filter((query, so) =>
        {
            if (so?.CategoryId?.Any() == true)
              query = query.Where(x => so.CategoryId.Contains(x.CategoryId));
            return query;
        });
    })
    .For<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes>(item => {
        // external classes for configuration
        item.AddSortBy<OrderSortedBuilder>();
        item.AddIncludes<OrderIncludableBuilder>();
        item.AddFilter<OrderQueryFilter>();
        // OrderRepository will handle OrderItems
        item.Related(c => c.OrderItems);
    });

// controllers
[ApiController, Route("categories")]
public class CategoryController : EntityControllerBase<Category>;
[ApiController, Route("products")]
public class ProductController : EntityControllerBase<Product, int, ProductSearchObject, ProductDto, ProductInputDto>;
[ApiController, Route("orders")]
public class OrderController : EntityControllerBase<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes, OrderDto, OrderInputDto>;
```

> **Free tier available:** `Regira.Entities.DependencyInjection` includes a free tier for small projects. A license key is required once your project grows beyond the free tier limits. Register it with `services.UseRegira(configuration)` (reads `Regira:LicenseKeys`) **before** calling `UseEntities()`. Without a key the free tier applies automatically. Obtain a key at [https://regira.com/licensing](https://regira.com/licensing).

> **Paging defaults:** set `options.DefaultPageSize` / `options.MaxPageSize` in the `UseEntities()` callback (or per entity with `e.SetPageSize(...)`) so List/Search endpoints page automatically instead of returning the full set. See [Web Endpoints → Paging](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html#paging).

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Entities/)** — Overview of Regira Entities
1. [Entity Models](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/models.html) — Creating and structuring entity models
1. [Services](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/services.html) — Implementing entity services and repositories
1. [Mapping](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/mapping.html) — Mapping Entities to and from DTOs
1. [Web Endpoints](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/web-endpoints.html) — Exposing entity operations as HTTP endpoints
1. [Normalizing](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/normalizing.html) — Data normalization techniques
1. [Attachments](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/attachments.html) — Managing file attachments
1. [Built-in Features](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/built-in-features.html) — Ready to use components
1. [Checklist](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/checklist.html) — Step-by-step guide for common tasks
1. [Practical Examples](https://regira.github.io/Regira-Packages/src/Common.Entities/docs/examples.html) — Complete implementation examples
