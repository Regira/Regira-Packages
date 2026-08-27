# Webshop API — Regira Entities Example

> **⚠️ Reference material — mixed fidelity examples.**
> - Some snippets in this file are abbreviated, while others are close to production-ready and only omit surrounding project context, imports, or neighboring files.
> - Before using any configuration call (especially lambdas inside `UseEntities`), verify the exact signature in [`entities.signatures.md`](./entities.signatures.md) — its **§Quick reference — `For<>()` overload → builder** table resolves builder tier, `SortBy` arity, typed `Includes`, and `HasAttachments`/`Related` availability in a single read, so you needn't probe types one by one.
> - **`using` directives are NOT shown in snippets.** Always resolve namespaces from [`entities.namespaces.md`](./entities.namespaces.md) — do not guess. Every Regira type, attribute, and extension method used in this file has its exact namespace listed there.

*Treat these as working patterns, not drop-in files.*

> **Slot budget.** This domain fits the free tier (2 complex / 5 simple): Product and Order are *complex* (typed `SortBy`/`Includes`); Category, Customer, and Supplier are *simple*. Category stays simple yet still eager-loads its parent/child hierarchy through the untyped `Includes` overload — the lever that keeps relation-loading entities off the complex budget. Tier rules: [§License requirement](./entities.instructions.md#license-requirement).

## Structure

> **→ See:** [`entities.setup.md`](./entities.setup.md) — §Project Structure for the recommended per-entity folder layout this example follows.

## Setup

> **→ See:** [`entities.setup.md`](./entities.setup.md) — project scaffolding, `Program.cs`, DbContext wiring, and the `UseEntities()` / `UseDefaults()` / `UseMapsterMapping()` block. Everything below assumes it is already in place.

## DbContext

```csharp no-compile
// Data/WebshopDbContext.cs
public class WebshopDbContext(DbContextOptions<WebshopDbContext> options) : DbContext(options)
{
    public DbSet<Category> Categories { get; set; } = null!;
    public DbSet<RelatedCategory> RelatedCategories { get; set; } = null!;
    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<ProductCategory> ProductCategories { get; set; } = null!;
    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<OrderLine> OrderLines { get; set; } = null!;

    // UTC dates are auto-wired by UseEntities(e => e.UseDefaults()) — see entities.setup.md P3

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.SetDecimalPrecisionConvention(18, 2); // Regira.DAL.EFcore.Extensions

        modelBuilder.Entity<RelatedCategory>(e =>
        {
            e.HasOne(c => c.Parent).WithMany(e => e.ChildEntities).HasForeignKey(c => c.ParentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.Child).WithMany(e => e.ParentEntities).HasForeignKey(c => c.ChildId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ProductCategory>(e =>
        {
            e.HasKey(pc => pc.Id);
            e.HasIndex(pc => new { pc.ProductId, pc.CategoryId }).IsUnique();
            e.HasOne(pc => pc.Product).WithMany(p => p.Categories).HasForeignKey(pc => pc.ProductId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(pc => pc.Category).WithMany().HasForeignKey(pc => pc.CategoryId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.Email).IsUnique();
            e.HasMany(c => c.Orders).WithOne(o => o.Customer).HasForeignKey(o => o.CustomerId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Order>(e =>
        {
            e.HasIndex(o => o.Code).IsUnique();
            e.HasMany(o => o.OrderLines).WithOne(ol => ol.Order).HasForeignKey(ol => ol.OrderId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderLine>(e =>
            e.HasOne(ol => ol.Product).WithMany().HasForeignKey(ol => ol.ProductId).OnDelete(DeleteBehavior.Restrict));

        // Category is IArchivable — nothing to add: UseEntities<TContext>(e => e.UseDefaults())
        // wires the archived query filter into the context's options
    }
}
```

## Category entity

```csharp no-compile
// Entities/Categories/Category.cs
public class Category : IEntityWithSerial, IHasTimestamps, IHasTitle, IHasNormalizedContent, IArchivable
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Title { get; set; } = null!;
    [MaxLength(1024)] public string? Description { get; set; }
    [MaxLength(1024), Normalized(SourceProperties = new[] { nameof(Title), nameof(Description) })]
    public string? NormalizedContent { get; set; }
    public bool IsArchived { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<RelatedCategory>? ParentEntities { get; set; }
    public ICollection<RelatedCategory>? ChildEntities { get; set; }
    [NotMapped] public int? ProductCount { get; set; }  // filled by CategoryProcessor
}

// Entities/Categories/RelatedCategory.cs — self-referential join entity
public class RelatedCategory : IEntityWithSerial
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public int ParentId { get; set; }
    public Category Child { get; set; } = null!;
    public Category Parent { get; set; } = null!;
}

// Entities/Categories/CategorySearchObject.cs
public record CategorySearchObject : SearchObject
{
    public ICollection<int>? ParentId { get; set; }
    public ICollection<int>? ChildId { get; set; }
    public bool? IsRoot { get; set; }
}

// Entities/Categories/CategoryDto.cs
public class CategoryCoreDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
public class CategoryDto : CategoryCoreDto
{
    public ICollection<ParentCategoryDto>? ParentEntities { get; set; }
    public ICollection<ChildCategoryDto>? ChildEntities { get; set; }
    public int? ProductCount { get; set; }
}

// Entities/Categories/RelatedCategoryDto.cs
public class RelatedCategoryDto {
    public int Id { get; set; }
    public int ChildId { get; set; }
    public int ParentId { get; set; }
}
public class ParentCategoryDto : RelatedCategoryDto { public CategoryCoreDto Parent { get; set; } = null!; }
public class ChildCategoryDto  : RelatedCategoryDto { public CategoryCoreDto Child { get; set; } = null!; }

// Entities/Categories/CategoryInputDto.cs + RelatedCategoryInputDto.cs
public class CategoryInputDto
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Title { get; set; } = null!;
    [MaxLength(1024)] public string? Description { get; set; }
    public ICollection<RelatedCategoryInputDto>? ParentEntities { get; set; }
    public ICollection<RelatedCategoryInputDto>? ChildEntities { get; set; }
}
public class RelatedCategoryInputDto
{
    public int Id { get; set; }
    public int ChildId { get; set; }
    public int ParentId { get; set; }
}

// Entities/Categories/CategoryProcessor.cs — separate class processor using DbContext DI
public class CategoryProcessor(WebshopDbContext dbContext) : IEntityProcessor<Category, EntityIncludes>
{
    public async Task Process(IList<Category> items, EntityIncludes? includes, CancellationToken token = default)
    {
        var itemIds = items.Select(x => x.Id).ToList();
        var counts = await dbContext.Categories
            .Where(x => itemIds.Contains(x.Id))
            .Select(x => new { x.Id, Count = dbContext.ProductCategories.Count(pc => pc.CategoryId == x.Id) })
            .ToDictionaryAsync(x => x.Id, v => v.Count);
        foreach (var item in items)
            item.ProductCount = counts.TryGetValue(item.Id, out var count) ? count : null;
    }
}

// Entities/Categories/CategoryServiceConfiguration.cs
public static EntityServiceCollection<WebshopDbContext> AddCategories(this IEntityServiceCollection<WebshopDbContext> services)
    // Simple registration (1 simple slot) — the untyped Includes overload still eager-loads the hierarchy.
    => services.For<Category, int, CategorySearchObject>(e =>
    {
        e.Filter((query, so) =>
        {
            if (so?.ParentId?.Any() == true)
              query = query.Where(x => x.ParentEntities!.Any(pe => so.ParentId.Contains(pe.ParentId)));
            if (so?.ChildId?.Any() == true)
              query = query.Where(x => x.ChildEntities!.Any(ce => so.ChildId.Contains(ce.ChildId)));
            if (so?.IsRoot != null)
                query = so.IsRoot.Value ? query.Where(x => !x.ParentEntities!.Any()) : query.Where(x => x.ParentEntities!.Any());
            return query;
        });
        e.SortBy(query => query.OrderByDescending(x => x.Title));
        // requires: using Microsoft.EntityFrameworkCore;  (Include / ThenInclude / AsSplitQuery)
        // Two collection navigations in one query: .AsSplitQuery() is what keeps EF from warning about the
        // Cartesian explosion. Needed per query like this, or once on the provider via UseQuerySplittingBehavior.
        e.Includes((query, _) => query
            .Include(x => x.ParentEntities!).ThenInclude(x => x.Parent)
            .Include(x => x.ChildEntities!).ThenInclude(x => x.Child)
            .AsSplitQuery());
        e.AddProcessor<CategoryProcessor>();
        e.Related(x => x.ParentEntities);
        e.Related(x => x.ChildEntities);
    });
```

## Product entity

```csharp no-compile
// Entities/Products/Product.cs
public class Product : IEntityWithSerial, IHasTimestamps, IHasTitle, IHasDescription, IHasNormalizedContent
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Title { get; set; } = null!;
    [MaxLength(1024)] public string? Description { get; set; }
    public decimal Price { get; set; }
    [MaxLength(1024), Normalized(SourceProperties = new[] { nameof(Title), nameof(Description) })]
    public string? NormalizedContent { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<ProductCategory>? Categories { get; set; }
}

// Entities/Products/ProductCategory.cs — many-to-many join
public class ProductCategory : IEntityWithSerial
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
}

// Entities/Products/ProductSearchObject.cs
public record ProductSearchObject : SearchObject
{
    public ICollection<int>? CategoryId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
}

// Entities/Products/ProductSortBy.cs
public enum ProductSortBy { Default=0, Title, TitleDesc, Price, PriceDesc }

// Entities/Products/ProductDto.cs
public class ProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<ProductCategoryDto>? Categories { get; set; }
}
public class ProductCategoryDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public CategoryDto? Category { get; set; }
}

// Entities/Products/ProductInputDto.cs
public class ProductInputDto
{
    public int Id { get; set; }
    [Required, MaxLength(64)] public string Title { get; set; } = null!;
    [MaxLength(1024)] public string? Description { get; set; }
    public decimal Price { get; set; }
    public ICollection<ProductCategoryInputDto>? Categories { get; set; }
}
public class ProductCategoryInputDto
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
}

// Entities/Products/ProductQueryBuilder.cs — separate class, handles all product filtering
public class ProductQueryBuilder
    : FilteredQueryBuilderBase<Product, int, ProductSearchObject>
{
    public override IQueryable<Product> Build(IQueryable<Product> query, ProductSearchObject? so)
    {
        if (so == null) return query;
        if (so.CategoryId?.Any() == true) query = query.Where(x => x.Categories!.Any(pc => so.CategoryId.Contains(pc.CategoryId)));
        if (so.MinPrice.HasValue) query = query.Where(p => p.Price >= so.MinPrice.Value);
        if (so.MaxPrice.HasValue) query = query.Where(p => p.Price <= so.MaxPrice.Value);
        return query;
    }
}

// Entities/Products/ProductServiceConfiguration.cs
public static EntityServiceCollection<WebshopDbContext> AddProducts(this IEntityServiceCollection<WebshopDbContext> services)
    => services.For<Product, ProductSearchObject, ProductSortBy, EntityIncludes>(e =>
    {
        e.AddFilter<ProductQueryBuilder>();
        // OrderOrThenBy (Regira.Entities.EFcore.Extensions) starts the ordering or continues it with
        // ThenBy — required because SortBy runs once per requested sort value. Never branch on
        // `query is IOrderedQueryable<T>` yourself: it throws at request time on EF Core.
        e.SortBy((query, sortBy) => sortBy switch
        {
            ProductSortBy.Title => query.OrderOrThenBy(x => x.Title),
            ProductSortBy.TitleDesc => query.OrderOrThenByDescending(x => x.Title),
            ProductSortBy.Price => query.OrderOrThenBy(x => x.Price),
            ProductSortBy.PriceDesc => query.OrderOrThenByDescending(x => x.Price),
            _ => query.OrderOrThenByDescending(x => x.Title)
        });
        // Related() only handles child collection synchronization.
        e.Related(x => x.Categories);
        e.Includes((query, includes) => {
            if (includes?.HasFlag(EntityIncludes.All) == true)
                query = query.Include(x => x.Categories!).ThenInclude(pc => pc.Category);
            return query;
        });
    });
```

## Customer entity

> **Note:** This entity uses `Guid` as the primary key to demonstrate the non-int key workflow. 
In real projects, choose the key type based on your requirements — `int` (auto-increment) is the default and most common choice.

```csharp no-compile
// Entities/Customers/Customer.cs — uses IEntity<Guid> (non-int key)
public class Customer : IEntity<Guid>, IHasTimestamps, IHasNormalizedContent
{
    public Guid Id { get; set; }
    [Required, MaxLength(256)] public string Name { get; set; } = null!;
    [Required, MaxLength(256)] public string Email { get; set; } = null!;
    [MaxLength(512), Normalized(SourceProperties = new[] { nameof(Name), nameof(Email) })]
    public string? NormalizedContent { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<Order>? Orders { get; set; }
}

// Entities/Customers/CustomerDto.cs + CustomerInputDto.cs
public class CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}
public class CustomerInputDto
{
    public Guid? Id { get; set; }  // nullable — omit on create, set on update
    [Required, MaxLength(256)] public string Name { get; set; } = null!;
    [Required, MaxLength(256), EmailAddress] public string Email { get; set; } = null!;
}

// Entities/Customers/CustomerServiceConfiguration.cs — For<TEntity, TKey> overload for non-int key
public static EntityServiceCollection<WebshopDbContext> AddCustomers(this IEntityServiceCollection<WebshopDbContext> services)
    => services.For<Customer, Guid>(e =>
    {
        e.SortBy(query => query.OrderBy(x => x.Name));
        e.Prepare(item => item.Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id); // inline prepper
    });
```

## Supplier entity — simple 3-arg registration (`For<TEntity, int, TSearchObject>`)

The **simple int-key + custom SearchObject** registration. Use it when you want filtering via a
SearchObject but do **not** need typed `TSortBy`/`TIncludes` (which would make it a *complex*
registration). This is a *simple* registration.

```csharp no-compile
// Entities/Suppliers/Supplier.cs
public class Supplier : IEntityWithSerial, IHasTitle, IHasTimestamps
{
    public int Id { get; set; }
    public string? Title { get; set; }                       // IHasTitle is getter-only — declare get;set;
    public ICollection<SupplierTag>? Tags { get; set; }      // owned child collection (managed via Related)
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}

// Entities/Suppliers/SupplierTag.cs — owned child, no standalone For<>() registration
public class SupplierTag : IEntityWithSerial { public int Id { get; set; } public int SupplierId { get; set; } public string? Name { get; set; } }

// Entities/Suppliers/SupplierSearchObject.cs
public record SupplierSearchObject : SearchObject { public string? Title { get; set; } }

// Entities/Suppliers/SupplierServiceConfiguration.cs
public static EntityServiceCollection<AppDbContext> AddSuppliers(this IEntityServiceCollection<AppDbContext> services)
    => services.For<Supplier, int, SupplierSearchObject>(e =>
    {
        e.SortBy(query => query.OrderBy(x => x.Title));                 // simple builder → single-arg lambda
        e.Filter((query, so) => string.IsNullOrWhiteSpace(so?.Title)
            ? query
            : query.Where(x => x.Title!.Contains(so.Title)));
        // Untyped Includes works on simple builders too — only the typed TIncludes overload is complex-only.
        // Eager-loads belong here (not in Filter), so they reach Details. Gate the collection behind the flag
        // so List/Search stay lean — Details passes EntityIncludes.All, clients opt in via ?includes=All.
        // requires: using Microsoft.EntityFrameworkCore;  (Include)
        e.Includes((query, includes) => includes?.HasFlag(EntityIncludes.All) == true
            ? query.Include(x => x.Tags!)
            : query);
        e.Related<SupplierTag>(x => x.Tags);                            // single-arg shortcut (int related key)
    });

// Controllers/SupplierController.cs — N type args on For<> → N+2 on the controller base
[ApiController, Route("suppliers")]                                    // spell the resource; see below
public class SupplierController(/* no constructor params needed */)
    : EntityControllerBase<Supplier, int, SupplierSearchObject, SupplierDto, SupplierInputDto>;

// Resolving the service for seeding/manual use. The 3-arg registration does NOT register the bare
// IEntityService<Supplier> shortcut — resolve one of these instead:
//   IEntityService<Supplier, int>                        // always registered by every .For<>() overload
//   IEntityService<Supplier, int, SupplierSearchObject>  // fully typed (from the Inject-as table)
var supplierService = scope.ServiceProvider.GetRequiredService<IEntityService<Supplier, int>>();
```

## Order + OrderLine entities

```csharp no-compile
// Entities/Orders/OrderStatus.cs
public enum OrderStatus { Pending=0, Processing, Shipped, Delivered, Cancelled }

// Entities/Orders/Order.cs — IHasAggregateKey for event-sourcing/idempotency; IHasNormalizedContent for search
public class Order : IEntityWithSerial, IHasAggregateKey, IHasTimestamps, IHasCode, IHasNormalizedContent
{
    public int Id { get; set; }
    public Guid? AggregateKey { get; set; }
    [MaxLength(16)] public string? Code { get; set; }
    public Guid CustomerId { get; set; }  // FK matches Customer's Guid key
    public Customer? Customer { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public decimal Total { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<OrderLine>? OrderLines { get; set; }
    [MaxLength(1024)] public string? NormalizedContent { get; set; }
}

// Entities/Orders/OrderLine.cs
public class OrderLine : IEntityWithSerial, IHasTimestamps, ISortable
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
    public int SortOrder { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}

// Entities/Orders/OrderSearchObject.cs
public record OrderSearchObject : SearchObject
{
    public string? Code { get; set; }
    public ICollection<Guid>? CustomerId { get; set; }  // Guid FK
    public ICollection<int>? ProductId { get; set; }
    public ICollection<int>? CategoryId { get; set; }
    public ICollection<OrderStatus>? Status { get; set; }
}

// Entities/Orders/OrderIncludes.cs
// A named [Flags] includes enum lets clients request members by name (?includes=Customer,OrderLines).
// Entities on the generic EntityIncludes accept only ?includes=Default / All.
[Flags] public enum OrderIncludes { Default=0, Customer=1<<0, OrderLines=1<<1, All=Customer|OrderLines }

// Entities/Orders/OrderDto.cs + OrderLineDto.cs
public class OrderDto
{
    public int Id { get; set; }
    public Guid? AggregateKey { get; set; }
    public string? Code { get; set; }
    public Guid CustomerId { get; set; }
    public CustomerDto? Customer { get; set; }
    public OrderStatus Status { get; set; }
    public decimal Total { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
    public ICollection<OrderLineDto>? OrderLines { get; set; }
}
public class OrderLineDto
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public int SortOrder { get; set; }
    public ProductDto? Product { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

// Entities/Orders/OrderInputDto.cs + OrderLineInputDto.cs
public class OrderInputDto
{
    public int Id { get; set; }
    public Guid? AggregateKey { get; set; }
    [MaxLength(16)] public string? Code { get; set; }
    public Guid CustomerId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public ICollection<OrderLineInputDto>? OrderLines { get; set; }
}
public class OrderLineInputDto   // no UnitPrice — it is server-owned, resolved from Product.Price in Prepare (price-tampering guard)
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

// Entities/Orders/OrderNormalizer.cs — folds customer + product text into the order so one ?q= hits all three.
// EntityNormalizerBase.HandleNormalize is a no-op: compose the whole value here and assign it. Never append to
// item.NormalizedContent — that self-concatenates on every save until [MaxLength] silently truncates it.
// Run the result through INormalizer: ?q= terms are normalized before matching (§Normalizing contract).
public class OrderNormalizer(WebshopDbContext dbContext, INormalizer normalizer) : EntityNormalizerBase<Order>
{
    public override async Task HandleNormalize(Order item, CancellationToken token = default)
    {
        var productIds = item.OrderLines?.Select(ol => ol.ProductId).Distinct().ToList() ?? [];
        var productTitles = await dbContext.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => p.Title)
            .ToListAsync(token);
        item.NormalizedContent = normalizer.Normalize(string.Join(' ',
            new[] { item.Code, item.Customer?.Name }
                .Concat(productTitles)
                .Where(s => !string.IsNullOrWhiteSpace(s))));
    }
}

// Entities/Orders/OrderQueryBuilder.cs — implements IFilteredQueryBuilder directly; uses QueryExtensions
public class OrderQueryBuilder : IFilteredQueryBuilder<Order, int, OrderSearchObject>
{
    public IQueryable<Order> Build(IQueryable<Order> query, OrderSearchObject? so)
    {
        if (so == null) return query;
        // FilterCode requires IHasCode — Order implements it; inline alternative: query.Where(x => x.Code == so.Code)
        if (!string.IsNullOrWhiteSpace(so.Code)) query = query.FilterCode(so.Code);
        if (so.CustomerId?.Any() == true) query = query.Where(x => so.CustomerId.Contains(x.CustomerId));
        if (so.ProductId?.Any() == true) query = query.Where(x => x.OrderLines!.Any(ol => so.ProductId.Contains(ol.ProductId)));
        if (so.CategoryId?.Any() == true) query = query.Where(x => x.OrderLines!.Any(ol => ol.Product!.Categories!.Any(pc => so.CategoryId.Contains(pc.CategoryId))));
        if (so.Status != null) query = query.Where(x => so.Status.Contains(x.Status));
        return query;
    }
}

// Entities/Orders/OrderManager.cs — EntityWrappingServiceBase with validation + EntityInputException
// Override Add/Modify (not Save): the controller write path calls Save(), and the base Save()
// routes to this service's own Add()/Modify() based on IEntity.IsNew() — so wrapping logic belongs there.
public interface IOrderService : IEntityService<Order, OrderSearchObject, EntitySortBy, OrderIncludes>;
public class OrderManager(IEntityRepository<Order, OrderSearchObject, EntitySortBy, OrderIncludes> service)
    : EntityWrappingServiceBase<Order, OrderSearchObject, EntitySortBy, OrderIncludes>(service), IOrderService
{
    public override Task Add(Order item, CancellationToken token = default) { RequireLines(item.OrderLines?.Any() == true); if (string.IsNullOrWhiteSpace(item.Code)) item.Code = $"ORD-{Guid.NewGuid():N}"[..16]; return base.Add(item, token); } // fits Code's [MaxLength(16)]; a longer value truncates and collides on the unique index
    // Same three-way on the collection as Prepare() below: null = not sent (a status-only PATCH), leave the
    // stored lines alone; [] = an explicit delete-all, which would strand the order without lines. Validating
    // null the same as [] rejects every partial update that doesn't resend the full child list.
    public override Task<Order?> Modify(Order item, CancellationToken token = default) { RequireLines(item.OrderLines is not { Count: 0 }); return base.Modify(item, token); }
    private static void RequireLines(bool hasLines)
    {
        if (!hasLines)
            throw new EntityInputException<Order>("Saving order failed")
            {
                InputErrors = { ["OrderLines"] = "Order must contain at least one order line." }
            };
    }
}

// Entities/Orders/OrderServiceConfiguration.cs
public static EntityServiceCollection<WebshopDbContext> AddOrders(this IEntityServiceCollection<WebshopDbContext> services)
    => services.For<Order, OrderSearchObject, EntitySortBy, OrderIncludes>(e =>
    {
        e.AddFilter<OrderQueryBuilder>();
        e.SortBy((query, sortBy) => query.OrderByDescending(x => x.Created));
        e.Includes((query, includes) => query
            .Include(x => x.Customer!)
            .Include(x => x.OrderLines!.OrderBy(l => l.SortOrder))
                .ThenInclude(ol => ol.Product!));
        e.Related(x => x.OrderLines, item => item.OrderLines?.SetSortOrder()); // resolves because OrderLine : ISortable (IEnumerable<OrderLine> is covariant to IEnumerable<ISortable>); if the element type isn't statically ISortable, cast: (item.OrderLines as IEnumerable<ISortable>)?.SetSortOrder()
        e.Prepare(async (order, dbContext) =>   // typed overload hands the strongly-typed DbContext
        {
            // Three-way on the incoming collection — the Related() contract (Step 5):
            //   null = not sent, stored lines untouched   |   [] = delete-all   |   populated = the new set.
            // Only null may skip the recompute, and even then Total must come from the PERSISTED lines:
            // returning early leaves Total at the DTO's default (0) on every status-only PATCH.
            // The [] branch is unreachable for Order specifically — OrderManager.Modify rejects an empty
            // collection above — but keep the three-way: an aggregate that allows delete-all needs it.
            if (order.OrderLines == null)
            {
                order.Total = order.Id > 0
                    ? await dbContext.OrderLines.AsNoTracking()
                        .Where(l => l.OrderId == order.Id)
                        .SumAsync(l => l.SubTotal)
                    : 0m;
                return;
            }
            // UnitPrice is server-owned: resolve it from the Product, never trust client input (price-tampering guard).
            var productIds = order.OrderLines.Select(l => l.ProductId).ToList();
            var prices = await dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Price);
            foreach (var line in order.OrderLines)
            {
                line.UnitPrice = prices.GetValueOrDefault(line.ProductId);
                line.SubTotal = line.Quantity * line.UnitPrice;
            }
            order.Total = order.OrderLines.Sum(line => line.SubTotal);   // [] sums to 0 — the delete-all case
        });
        e.AddNormalizer<OrderNormalizer>();
        e.AddTransient<IOrderService, OrderManager>();  // enables typed IOrderService injection
        e.UseEntityService<OrderManager>();             // replaces default EntityRepository
    });
```

## Web Endpoints

### Controllers

Use `EntityControllerBase` for entity HTTP endpoints:

```csharp no-compile
// Controllers/CategoryController.cs ~ For<Category, int, CategorySearchObject>()
[ApiController, Route("categories")]
public class CategoryController : EntityControllerBase<Category, int, CategorySearchObject, CategoryDto, CategoryInputDto>;

// Controllers/ProductController.cs ~ For<Product, ProductSearchObject, ProductSortBy, EntityIncludes>()
[ApiController, Route("products")]
public class ProductController : EntityControllerBase<Product, ProductSearchObject, ProductSortBy, EntityIncludes, ProductDto, ProductInputDto>;

// Controllers/CustomerController.cs ~ For<Customer, Guid, SearchObject<Guid>>()
// Guid key: uses TKey overload with SearchObject<Guid>
[ApiController, Route("customers")]
public class CustomerController : EntityControllerBase<Customer, Guid, SearchObject<Guid>, CustomerDto, CustomerInputDto>;

// Controllers/OrderController.cs ~ For<Order, OrderSearchObject, EntitySortBy, OrderIncludes>()
[ApiController, Route("orders")]
public class OrderController : EntityControllerBase<Order, OrderSearchObject, EntitySortBy, OrderIncludes, OrderDto, OrderInputDto>;
```

> **Response envelope.** Responses are wrapped, not bare DTOs — see the §Step 13 endpoints note in [`entities.instructions.md`](./entities.instructions.md#step-13-configure-web-endpoints) for the exact `item`/`items`/`count` shapes.

---

## Additional Patterns

### Sortable owned child with a per-row toggle

An owned child that is **reorderable** *and* has a **per-row flag the user flips independently** (a checklist item, a
task line, an enabled/disabled join row). `Related()` keeps it owned — no `.For<>()`, no controller, no budget slot.

**Ownership follows write cardinality.** `SortOrder` is only meaningful relative to siblings, so reordering is a
collection-level write and travels on the parent's input DTO. `IsDone` changes one row in isolation, so it gets its
own PATCH route.

> ⚠️ **The sync rewrites whole rows.** Because `ChecklistInputDto` carries `Items`, `UpdateRelatedCollection` marks
> every matched row `Modified` and writes **all** its values from the payload. Two consequences:
> - **`IsDone` is deliberately absent** from the child input DTO, so without a guard each save would reset it to
>   `false` on every row — 200 OK, nothing logged. The `e.Prepare(...)` hook below re-stamps it from the store
>   *before* the sync runs, which is what keeps the PATCH route authoritative.
> - **The FK must stay on the child input DTO.** Nothing in `UpdateRelatedCollection` stamps it, and a DTO-mapped
>   instance has a null parent navigation for EF to fix up from — omit `ChecklistId` and every matched row is
>   updated with `0`, which surfaces as a **409** (FK violation) or silently reparents the row.

> This example uses its own `Checklist` aggregate rather than extending the `Order` + `OrderLine` example above,
> which declares a different field set for the same names. Don't merge the two.

```csharp no-compile
// Entities/Checklists/Checklist.cs
public class Checklist : IEntityWithSerial, IHasTitle
{
    public int Id { get; set; }
    public string? Title { get; set; }                        // IHasTitle is getter-only — declare get;set;
    public ICollection<ChecklistItem>? Items { get; set; }    // owned child collection (managed via Related)
}

// Entities/Checklists/ChecklistSearchObject.cs
public record ChecklistSearchObject : SearchObject { public string? Title { get; set; } }

// Entities/Checklists/ChecklistIncludes.cs
[Flags] public enum ChecklistIncludes { Default = 0, Items = 1 << 0 }

// Entities/Checklists/ChecklistItem.cs
public class ChecklistItem : IEntityWithSerial, ISortable
{
    public int Id { get; set; }
    public int ChecklistId { get; set; }
    public Checklist? Checklist { get; set; }
    public string Title { get; set; } = null!;
    public int SortOrder { get; set; }   // collection-level: position in ChecklistInputDto.Items
    public bool IsDone { get; set; }     // per-row: only ever written by PATCH /checklist-items/{id}/done
}

// Entities/Checklists/ChecklistItemInputDto.cs
public class ChecklistItemInputDto
{
    public int Id { get; set; }          // 0 for new items
    public int ChecklistId { get; set; } // FK must be carried — see the warning above
    public string Title { get; set; } = null!;
    // No SortOrder — derived from position by SetSortOrder().
    // No IsDone — owned by the PATCH route and restored by the prepper below.
}

// Entities/Checklists/ChecklistInputDto.cs
public class ChecklistInputDto
{
    public string Title { get; set; } = null!;
    public ICollection<ChecklistItemInputDto>? Items { get; set; }   // list position becomes SortOrder
}
```

```csharp no-compile
// Entities/Checklists/ChecklistServiceConfiguration.cs
public static EntityServiceCollection<AppDbContext> AddChecklists(this IEntityServiceCollection<AppDbContext> services)
    => services.For<Checklist, ChecklistSearchObject, EntitySortBy, ChecklistIncludes>(e =>
    {
        // Ordering is write-side only — nothing sorts ISortable navigations on read, so order inside the include.
        e.Includes((query, includes) => query.Include(x => x.Items!.OrderBy(i => i.SortOrder)));

        // Registered BEFORE Related() so it runs first: re-stamp the per-row field from the store,
        // so a collection-level save cannot clear what the PATCH route owns.
        e.Prepare(async (checklist, db) =>
        {
            if (checklist.Items == null) return;              // null collection: the sync is a no-op anyway
            var ids = checklist.Items.Where(i => i.Id > 0).Select(i => i.Id).ToList();
            var done = await db.ChecklistItems.AsNoTracking()
                .Where(i => ids.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, i => i.IsDone);
            foreach (var item in checklist.Items)
                if (done.TryGetValue(item.Id, out var isDone))
                    item.IsDone = isDone;                     // new items (Id == 0) keep their default
        });

        // Owned child: handles create, reorder, add and remove in one parent save.
        e.Related(x => x.Items, item => item.Items?.SetSortOrder());
    });
```

> ⚠️ **Registration order governs top-level preppers only.** `e.Prepare(...)` written above `e.Related(...)` runs
> before the sync, as shown. But the `prepareFunc` passed *inside* `Related(...)` is registered **after** the
> collection prepper (`Related()` calls `AddPrepper(...)` first, then `Prepare(prepareFunc)`), so it always runs
> **after** the sync regardless of where it appears lexically. `SetSortOrder()` is unaffected — it mutates
> already-tracked instances — but don't use the inline hook for anything that must precede the diff.
>
> The ordering guarantee is also **global across the container**, not per-`For<>()` block: `EntityWriteService`
> receives every registered `IEntityPrepper` and filters by entity type, so preppers added for the same entity from
> another call site interleave by global registration order.

```csharp no-compile
// Controllers/ChecklistItemsController.cs — one route, one field. No .For<ChecklistItem>() anywhere:
// ChecklistItem has no IEntityService, so inject the DbContext and SaveChanges explicitly.
[ApiController]
[Route("api")]
public class ChecklistItemsController(AppDbContext db) : ControllerBase
{
    // Owns IsDone. Never reads or writes SortOrder.
    [HttpPatch("checklist-items/{id:int}/done")]
    public async Task<IActionResult> SetDone(int id, [FromBody] bool isDone)
    {
        var item = await db.ChecklistItems.FindAsync(id);
        if (item == null) return NotFound();
        item.IsDone = isDone;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
```

**Why this stays safe:** each field has exactly one writer. `SortOrder` comes from list position on every parent save;
`IsDone` comes only from the PATCH route, and the prepper re-reads it from the database so the parent save carries
the current value rather than the DTO's default. Reordering and toggling can interleave freely without either
reverting the other.

> **Why not a nested `builder.Prepare(...)`?** It would read better, but `RelatedEntityBuilder.Prepare` takes
> `Action<TRelated>` and wraps it in `EntityPrepper<TRelated>`, which **discards `original`** — even though
> `RelatedCollectionPrepper` passes the matched original row to nested preppers. `PrepperFactories` is `internal`
> and no public overload exposes `original`, so there is no escape hatch: the parent-level
> `Prepare(entity, dbContext)` hook is the only way to reach prior state.

**Simpler alternative:** put `IsDone` on `ChecklistItemInputDto` and drop the `e.Prepare(...)` guard entirely, letting
the client send the current value with each save. Fewer moving parts, but it is last-write-wins — a stale snapshot
reverts a concurrent toggle. Choose one or the other: registering the guard *and* carrying the field makes the DTO
value inert, since the guard overwrites whatever the client sent.

**Covariance note:** `item.Items?.SetSortOrder()` resolves because `ChecklistItem` statically implements `ISortable`.
When the element type isn't statically known to, cast first — `(item.Items as IEnumerable<ISortable>)?.SetSortOrder()`.
`IEnumerable<out T>` is covariant, so this succeeds for any reference element type that implements `ISortable` at
runtime. It yields `null` only for **value-type** element types, where variance doesn't apply.

### Inline processor

```csharp no-compile
e.Process((items, includes) =>
{
    foreach (var item in items)
        item.DisplayPrice = $"€{item.Price:F2}";
    return Task.CompletedTask;
});
```

### Prepper — with DbContext / separate class

The typed `Prepare(Func<TEntity, TContext, Task>)` overload hands you the **strongly-typed `DbContext`**
(the `TContext` from `UseEntities<TContext>()`), so you can look up related rows or apply server-side pricing
before the entity is tracked — no need to inject anything or write a separate prepper class:

> ⚠️ **A total computed from a child collection has three input cases, not two.** `null` = the DTO didn't send
> the collection (the `Related()` sync leaves the stored rows alone), `[]` = delete every row, populated = the
> new set. `?? 0` / `?.Any() != true` collapse `null` and `[]` into one branch and zero the stored total on
> every status-only PATCH — 200 OK, silent corruption. Branch on `null` and re-read the persisted children.

```csharp no-compile
// With DbContext — look up related data and auto-price order lines before save:
e.Prepare(async (order, dbContext) =>
{
    if (order.OrderItems == null)                     // not sent → children untouched; recompute from the store
    {
        order.TotalAmount = order.Id > 0
            ? await dbContext.OrderItems.AsNoTracking()
                .Where(i => i.OrderId == order.Id)
                .SumAsync(i => i.Quantity * i.UnitPrice)
            : 0m;
        return;
    }
    // Batch the lookup: a DB-touching prepper runs per item, so FindAsync per line is N+1 (see below).
    var productIds = order.OrderItems.Select(i => i.ProductId).ToList();
    var prices = await dbContext.Products
        .Where(p => productIds.Contains(p.Id))
        .ToDictionaryAsync(p => p.Id, p => p.Price);
    foreach (var line in order.OrderItems)
        // Pull the authoritative unit price from the DB rather than trusting the client.
        line.UnitPrice = prices.TryGetValue(line.ProductId, out var price) ? price : line.UnitPrice;

    order.TotalAmount = order.OrderItems.Sum(x => x.Quantity * x.UnitPrice);   // [] sums to 0 — delete-all
});

// Separate class:
public class ProductPrepper : EntityPrepperBase<Product>
{
    public override Task Prepare(Product modified, Product? original, CancellationToken token = default)
    {
        modified.Slug ??= modified.Title.ToLowerInvariant().Replace(' ', '-');
        return Task.CompletedTask;
    }
}
// Registration: e.AddPrepper<ProductPrepper>();
```

> ⚠️ **A DB-touching prepper runs once per item, so it is N+1 on any bulk path.** Seeding 500 rows through
> `service.Add(...)` executes the prepper 500 times *before* the single `SaveChanges()` — the queries are not
> batched with the flush. Hoist lookups into a dictionary built once outside the loop, or register the prepper
> only for the request path.

### Primers

```csharp no-compile
// Entity-specific primer — mint a server-owned field on create, and protect it on update.
// Same mechanism as the built-in HasCreatedDbPrimer, which restores Created from OriginalValues this way.
// For this exact case the one-liner is e.ServerOwned(x => x.Code, _ => …) (or [ServerOwned] to protect
// only); write it out as a primer when the value must also be stamped for a raw-DbContext writer.
public class ProductPrimer : EntityPrimerBase<Product>
{
    public override Task PrepareAsync(Product entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Added)
            entity.Code ??= Guid.NewGuid().ToString("N")[..8].ToUpper();          // mint on create
        else if (entry.State == EntityState.Modified)                            // restore on update — Code is off
            entity.Code = (string?)entry.OriginalValues[nameof(entity.Code)];    // ProductInputDto, so the map nulls it
        return Task.CompletedTask;
    }
}
// Registration: e.AddPrimer<ProductPrimer>();

// Global primer: options.AddPrimer<YourGlobalPrimer>();
// using Regira.Entities.DependencyInjection.Primers;   ← AddPrimer lives here
```

### Mapping — UseMapping / AddMapping (usually not needed)

With Mapster (the default), `TEntity ↔ TDto`/`TInputDto` mapping — **including nested objects and child
collections** — works by convention whenever the DTO shape resembles the entity. The Category, Product
and Order configurations above register **no** mapping at all and still round-trip their nested
collections correctly. Reach for these calls only in the cases below. (Full rule: `entities.instructions.md` §Step 10.)

- **`UseMapping<TDto, TInputDto>()`** — needed when you want to attach an **after-mapper**
  (`.After(...)` to enrich the output DTO, `.AfterInput(...)` to tweak the entity after input mapping) or
  otherwise customise the top-level mapping. See the **AfterMapper** pattern below for the canonical example.
- **`AddMapping<TSource, TTarget>()`** — an escape hatch: register an explicit mapping for a specific
  (usually nested/child) type pair **only** when Mapster's convention produces the wrong result — e.g. a
  child DTO whose shape diverges from the entity, or a child input type that needs a custom mapping. It is
  **not** required to project nested collections.

> If a nested collection comes back **empty** in an API response, that is almost always a missing
> **`Includes`** (the navigation was never loaded from the database), not a missing mapping. Check
> `e.Includes(...)` first.

### AfterMapper

```csharp no-compile
// Inline:
e.UseMapping<ProductDto, ProductInputDto>()
    .After((entity, dto) =>
    {
        dto.DisplayName = $"{entity.Title} - €{entity.Price:F2}";
    })
    .AfterInput((inputDto, entity) =>
    {
        // runs after InputDto → Entity mapping
    });

// Separate class (with DI):
public class ProductAfterMapper(IHttpContextAccessor httpContextAccessor) : EntityAfterMapperBase<Product, ProductDto>
{
    public override void AfterMap(Product source, ProductDto target)
        => target.ImageUrl = $"https://{httpContextAccessor.HttpContext?.Request.Host}/images/{source.Id}";
}
// Registration: e.UseMapping<ProductDto, ProductInputDto>().After<ProductAfterMapper>();
// Global:       options.AddAfterMapper<MyGlobalAfterMapper>();
```

### IQKeywordHelper — Q full-text search

```csharp no-compile
public class ProductQueryBuilder(IQKeywordHelper qHelper) : FilteredQueryBuilderBase<Product, int, ProductSearchObject>
{
    public override IQueryable<Product> Build(IQueryable<Product> query, ProductSearchObject? so)
    {
        if (!string.IsNullOrWhiteSpace(so?.Q))
        {
            var keywords = qHelper.Parse(so.Q);
            foreach (var keyword in keywords)
                query = query.Where(x => EF.Functions.Like(x.NormalizedContent, keyword.QW));
        }
        return query;
    }
}
```

### Global filter query builder

```csharp no-compile
// Separate class — applies to all entities implementing the interface:
public class FilterByTenantQueryBuilder(ITenantContext tenantContext) : GlobalFilteredQueryBuilderBase<ITenantEntity, int>
{
    public override IQueryable<ITenantEntity> Build(IQueryable<ITenantEntity> query, ISearchObject<int>? so)
        => query.Where(x => x.TenantId == tenantContext.CurrentTenantId);
}
// Registration: options.AddGlobalFilterQueryBuilder<FilterByTenantQueryBuilder>();
// using Regira.Entities.DependencyInjection.QueryBuilders;   ← AddGlobalFilterQueryBuilder lives here

// Built-in Q search for all IHasNormalizedContent entities:
// ⚠️ UseDefaults() already calls this automatically — only register manually if you are
//    NOT using UseDefaults() and want to enable Q-based full-text search globally.
options.AddGlobalFilterQueryBuilder<FilterHasNormalizedContentQueryBuilder>();
```

### Global normalizer

```csharp no-compile
// Uses INormalizer to manually control normalization output:
public class ProductNormalizer(INormalizer normalizer) : EntityNormalizerBase<Product>
{
    // INormalizer.Normalize(string) is synchronous (returns string) — do not await it.
    public override Task HandleNormalize(Product item, CancellationToken token = default)
    {
        item.NormalizedContent = normalizer.Normalize($"{item.Title} {item.Description}".Trim());
        return Task.CompletedTask;
    }
}
// Per-entity: e.AddNormalizer<ProductNormalizer>();
// Global:     options.AddNormalizer<IHasPhone, PhoneNormalizer>();
```

### Paging defaults

```csharp no-compile
// Default + max page size — enforced by the controller List/Search endpoints. An omitted pageSize uses the
// default; pageSize <= 0 opts out and falls back to the max; a positive pageSize is clamped to the max.
services.UseEntities<WebshopDbContext>(options =>
{
    options.DefaultPageSize = 50;   // applied when the request omits pageSize
    options.MaxPageSize = 200;      // caps every request; also what a pageSize <= 0 opt-out returns
})
    // Per-entity override — fully replaces the global values for this entity:
    .For<Product>(e => e.SetPageSize(defaultPageSize: 25, maxPageSize: 100))
    // Opt out entirely — this entity is never force-paged (omitted / pageSize <= 0 returns every row):
    .For<Category>(e => e.SetPageSize());

// Note: enforced at the HTTP boundary only. A direct IEntityService.List(so) call (no PagingInfo)
// returns the full set uncapped — the service layer keeps full control.
```

### Attachments

> **Two registrations, two jobs — you need both.** `WithAttachments(factory)` registers the *shared*
> `Attachment` entity, the file store and the bytes→file primer. `HasAttachments<…>(x => x.Attachments)` — chained on
> the owner's `For<>()` builder — registers the *typed* per-owner read/write services, the link prepper and
> the DTO mapping, plus a per-owner join entity. Slot cost: [§License requirement](./entities.instructions.md#license-requirement).

```csharp no-compile
// Attachment entity — inherit the `EntityAttachment` base (maps to int, int, int, Attachment) and set
// ObjectType in the constructor.
public class ProductAttachment : EntityAttachment
{
    public ProductAttachment() => ObjectType = nameof(Product);
}

// Entity:
public class Product : IEntityWithSerial, IHasAttachments, IHasAttachments<ProductAttachment>
{
    public bool? HasAttachment { get; set; }
    public ICollection<ProductAttachment>? Attachments { get; set; }
    ICollection<IEntityAttachment>? IHasAttachments.Attachments
    {
        get => Attachments?.Cast<IEntityAttachment>().ToArray();
        set => Attachments = value?.Cast<ProductAttachment>().ToArray();
    }
}

// Mapped owner (UseMapping)? The DTOs carry `ICollection<EntityAttachmentInputDto>? Attachments` /
// `ICollection<EntityAttachmentDto>? Attachments` — see the recipe's input-DTO step in entities.instructions.

// Controller — the class route is the owner base path; the base controller appends the sub-routes
// (`{objectId}/attachments`, `attachments/{id}`, `{objectId}/files`, ...).
[ApiController, Route("products")]
public class ProductAttachmentsController : EntityAttachmentControllerBase<ProductAttachment> { }
// Endpoints exposed: POST {objectId}/files (upload), PUT {objectId}/files/{id} (replace),
// GET {objectId}/attachments (list), GET attachments/{id}, PUT {objectId}/attachments/{id} (metadata),
// DELETE attachments/{id}, GET files/{id} and GET {objectId}/files/{fileName} (download).

// DbContext:
public DbSet<Attachment> Attachments { get; set; } = null!;
public DbSet<ProductAttachment> ProductAttachments { get; set; } = null!;
// OnModelCreating:
modelBuilder.Entity<ProductAttachment>()
    .HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
modelBuilder.Entity<Product>(entity =>
    entity.HasMany(e => e.Attachments).WithOne().HasForeignKey(e => e.ObjectId).HasPrincipalKey(e => e.Id));

// DI:
services
    .AddHttpContextAccessor()                          // web apps: required for attachment Uri resolution
    .UseEntities<AppDbContext>(options =>
    {
        options.UseDefaults();
        options.UseAttachmentUris();                   // web apps: resolve attachment DTO Uri's (opt-in, from Regira.Entities.Web)
        /* ... */
    })
    // 1. shared Attachment entity + file store + bytes→file primer
    .WithAttachments(sp => new BinaryFileService(
        new FileSystemOptions { RootFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads") }))
    // 2. typed per-owner services + link prepper + DTO mapping
    .For<Product>(e => e.HasAttachments<AppDbContext, Product, ProductAttachment>(x => x.Attachments));
// Note: UseAttachmentUris() lives in Regira.Entities.Web.Attachments.DependencyInjection, and must be set
// on this same options instance. A null Uri is never an error — entities.instructions § Attachments lists
// all four causes and which one logs a warning.
```

> **File-service factory.** `WithAttachments` takes an `IFileService` *factory* (not a registered
> `IFileService`), so your app stays free to register its own store(s) for other features without conflict.
> Build one inline — `WithAttachments(_ => new BinaryFileService(...))` — or reuse an app-registered one —
> `WithAttachments(p => p.GetRequiredService<IFileService>())`. Either way it's wrapped into the registered
> `IAttachmentFileService<Attachment, int>` (one per attachment base type).

> **Reading file bytes.** Use the built-in download endpoints, or inject
> `IAttachmentFileService<Attachment, int>` and call `GetBytes(item)`. Consuming code references files by
> `Identifier` (the public storage key, populated when you load through the entity service); `Path` is
> internal and isn't mapped to DTOs — clients get a download `Uri` instead.

> **Creating attachments in code** (bulk insert / import): see [`entities.patterns.md`](./entities.patterns.md) — Bulk insert / update.

### Query extensions reference

> Each `QueryExtensions` method requires the entity to implement the listed interface. If it does not,
> use inline LINQ (e.g. `query.Where(x => x.Code == so.Code)`) as a drop-in replacement.
> See [`entities.signatures.md`](./entities.signatures.md) — §QueryExtensions for full interface constraints.

```csharp no-compile
// From Regira.Entities.EFcore.Extensions (QueryExtensions):
query.FilterId(so.Id)                   // requires IEntity<TKey>
query.FilterIds(so.Ids)                 // requires IEntity<TKey>
query.FilterExclude(so.Exclude)         // requires IEntity<TKey>
query.FilterCode(so.Code)               // requires IHasCode
query.FilterTitle(keywords)             // requires IHasTitle
query.FilterNormalizedTitle(keywords)   // requires IHasNormalizedTitle
query.FilterCreated(so.MinCreated, so.MaxCreated)               // requires IHasCreated
query.FilterLastModified(so.MinLastModified, so.MaxLastModified) // requires IHasLastModified
query.FilterTimestamps(minCreated, maxCreated, minModified, maxModified) // requires IHasTimestamps
query.FilterQ(keywords)                 // requires IHasNormalizedContent
query.FilterArchivable(so.Archived ?? ArchivedFilter.Excluded) // requires IArchivable (non-nullable arg)
query.FilterHasAttachment(so.HasAttachment) // requires IHasAttachments
query.SortQuery<TEntity, TKey>()
query.PageQuery(pagingInfo)
query.PageQuery(pageSize: 20, page: 1)
```
