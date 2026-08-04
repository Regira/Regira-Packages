using Entities.Testing.Infrastructure.Data;
using Entities.Testing.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

[TestFixture]
public class ServiceTests
{
    [Test]
    public void EntityType_only()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Category>();
        using var sp = services.BuildServiceProvider();
        var queryBuilder = sp.GetService<IQueryBuilder<Category, int, SearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Category>>();
        var entityService2 = sp.GetService<IEntityService<Category, int>>();
        Assert.That(queryBuilder, Is.Not.Null);
        Assert.That(entityService1, Is.Not.Null);
        Assert.That(entityService2, Is.Not.Null);
    }
    [Test]
    public void EntityType_And_KeyType()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Category, int>();
        using var sp = services.BuildServiceProvider();
        var queryBuilder = sp.GetService<IQueryBuilder<Category, int, SearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityService = sp.GetService<IEntityService<Category, int>>();
        Assert.That(queryBuilder, Is.Not.Null);
        Assert.That(entityService, Is.Not.Null);
    }
    [Test]
    public void EntityType_And_KeyType_And_SearchObject()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Product, int, ProductSearchObject>();
        using var sp = services.BuildServiceProvider();
        var queryBuilder = sp.GetService<IQueryBuilder<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Product, int>>();
        var entityService2 = sp.GetService<IEntityService<Product, int, ProductSearchObject>>();
        Assert.That(queryBuilder, Is.Not.Null);
        Assert.That(entityService1, Is.Not.Null);
        Assert.That(entityService2, Is.Not.Null);
    }
    [Test]
    public void Complex_Service_Without_Key()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Product, ProductSearchObject, EntitySortBy, EntityIncludes>();
        using var sp = services.BuildServiceProvider();
        var queryBuilder = sp.GetService<IQueryBuilder<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        var entityService0 = sp.GetService<IEntityService<Product>>();
        var entityService1 = sp.GetService<IEntityService<Product, int>>();
        var entityService2 = sp.GetService<IEntityService<Product, int, ProductSearchObject>>();
        var entityService3 = sp.GetService<IEntityService<Product, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        var entityService4 = sp.GetService<IEntityService<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        Assert.That(queryBuilder, Is.Not.Null);
        Assert.That(entityService0, Is.Not.Null);
        Assert.That(entityService1, Is.Not.Null);
        Assert.That(entityService2, Is.Not.Null);
        Assert.That(entityService3, Is.Not.Null);
        Assert.That(entityService4, Is.Not.Null);
    }
    [Test]
    public void Complex_Service_With_Key()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>();
        using var sp = services.BuildServiceProvider();
        var queryBuilder = sp.GetService<IQueryBuilder<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        var readService2 = sp.GetService<IEntityReadService<Product, int>>();
        var readService5 = sp.GetService<IEntityReadService<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        var writeService3 = sp.GetService<IEntityWriteService<Product, int>>();
        var entityService1 = sp.GetService<IEntityService<Product, int>>();
        var entityService2 = sp.GetService<IEntityService<Product, int, ProductSearchObject>>();
        var entityService3 = sp.GetService<IEntityService<Product, int, ProductSearchObject, EntitySortBy, EntityIncludes>>();
        Assert.That(queryBuilder, Is.Not.Null);
        Assert.That(readService2, Is.Not.Null);
        Assert.That(readService5, Is.Not.Null);
        Assert.That(writeService3, Is.Not.Null);
        Assert.That(entityService1, Is.Not.Null);
        Assert.That(entityService2, Is.Not.Null);
        Assert.That(entityService3, Is.Not.Null);
    }

    [Test]
    public void Custom_EntityService()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Product>(e => e.UseEntityService<ProductService>());
        using var sp = services.BuildServiceProvider();

        var queryBuilder5 = sp.GetService<IQueryBuilder<Product, int, SearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Product>>();
        var entityService2 = sp.GetService<IEntityService<Product, int>>();

        Assert.That(queryBuilder5, Is.Not.Null);
        Assert.That(entityService1, Is.TypeOf<ProductService>());
        Assert.That(entityService2, Is.TypeOf<ProductService>());
    }
    [Test]
    public void Custom_QueryBuilder()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Product>(e => e.UseQueryBuilder<ProductQueryBuilder>());
        using var sp = services.BuildServiceProvider();

        var queryBuilder5 = sp.GetService<IQueryBuilder<Product, int, SearchObject<int>, EntitySortBy, EntityIncludes>>();
        var entityService1 = sp.GetService<IEntityService<Product>>();
        var entityService2 = sp.GetService<IEntityService<Product, int>>();

        Assert.That(queryBuilder5, Is.TypeOf<ProductQueryBuilder>());
        Assert.That(entityService1, Is.Not.Null);
        Assert.That(entityService2, Is.Not.Null);
    }

    // Regression: with a non-int key the global Id filter was silently skipped (only FilterIdsQueryBuilder<int>
    // was registered), so Details(id) ran SingleOrDefault over the whole table and threw. The string-keyed
    // User exercises the same non-int code path as the reported Guid scenario.
    [Test]
    public async Task Details_With_NonIntKey_Returns_Single_Row()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"regira-nonintkey-{Guid.NewGuid():N}.db");
        using var sp = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite($"Filename={dbFile}"))
            .UseEntities<ProductContext>(e => e.UseDefaults())
            .For<User, string, SearchObject<string>>()
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductContext>();
        await db.Database.EnsureCreatedAsync();
        db.Users.AddRange(
            new User { Id = "u1", Username = "alice" },
            new User { Id = "u2", Username = "bob" },
            new User { Id = "u3", Username = "carol" });
        await db.SaveChangesAsync();

        var readService = scope.ServiceProvider.GetRequiredService<IEntityReadService<User, string>>();
        var details = await readService.Details("u2");

        Assert.That(details, Is.Not.Null);
        Assert.That(details!.Id, Is.EqualTo("u2"));
        Assert.That(details.Username, Is.EqualTo("bob"));

        await db.Database.EnsureDeletedAsync();
    }

    // Regression: the controller write path calls Save(), not Add(). A wrapping service that overrides
    // Add must still have that override run via Save() — base Save() routes to its own Add/Modify.
    [Test]
    public async Task WrappingService_Add_Override_Runs_On_Save_Path()
    {
        var dbFile = Path.Combine(Path.GetTempPath(), $"regira-wrap-{Guid.NewGuid():N}.db");
        using var sp = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite($"Filename={dbFile}"))
            .UseEntities<ProductContext>()
            .For<Customer, int, SearchObject<int>>(e => e.UseEntityService<CustomerAddOverrideService>())
            .BuildServiceProvider();

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ProductContext>();
        await db.Database.EnsureCreatedAsync();

        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Customer, int>>();
        Assert.That(service, Is.TypeOf<CustomerAddOverrideService>());

        var customer = new Customer();
        await service.Save(customer);   // write path uses Save(), never Add() directly
        await service.SaveChanges();

        var saved = await db.Customers.FindAsync(customer.Id);
        Assert.That(saved, Is.Not.Null);
        Assert.That(saved!.Name, Is.EqualTo(CustomerAddOverrideService.GeneratedName));

        await db.Database.EnsureDeletedAsync();
    }

    // A4 (documented behavior): the bare IEntityService<TEntity> alias is only registered by the int-keyed
    // For<TEntity>()/complex-int overloads. The explicit-key overloads (For<T,int>, For<T,int,TSearch>) register
    // the keyed IEntityService<TEntity, int> as the canonical resolve — the bare alias is not constructible from
    // their keyed implementation. Always resolve via IEntityService<TEntity, TKey> when an explicit key was used.
    [Test]
    public void ExplicitKey_Overloads_Resolve_Via_Keyed_EntityService()
    {
        var services = new ServiceCollection()
            .AddDbContext<ProductContext>((_, db) => db.UseSqlite("Filename=:memory:"))
            .UseEntities<ProductContext>()
            .For<Category, int>()
            .For<Product, int, ProductSearchObject>();
        using var sp = services.BuildServiceProvider();

        // Canonical resolve for explicit-key registrations:
        Assert.That(sp.GetService<IEntityService<Category, int>>(), Is.Not.Null);
        Assert.That(sp.GetService<IEntityService<Product, int>>(), Is.Not.Null);

        // The bare alias is intentionally not registered by the explicit-key overloads.
        Assert.That(sp.GetService<IEntityService<Category>>(), Is.Null);
        Assert.That(sp.GetService<IEntityService<Product>>(), Is.Null);
    }
}