using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Utilities;

namespace Entities.Testing;

[TestFixture]
public class ReadBehaviorTests
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

    private async Task<(ServiceProvider sp, int productId, List<EntityIncludes?> capturedIncludes)> BuildProvider(
        Action<Regira.Entities.DependencyInjection.ServiceCollections.Models.EntityServiceCollectionOptions>? configureOptions = null)
    {
        var captured = new List<EntityIncludes?>();
        var services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => configureOptions?.Invoke(o))
            .For<Product>(e =>
            {
                e.Includes((query, includes) =>
                {
                    captured.Add(includes);
                    return query;
                });
            });

        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();
        var product = new Product { Title = "Read behavior test" };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync();
        return (sp, product.Id, captured);
    }

    [Test]
    public async Task Details_Applies_Max_Includes_By_Default()
    {
        var (sp, id, captured) = await BuildProvider();
        using var _ = sp;
        var service = sp.GetRequiredService<IEntityService<Product, int>>();

        var item = await service.Details(id);

        Assert.That(item, Is.Not.Null);
        Assert.That(captured, Has.Some.EqualTo(EnumUtility.GetMaxFlagValue<EntityIncludes>()));
    }

    // Shared paging clamp algorithm (used by MVC and the FastEndpoints auto-endpoints)
    [TestCase(null, 10, 100, 10)]   // omitted → default
    [TestCase(0, 10, 100, 100)]     // explicit opt-out → max
    [TestCase(25, 10, 100, 25)]     // explicit within range
    [TestCase(999, 10, 100, 100)]   // explicit above max → clamped
    public void ApplyPagingDefaults_Clamps(int? requested, int? defaultSize, int? maxSize, int expected)
    {
        var pagingInfo = requested == null ? null : new PagingInfo { PageSize = requested };
        var result = pagingInfo.ApplyPagingDefaults(new EntityListOptions { DefaultPageSize = defaultSize, MaxPageSize = maxSize });
        Assert.That(result?.PageSize, Is.EqualTo(expected));
    }

    [Test]
    public void ApplyPagingDefaults_Without_Options_Keeps_Paging_Untouched()
    {
        Assert.That(((PagingInfo?)null).ApplyPagingDefaults(null), Is.Null);
        var pagingInfo = new PagingInfo { PageSize = 5 };
        Assert.That(pagingInfo.ApplyPagingDefaults(null)?.PageSize, Is.EqualTo(5));
    }
}
