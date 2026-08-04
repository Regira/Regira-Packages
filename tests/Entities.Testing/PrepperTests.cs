using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

[TestFixture]
public class PrepperTests
{
    private SqliteConnection _connection = null!;
    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    [TearDown]
    public void TearDown()
    {
        _connection.Close();
    }


    /// <summary>
    /// Tests that the nested prepper is called for both existing items (update) and new items
    /// (Id=0), but NOT for deleted items (absent from modified).
    /// </summary>
    [Test]
    public async Task Nested_Prepper_Is_Called_For_Existing_And_New_Items()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();

        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new() { Title = "Product (1)" },   // will become Id=1
                    new() { Title = "Product (2)" },   // will become Id=2
                }
            });
            await ctx.SaveChangesAsync();
        }

        // Track which product Ids the nested prepper is called for (new item has Id=0 at call time)
        var calledForIds = new List<int>();
        IEntityPrepper<Product> trackingPrepper = new EntityPrepper<Product>(p => calledForIds.Add(p.Id));

        using var updateScope = sp.CreateScope();
        var updateCtx = updateScope.ServiceProvider.GetRequiredService<ProductContext>();

        var original = await updateCtx.Categories
            .Include(x => x.Products)
            .AsNoTrackingWithIdentityResolution()
            .SingleAsync(x => x.Id == 1);

        var modified = new Category
        {
            Id = 1,
            Title = "Category (updated)",
            Products = new List<Product>
            {
                new() { Id = 1, Title = "Product (1 - modified)" },   // existing → nested prepper called with original
                new() { Title = "Product (3)" },                       // new (Id=0) → nested prepper called with null original
                // Product (2) omitted → deleted → nested prepper should NOT be called
            }
        };

        var prepper = new RelatedCollectionPrepper<ProductContext, Category, Product, int, int>(
            updateCtx,
            x => x.Products,
            [trackingPrepper]);

        await prepper.Prepare(modified, original);

        // Both existing (Id=1) and new (Id=0) products should trigger the nested prepper;
        // deleted Product (2) should not.
        Assert.That(calledForIds, Is.EquivalentTo(new[] { 1, 0 }));
    }

    /// <summary>
    /// Tests that when a parent item has no original (it is new), nested preppers are still
    /// invoked for its child items — covering the else-branch in RelatedCollectionPrepper.
    /// </summary>
    [Test]
    public async Task Nested_Prepper_Is_Called_With_Null_Original_For_New_Parent()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();

        await sp.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();

        var calledForTitles = new List<string?>();
        IEntityPrepper<Product> trackingPrepper = new EntityPrepper<Product>(p => calledForTitles.Add(p.Title));

        using var scope = sp.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ProductContext>();

        var newCategory = new Category
        {
            Id = 0,
            Title = "New Category",
            Products = new List<Product>
            {
                new() { Title = "Product (A)" },
                new() { Title = "Product (B)" },
            }
        };

        var prepper = new RelatedCollectionPrepper<ProductContext, Category, Product, int, int>(
            ctx,
            x => x.Products,
            [trackingPrepper]);

        // No original — parent is brand new
        await prepper.Prepare(newCategory, null);

        Assert.That(calledForTitles, Is.EquivalentTo(new[] { "Product (A)", "Product (B)" }));
    }

    /// <summary>
    /// Tests that without nested preppers the prepper still correctly adds/removes/updates
    /// related items (no regression from existing behavior).
    /// </summary>
    [Test]
    public async Task Without_Nested_Preppers_Related_Collection_Is_Updated_Correctly()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();

        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new() { Title = "Product (1)" },
                    new() { Title = "Product (2)" },
                }
            });
            await ctx.SaveChangesAsync();
        }

        using var updateScope = sp.CreateScope();
        var updateCtx = updateScope.ServiceProvider.GetRequiredService<ProductContext>();

        var original = await updateCtx.Categories
            .Include(x => x.Products)
            .AsNoTrackingWithIdentityResolution()
            .SingleAsync(x => x.Id == 1);

        var modified = new Category
        {
            Id = 1,
            Title = "Category (updated)",
            Products = new List<Product>
            {
                new() { Id = 1, Title = "Product (1 - modified)" },
                new() { Title = "Product (3)" },
                // Product (2) removed
            }
        };

        // No nested preppers
        var prepper = new RelatedCollectionPrepper<ProductContext, Category, Product, int, int>(
            updateCtx,
            x => x.Products);

        await prepper.Prepare(modified, original);
        await updateCtx.SaveChangesAsync();

        var savedProducts = await updateCtx.Products.ToListAsync();
        Assert.That(savedProducts.Count, Is.EqualTo(2));
        Assert.That(savedProducts.Any(p => p.Title == "Product (1 - modified)"), Is.True);
        Assert.That(savedProducts.Any(p => p.Title == "Product (3)"), Is.True);
    }

    /// <summary>
    /// Tests a 3-level hierarchy: Category → Products → ProductTags.
    /// When a Product within a Category is updated, any nested related collection (Tags)
    /// on that Product is also correctly updated by the recursive nested prepper.
    /// </summary>
    [Test]
    public async Task Three_Level_Hierarchy_Updates_Nested_Collections()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();

        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Insert Category → Product → Tags
        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new()
                    {
                        Title = "Product (1)",
                        Tags = new List<ProductTag>
                        {
                            new() { Value = "tag-a" },   // will be Id=1
                            new() { Value = "tag-b" },   // will be Id=2
                        }
                    }
                }
            });
            await ctx.SaveChangesAsync();
        }

        using var updateScope = sp.CreateScope();
        var updateCtx = updateScope.ServiceProvider.GetRequiredService<ProductContext>();

        var original = await updateCtx.Categories
            .Include(x => x.Products!)
            .ThenInclude(p => p.Tags)
            .AsNoTrackingWithIdentityResolution()
            .SingleAsync(x => x.Id == 1);

        // Modify: update tag-a, add tag-c, remove tag-b
        var modified = new Category
        {
            Id = 1,
            Title = "Category (updated)",
            Products = new List<Product>
            {
                new()
                {
                    Id = 1,
                    Title = "Product (1 - modified)",
                    Tags = new List<ProductTag>
                    {
                        new() { Id = 1, Value = "tag-a-updated" },   // existing → update
                        new() { Value = "tag-c" },                    // new → add
                        // tag-b omitted → delete
                    }
                }
            }
        };

        // Level 2: nested prepper for Product.Tags
        var tagsPrepper = new RelatedCollectionPrepper<ProductContext, Product, ProductTag, int, int>(
            updateCtx,
            x => x.Tags);

        // Level 1: Category.Products prepper, with tagsPrepper as nested
        var productsPrepper = new RelatedCollectionPrepper<ProductContext, Category, Product, int, int>(
            updateCtx,
            x => x.Products,
            [tagsPrepper]);

        await productsPrepper.Prepare(modified, original);
        await updateCtx.SaveChangesAsync();

        var savedTags = await updateCtx.ProductTags.ToListAsync();
        Assert.That(savedTags.Count, Is.EqualTo(2), "tag-b should be deleted, tag-c should be added");
        Assert.That(savedTags.Any(t => t.Value == "tag-a-updated"), Is.True);
        Assert.That(savedTags.Any(t => t.Value == "tag-c"), Is.True);
    }

    /// <summary>
    /// Tests that the builder API (EntityIntServiceBuilder.Related with configure action)
    /// correctly wires up the nested prepper and a Prepare action.
    /// </summary>
    [Test]
    public async Task Builder_API_Related_With_Configure_Wires_Nested_Preppers()
    {
        const string preparedMarker = "PREPARED";

        var services = new ServiceCollection()
            .AddDbContext<ProductContext>(db => db.UseSqlite(_connection))
            .UseEntities<ProductContext>()
            .For<Category>(e =>
            {
                // Load Products when fetching the original for comparison
                e.Includes((q, _) => q.Include(c => c.Products));
                e.Related<Product>(x => x.Products, products =>
                {
                    // Mark each existing product's Description when it's updated
                    products.Prepare(p => p.Description = preparedMarker);
                });
            });

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Insert a category with one product
        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new() { Title = "Product (1)" },
                }
            });
            await ctx.SaveChangesAsync();
        }

        // Update via the write service so preppers are invoked
        using (var updateScope = sp.CreateScope())
        {
            var writeService = updateScope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            var modified = new Category
            {
                Id = 1,
                Title = "Category (updated)",
                Products = new List<Product>
                {
                    new() { Id = 1, Title = "Product (1 - modified)" },
                }
            };
            await writeService.Save(modified);
            await updateScope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var savedProduct = await readScope.ServiceProvider.GetRequiredService<ProductContext>().Products.SingleAsync(p => p.Id == 1);

        Assert.That(savedProduct.Description, Is.EqualTo(preparedMarker),
            "The nested Prepare action should have been called for the existing product.");
    }

    /// <summary>
    /// Tests the 3-level hierarchy using the builder API.
    /// Category.Related(Products, items => items.Related(Tags)) should update tags when
    /// a category with products is saved.
    /// </summary>
    [Test]
    public async Task Builder_API_Three_Level_Hierarchy_Updates_Nested_Collections()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>(db => db.UseSqlite(_connection))
            .UseEntities<ProductContext>()
            .For<Category>(e =>
            {
                // Load Products + Tags when fetching the original for comparison
                e.Includes((q, _) => q.Include(c => c.Products!).ThenInclude(p => p.Tags));
                e.Related<Product>(x => x.Products, products =>
                {
                    products.Related(x => x.Tags);
                });
            });

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // Insert Category → Product → Tags
        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new()
                    {
                        Title = "Product (1)",
                        Tags = new List<ProductTag>
                        {
                            new() { Value = "tag-a" },
                            new() { Value = "tag-b" },
                        }
                    }
                }
            });
            await ctx.SaveChangesAsync();
        }

        // Update via write service: keep product 1, remove tag-b, add tag-c
        using (var updateScope = sp.CreateScope())
        {
            var writeService = updateScope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            var modified = new Category
            {
                Id = 1,
                Title = "Category (updated)",
                Products = new List<Product>
                {
                    new()
                    {
                        Id = 1,
                        Title = "Product (1 - modified)",
                        Tags = new List<ProductTag>
                        {
                            new() { Id = 1, Value = "tag-a-updated" },
                            new() { Value = "tag-c" },
                        }
                    }
                }
            };
            await writeService.Save(modified);
            await updateScope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var savedTags = await readScope.ServiceProvider.GetRequiredService<ProductContext>().ProductTags.ToListAsync();

        Assert.That(savedTags.Count, Is.EqualTo(2), "tag-b should be deleted, tag-c should be added");
        Assert.That(savedTags.Any(t => t.Value == "tag-a-updated"), Is.True);
        Assert.That(savedTags.Any(t => t.Value == "tag-c"), Is.True);
    }

    /// <summary>
    /// Guarantee: the original always exposes its related rows to the diff, even when the caller's
    /// reload did NOT eager-load the navigation (original.Products == null). The prepper must reload
    /// the collection itself so existing rows are reconciled (kept/updated/deleted) instead of being
    /// blindly re-inserted. Without this, an un-included navigation makes every save non-idempotent
    /// and breaks unique constraints on join tables.
    /// </summary>
    [Test]
    public async Task Related_Reloads_Original_Collection_When_Navigation_Was_Not_Included()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();

        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category
            {
                Id = 1,
                Title = "Category",
                Products = new List<Product>
                {
                    new() { Title = "Product (1)" },   // Id=1
                    new() { Title = "Product (2)" },   // Id=2
                }
            });
            await ctx.SaveChangesAsync();
        }

        using var updateScope = sp.CreateScope();
        var updateCtx = updateScope.ServiceProvider.GetRequiredService<ProductContext>();

        // Reload WITHOUT Include(Products) — simulates an entity whose Includes config does not
        // eager-load the related collection. original.Products is therefore null.
        var original = await updateCtx.Categories
            .AsNoTrackingWithIdentityResolution()
            .SingleAsync(x => x.Id == 1);
        Assert.That(original.Products, Is.Null, "precondition: navigation not eager-loaded");

        var modified = new Category
        {
            Id = 1,
            Title = "Category (updated)",
            Products = new List<Product>
            {
                new() { Id = 1, Title = "Product (1 - modified)" },   // existing → update
                new() { Title = "Product (3)" },                       // new → add
                // Product (2) omitted → delete
            }
        };

        var prepper = new RelatedCollectionPrepper<ProductContext, Category, Product, int, int>(
            updateCtx,
            x => x.Products);

        await prepper.Prepare(modified, original);
        await updateCtx.SaveChangesAsync();

        var savedProducts = await updateCtx.Products.AsNoTracking().ToListAsync();
        Assert.That(savedProducts.Select(p => p.Id), Is.EquivalentTo(new[] { 1, 3 }),
            "Product (2) should be deleted and Product (3) added — not a blind re-insert of everything");
        Assert.That(savedProducts.Single(p => p.Id == 1).Title, Is.EqualTo("Product (1 - modified)"),
            "the existing row should be updated in place, matched by id from the reloaded original");
    }
}
