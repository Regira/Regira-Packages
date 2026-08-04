# Practical Examples

This guide demonstrates the Regira Entities framework using a simple webshop scenario with Products and Categories.

## Example 1: Product Entity (Full Implementation)

### Entity Model

```csharp
public class Product : IEntity<int>, IHasTimestamps, IArchivable, IHasTitle, IHasDescription
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    
    // Normalization support
    [Normalized(SourceProperties = [nameof(Title), nameof(Description)])]
    public string? NormalizedContent { get; set; }
    
    // Built-in interfaces
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public bool IsArchived { get; set; }
    
    // Navigation
    public Category? Category { get; set; }
}
```

### SearchObject

`SearchObject<TKey>` (and its `SearchObject` shorthand for int keys) is a `record`, so subclasses must also be records.

```csharp
public record ProductSearchObject : SearchObject
{
    public int? CategoryId { get; set; }
    public ICollection<int>? CategoryIds { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}
```

### SortBy Enum

```csharp
public enum ProductSortBy
{
    Default = 0,
    Title,
    TitleDesc,
    Price,
    PriceDesc,
    Created,
    CreatedDesc
}
```

### Includes Enum

```csharp
[Flags]
public enum ProductIncludes
{
    Default = 0,
    Category = 1 << 0,
    All = Category
}
```

### Query Builder (Separate Class)

```csharp
public class ProductQueryBuilder : FilteredQueryBuilderBase<Product, int, ProductSearchObject>
{
    public override IQueryable<Product> Build(IQueryable<Product> query, ProductSearchObject? so)
    {
        if (so == null) return query;

        // Filter by CategoryId
        if (so.CategoryId.HasValue)
            query = query.Where(x => x.CategoryId == so.CategoryId.Value);

        // Filter by CategoryIds
        if (so.CategoryIds?.Any() == true)
            query = query.Where(x => so.CategoryIds.Contains(x.CategoryId));

        // Price range
        if (so.MinPrice.HasValue)
            query = query.Where(x => x.Price >= so.MinPrice.Value);
        if (so.MaxPrice.HasValue)
            query = query.Where(x => x.Price <= so.MaxPrice.Value);

        return query;
    }
}
```

### DTOs

```csharp
public class ProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryTitle { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}

public class ProductInputDto
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = null!;
    [MaxLength(1000)]
    public string? Description { get; set; }
    [Range(0, 999999)]
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
}
```

### Controller

```csharp
[ApiController]
[Route("[controller]")]
public class ProductsController : EntityControllerBase<Product, ProductSearchObject, ProductSortBy, ProductIncludes, ProductDto, ProductInputDto>
{
}
```

### Dependency Injection

```csharp
services.UseEntities<ShopDbContext>(options =>
{
    options.AddDefaultEntityNormalizer();
})
.For<Product, ProductSearchObject, ProductSortBy, ProductIncludes>(e =>
{
    e.AddFilter<ProductQueryBuilder>()
        .UseMapping<ProductDto, ProductInputDto>()
        .After((product, dto) =>
        {
            // AfterMapper: Add category title to DTO
            dto.CategoryTitle = product.Category?.Title;
        });
});
```

## Example 2: Category with Inline Configuration

### Entity Model

```csharp
public class Category : IEntity<int>, IHasTitle
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public ICollection<Product>? Products { get; set; }
}
```

### SearchObject

```csharp
public record CategorySearchObject : SearchObject
{
    // Uses default SearchObject properties only
}
```

### DTOs

```csharp
public class CategoryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public int ProductCount { get; set; }
}

public class CategoryInputDto
{
    [Required, MaxLength(100)]
    public string Title { get; set; } = null!;
}
```

### Controller

```csharp
[ApiController]
[Route("[controller]")]
public class CategoriesController : EntityControllerBase<Category, CategoryDto, CategoryInputDto>
{
}
```

### Dependency Injection (Inline Configuration)

```csharp
services.UseEntities<ShopDbContext>(options => { /* ... */ })
.For<Category>(e =>
{
    // Inline QueryBuilder
    e.Filter((query, so) =>
    {
        // Title search using Q property
        if (!string.IsNullOrWhiteSpace(so?.Q))
            query = query.Where(x => EF.Functions.Like(x.Title, $"%{so.Q}%"));
        return query;
    })
        .UseMapping<CategoryDto, CategoryInputDto>()
        // Inline AfterMapper
        .After((category, dto) =>
        {
            dto.ProductCount = category.Products?.Count ?? 0;
        });
});
```

## Example 3: Product Attachments

### Entity Attachment Model

```csharp
// Inherit the `EntityAttachment` base (= EntityAttachment<int,int,int,Attachment>) and set ObjectType
// in the constructor.
public class ProductAttachment : EntityAttachment
{
    public ProductAttachment() => ObjectType = nameof(Product);
}
```

### Update Product Entity

```csharp
public class Product : IEntity<int>, IHasTimestamps, IArchivable, IHasTitle, IHasDescription,
    IHasAttachments, IHasAttachments<ProductAttachment>
{
    // ... existing properties ...
    
    // Attachment support
    public bool? HasAttachment { get; set; }
    public ICollection<ProductAttachment>? Attachments { get; set; }
    ICollection<IEntityAttachment>? IHasAttachments.Attachments
    {
        get => Attachments?.Cast<IEntityAttachment>().ToArray();
        set => Attachments = value?.Cast<ProductAttachment>().ToArray();
    }
}
```

### DbContext Configuration

```csharp
public class ShopDbContext : DbContext
{
    public DbSet<Product> Products { get; set; }
    public DbSet<Category> Categories { get; set; }
    public DbSet<Attachment> Attachments { get; set; }
    public DbSet<ProductAttachment> ProductAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.SetDecimalPrecisionConvention(18, 2);
        
        modelBuilder.Entity<ProductAttachment>()
            .HasOne(x => x.Attachment)
            .WithMany()
            .HasForeignKey(x => x.AttachmentId);

        // Product is IArchivable — nothing to add: UseEntities<TContext>(e => e.UseDefaults())
        // wires the archived query filter into the context's options
    }
}
```

### Web Endpoints

Both entity and attachment endpoints require a controller:

```csharp
[ApiController]
[Route("[controller]")]
public class ProductsController : EntityControllerBase<Product, ProductSearchObject, ProductSortBy, ProductIncludes, ProductDto, ProductInputDto>
{
}

// The class route is the owner base path; the base actions append the sub-routes
// {objectId}/attachments, attachments/{id}, {objectId}/files, files/{id}, ...
[ApiController]
[Route("products")]
public class ProductAttachmentsController : EntityAttachmentControllerBase<ProductAttachment>
{
}
```

### Dependency Injection

Attachments need **two** registrations: `WithAttachments(factory)` registers the shared `Attachment`
entity, the file store and the bytes→file primer (framework infrastructure — **no license slot**), and
`HasAttachments<…>(x => x.Attachments)` — chained on the owner's `For<>()` builder — registers the typed
per-owner services, the link prepper and DTO mapping (**one simple-tier slot** — the per-owner join entity).

```csharp
// only the provider — UseEntities(options => options.UseDefaults()) below auto-wires the
// interceptors and the UTC date convention
services.AddDbContext<ShopDbContext>(db =>
{
    db.UseSqlServer(connectionString);
});

services
    .AddHttpContextAccessor()                          // web apps: required for attachment Uri resolution
    .UseEntities<ShopDbContext>(options =>
    {
        options.UseDefaults();
        options.UseAttachmentUris();                   // web apps: resolve attachment DTO Uri's (opt-in)
        /* ... */
    })
    // 1. shared Attachment entity + file store + bytes→file primer (framework infrastructure — no license slot)
    .WithAttachments(sp => new BinaryFileService(
        new FileSystemOptions { RootFolder = "uploads/products" }))
    // 2. typed per-owner services + link prepper + DTO mapping
    .For<Product, ProductSearchObject, ProductSortBy, ProductIncludes>(e =>
        e.HasAttachments<ShopDbContext, Product, ProductAttachment>(x => x.Attachments));
```

> **Reading file bytes:** use the built-in download endpoints, or inject
> `IAttachmentFileService<Attachment, int>` and call `GetBytes(item)`. Consuming code references files by
> `Identifier` (the public storage key, populated when you load through the entity service); `Path` is
> internal and isn't mapped to DTOs — clients get a download `Uri` instead.

## Example 4: Custom Normalizer

### Separate Normalizer Class

```csharp
public class ProductNormalizer : EntityNormalizerBase<Product>
{
    private readonly INormalizer _normalizer;

    public ProductNormalizer(INormalizer normalizer)
    {
        _normalizer = normalizer;
    }

    public override async Task HandleNormalize(Product item, CancellationToken token = default)
    {
        var content = $"{item.Title} {item.Description}".Trim();
        item.NormalizedContent = await _normalizer.Normalize(content);
    }
}
```

### Registration

```csharp
services.UseEntities<ShopDbContext>(options => { /* ... */ })
.For<Product>(e =>
{
    e.AddNormalizer<ProductNormalizer>();
    // ... rest of configuration ...
});
```

## Overview

1. [Index](../README.md) — Overview of Regira Entities
1. [Entity Models](models.md) — Creating and structuring entity models
1. [Services](services.md) — Implementing entity services and repositories
1. [Mapping](mapping.md) — Mapping Entities to and from DTOs
1. [Web Endpoints](web-endpoints.md) — Exposing entity operations as HTTP endpoints
1. [Normalizing](normalizing.md) — Data normalization techniques
1. [Attachments](attachments.md) — Managing file attachments
1. [Built-in Features](built-in-features.md) — Ready to use components
1. [Checklist](checklist.md) — Step-by-step guide for common tasks
1. **[Practical Examples](examples.md)** — Complete implementation examples
