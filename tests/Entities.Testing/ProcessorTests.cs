using Entities.Testing.Infrastructure.Data;
using Entities.Testing.Infrastructure.Processors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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


    [Test]
    public async Task Test()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        var sp = services.BuildServiceProvider();
        var dbContext = sp.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var inputCategories = Enumerable.Range(0, 3)
            .Select((_, i) => new Category
            {
                Title = $"Category #{i + 1}",
                Products = Enumerable.Range(0, (i + 1) * 2).Select((_, j) => new Product { Title = $"Product #{i + j + 1}" }).ToArray()
            })
            .ToArray();

        dbContext.Categories.AddRange(inputCategories);
        await dbContext.SaveChangesAsync();

        var categories = await dbContext.Categories.ToListAsync();
        var categoryProcessor = new CategoryProcessor(sp.GetRequiredService<ProductContext>());

        foreach (var category in categories)
        {
            Assert.That(category.NumberOfProducts, Is.Null);
        }

        await categoryProcessor.Process(categories, null);

        foreach (var category in categories)
        {
            Assert.That(category.NumberOfProducts, Is.EqualTo(category.Id * 2));
        }
    }
}