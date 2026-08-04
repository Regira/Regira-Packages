using Entities.Testing.Infrastructure.Data;
using Entities.Testing.Infrastructure.Primers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Entities.Testing;

[TestFixture]
public class InterceptorTests
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


    [Test]
    public async Task Execute_EntityPrimer_By_Interceptor()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<ProductPrimer>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product
        {
            Title = "Product (test)"
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Description, Is.EqualTo(ProductPrimer.DescriptionMessage));
        Assert.That(item.Created, Is.EqualTo(DateTime.MinValue));
        Assert.That(item.LastModified, Is.Null);
    }

    [Test]
    public async Task ApplyTo_Interface()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<IHasEncryptedPassword, PasswordPrimer>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var account = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = "TestUser",
            Password = "Testing Primer"
        };

        dbContext.Users.Add(account);

        await dbContext.SaveChangesAsync();

        Assert.That(account.EncryptedPassword, Is.Not.Null);
        Assert.That(account.EncryptedPassword, Is.EqualTo(account.Password.Base64Encode()));
        Assert.That(account.EncryptedPassword!.Base64Decode(), Is.EqualTo(account.Password));
    }

    [Test]
    public async Task Fill_Timestamps()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
            {
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp));
            })
            .AddPrimer<TimestampPrimer>();
        services.RegisterPrimerContainer<ProductContext>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var created = DateTime.UtcNow;
        var item = new Product
        {
            Title = "Product (test)",
            Created = created
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Created, Is.EqualTo(created));
        Assert.That(item.LastModified, Is.Null);

        dbContext.Entry(item).State = EntityState.Modified;

        await dbContext.SaveChangesAsync();
        Assert.That(item.Created, Is.EqualTo(created));
        Assert.That(item.LastModified, Is.Not.Null);

        Assert.That(item.LastModified, Is.GreaterThan(item.Created));
    }
    [Test]
    public async Task Fill_ChildCollection_Timestamps()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<IHasTimestamps, TimestampPrimer>();

        var sp = services.BuildServiceProvider();

        var created = DateTime.UtcNow.AddDays(-1);

        // use different scopes for insert & update to simulate disconnected update and prevent tracking issues
        using (var insertScope = sp.CreateScope())
        {
            var dbContext = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Category
            {
                Title = "Category (test)",
                Created = created,
                Products = new List<Product>
                {
                    new() { Title = "Product (1)", Created = created },
                    new() { Title = "Product (2)" }
                }
            };
            dbContext.Categories.Add(item);

            await dbContext.SaveChangesAsync();
        }
        
        var updatedTitle = "Category (updated)";
        using (var updateScope = sp.CreateScope())
        {
            var dbContext = updateScope.ServiceProvider.GetRequiredService<ProductContext>();
            var modifiedItem = new Category
            {
                Id = 1,
                Title = updatedTitle,
                Products = new List<Product>
                {
                    new() { Id = 1, Title = "Product (1)", Description = "updated"},
                    new() { Title = "Product (3)", Created = created }
                }
            };

            var originalItem = await dbContext.Categories
                .Include(x => x.Products)
                .AsNoTrackingWithIdentityResolution()
                .SingleAsync(x => x.Id == 1);
            
            dbContext.Attach(modifiedItem);
            dbContext.Entry(modifiedItem).OriginalValues.SetValues(originalItem);
            dbContext.Update(modifiedItem);

            dbContext.UpdateRelatedCollection(modifiedItem, originalItem, x => x.Products);

            await dbContext.SaveChangesAsync();
            
            Assert.That(modifiedItem.Title, Is.EqualTo(updatedTitle));
            Assert.That(modifiedItem.Created, Is.EqualTo(created));
            Assert.That(modifiedItem.LastModified, Is.Not.Null);

            await dbContext.SaveChangesAsync();
            
            Assert.That(modifiedItem.Products?.Count, Is.EqualTo(2));
            foreach (var product in modifiedItem.Products!)
            {
                Assert.That(product.Created, Is.EqualTo(created));
                if (product.Id == 1)
                {
                    Assert.That(product.LastModified, Is.Not.Null);
                    Assert.That(product.LastModified, Is.GreaterThan(product.Created));
                }
                Assert.That(product.CategoryId, Is.EqualTo(originalItem.Id));
            }
        }
    }

    [Test]
    public async Task Execute_Base_Primers()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<IHasTimestamps, TimestampPrimer>()
            .AddPrimer<NormalizingPrimer>()
            .AddPrimer<Product, ProductPrimer>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product
        {
            Title = "Product (test)"
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Id, Is.GreaterThan(0));
        Assert.That(item.Description, Is.EqualTo(ProductPrimer.DescriptionMessage));
        Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue));
        Assert.That(item.LastModified, Is.Null);

        var item1 = await dbContext.Products.FindAsync(item.Id);
        var newTitle = "Product (modified)";
        item1!.Title = newTitle;

        await dbContext.SaveChangesAsync();

        Assert.That(item1.Title, Is.EqualTo(newTitle));
        Assert.That(item1.NormalizedTitle, Is.EqualTo("Product modified"));
        Assert.That(item1.LastModified, Is.Not.Null);
    }

    [Test]
    public async Task Execute_Base_Primers_In_Order()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<NormalizingPrimer>()
            // overwrites Normalized title using Uppercase Normalizer
            .AddPrimer<IHasNormalizedTitle, TitlePrimer>()
            .AddPrimer<Product, ProductPrimer>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product
        {
            Title = "Product (test)"
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Description, Is.EqualTo(ProductPrimer.DescriptionMessage));
        Assert.That(item.NormalizedTitle, Is.EqualTo("PRODUCT TEST"));
    }

    [Test]
    public async Task Execute_Matching_Primers()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer(p => new CategoryPrimer(p.GetRequiredService<ProductContext>(), new TimestampPrimer(), new NormalizingPrimer()))
            .AddPrimer<Product, ProductPrimer>();

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Category
        {
            Title = "Category (test)"
        };
        dbContext.Categories.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.NormalizedTitle, Is.EqualTo("Category test"));
        Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue));
        Assert.That(item.LastModified, Is.Null);
        Assert.That(item.Description, Is.Null);
    }

    [Test]
    public async Task ApplyTo_Many_Entries()
    {
        var items = Enumerable.Range(0, 10).Select((_, i) => new Category { Title = $"Category #{i + 1}" }).ToArray();

        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<IHasTimestamps, TimestampPrimer>()
            .AddPrimer(p => new CategoryPrimer(p.GetRequiredService<ProductContext>(), new TimestampPrimer(), new NormalizingPrimer()));

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Categories.AddRange(items);

        await dbContext.SaveChangesAsync();

        for (var i = 1; i < items.Length; i++)
        {
            Assert.That(items[i - 1].SortOrder, Is.LessThan(items[i].SortOrder));
        }
    }
}