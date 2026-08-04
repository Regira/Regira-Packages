using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The seam between a front-end owned-collection editor and <c>Related()</c>. Editors hand a row added in the
/// current session a NEGATIVE temp id so it has a stable key before the server has seen it. Such a row matches
/// no original, so the diff correctly classifies it as new — but attaching it as <c>Added</c> with the temp key
/// still set makes the provider insert that key verbatim (SQLite accepts a negative rowid without complaint).
/// The row looks right in the UI and is only visibly wrong to someone who inspects primary keys.
/// </summary>
[TestFixture]
public class RelatedCollectionTempIdTests
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    private ServiceProvider Build() => new ServiceCollection()
        .AddDbContext<ProductContext>(db => db.UseSqlite(_connection))
        .UseEntities<ProductContext>()
        .For<Category>(e =>
        {
            e.Includes((q, _) => q.Include(c => c.Products!));
            e.Related<Product>(x => x.Products);
        })
        .Services
        .BuildServiceProvider();

    [Test]
    public async Task A_Negative_Temp_Id_On_A_New_Related_Row_Is_Not_Persisted_As_The_Primary_Key()
    {
        var sp = Build();
        await sp.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category { Id = 1, Title = "Category", Products = [new Product { Title = "Existing" }] });
            await ctx.SaveChangesAsync();
        }

        using (var updateScope = sp.CreateScope())
        {
            var writeService = updateScope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            await writeService.Save(new Category
            {
                Id = 1,
                Title = "Category",
                Products =
                [
                    new Product { Id = 1, Title = "Existing" },
                    // What useOwnedCollection sends for a row added in this session.
                    new Product { Id = -1, Title = "Added in the editor" },
                ]
            });
            await updateScope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var products = await readScope.ServiceProvider.GetRequiredService<ProductContext>().Products.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(products, Has.Count.EqualTo(2), "the new row is inserted, as before");
            Assert.That(products.Select(p => p.Id), Has.All.GreaterThan(0), "no row keeps a client-minted temp key");
            Assert.That(products.Single(p => p.Title == "Added in the editor").Id, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task A_Zero_Id_Still_Inserts_And_An_Existing_Id_Still_Updates()
    {
        // The normalization must not disturb the two cases that already worked.
        var sp = Build();
        await sp.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category { Id = 1, Title = "Category", Products = [new Product { Title = "Existing" }] });
            await ctx.SaveChangesAsync();
        }

        using (var updateScope = sp.CreateScope())
        {
            var writeService = updateScope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            await writeService.Save(new Category
            {
                Id = 1,
                Title = "Category",
                Products = [new Product { Id = 1, Title = "Renamed" }, new Product { Id = 0, Title = "Brand new" }]
            });
            await updateScope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var products = await readScope.ServiceProvider.GetRequiredService<ProductContext>().Products.ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(products, Has.Count.EqualTo(2));
            Assert.That(products.Single(p => p.Id == 1).Title, Is.EqualTo("Renamed"));
            Assert.That(products.Any(p => p.Title == "Brand new"), Is.True);
        });
    }

    [Test]
    public async Task A_Stale_Positive_Id_Is_Inserted_With_That_Id_Not_Silently_Renumbered()
    {
        // Pins the documented contract: only TEMP (negative) keys are cleared. A positive key that matches no
        // original means the client is sending a stale id for a row someone else deleted; it is inserted
        // explicitly rather than absorbed, which is what makes the client bug visible instead of silent.
        var sp = Build();
        await sp.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();

        using (var insertScope = sp.CreateScope())
        {
            var ctx = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            ctx.Categories.Add(new Category { Id = 1, Title = "Category", Products = [new Product { Title = "Existing" }] });
            await ctx.SaveChangesAsync();
        }

        using (var updateScope = sp.CreateScope())
        {
            var writeService = updateScope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            await writeService.Save(new Category
            {
                Id = 1,
                Title = "Category",
                Products = [new Product { Id = 1, Title = "Existing" }, new Product { Id = 4242, Title = "Stale id" }]
            });
            await updateScope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var products = await readScope.ServiceProvider.GetRequiredService<ProductContext>().Products.ToListAsync();

        Assert.That(products.Single(p => p.Title == "Stale id").Id, Is.EqualTo(4242),
            "a non-temp key is kept — the guide documents this as a client bug to fix, not a case the server absorbs");
    }

    /// <summary>
    /// The parent FK question: a NEW parent saved together with new children. The children reach the store
    /// through the parent's navigation, so EF's relationship fixup assigns the FK once the parent's key is
    /// generated — nothing has to stamp it client-side, and a client that stamps 0 is what breaks it.
    /// </summary>
    [Test]
    public async Task A_New_Parent_Saved_With_New_Children_Gets_The_Child_Foreign_Key_From_Ef_Fixup()
    {
        var sp = Build();
        await sp.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();

        using (var scope = sp.CreateScope())
        {
            var writeService = scope.ServiceProvider.GetRequiredService<IEntityWriteService<Category, int>>();
            await writeService.Save(new Category
            {
                Title = "Brand new category",
                // No CategoryId on either child, and the parent has no key yet.
                Products = [new Product { Title = "Child A" }, new Product { Title = "Child B" }]
            });
            await scope.ServiceProvider.GetRequiredService<ProductContext>().SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var ctx = readScope.ServiceProvider.GetRequiredService<ProductContext>();
        var category = await ctx.Categories.Include(c => c.Products!).SingleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(category.Id, Is.GreaterThan(0));
            Assert.That(category.Products, Has.Count.EqualTo(2));
            Assert.That(category.Products!.Select(p => p.CategoryId), Has.All.EqualTo(category.Id));
        });
    }
}
