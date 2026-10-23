using Entities.Testing.Infrastructure.Data;
using Entities.Testing.Infrastructure.Processors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

[TestFixture]
public class ProcessorTests
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


    // through the entity service, so it is the read pipeline that runs the registered processor
    [Test]
    public async Task A_Registered_Processor_Fills_The_Listed_Items()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>()
            .For<Category>(e => e.AddProcessor<CategoryProcessor>());
        await using var sp = services.BuildServiceProvider();

        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ProductContext>();
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.Categories.AddRange(Enumerable.Range(1, 3)
                .Select(i => new Category
                {
                    Title = $"Category #{i}",
                    Products = Enumerable.Range(1, i * 2).Select(j => new Product { Title = $"Product #{i}.{j}" }).ToArray()
                }));
            await dbContext.SaveChangesAsync();
        }

        using var readScope = sp.CreateScope();
        var categories = await readScope.ServiceProvider.GetRequiredService<IEntityService<Category>>().List(new SearchObject());

        Assert.That(categories.Select(x => (x.Title, x.NumberOfProducts)),
            Is.EquivalentTo(new (string?, int?)[] { ("Category #1", 2), ("Category #2", 4), ("Category #3", 6) }));
    }
}