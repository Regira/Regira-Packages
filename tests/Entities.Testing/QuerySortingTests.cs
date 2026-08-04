using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Extensions;

namespace Entities.Testing;

[TestFixture]
public class QuerySortingTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;
    private ProductContext _dbContext = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        _dbContext.Products.AddRange(
            new Product { Title = "Bravo", Created = new DateTime(2024, 3, 1) },
            new Product { Title = "Alpha", Created = new DateTime(2024, 1, 1) },
            new Product { Title = "Alpha", Created = new DateTime(2024, 2, 1) }
        );
        await _dbContext.SaveChangesAsync();
    }
    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
        _serviceProvider.Dispose();
        _connection.Close();
    }

    [Test]
    public async Task OrderOrThenBy_Starts_Ordering_On_Unordered_Query()
    {
        var items = await _dbContext.Products
            .Where(x => x.Id > 0)
            .OrderOrThenBy(x => x.Title)
            .ToListAsync();

        Assert.That(items.Select(x => x.Title), Is.EqualTo(new[] { "Alpha", "Alpha", "Bravo" }));
    }

    [Test]
    public async Task OrderOrThenBy_Continues_Ordering_As_ThenBy()
    {
        var items = await _dbContext.Products
            .OrderBy(x => x.Title)
            .OrderOrThenBy(x => x.Created)
            .ToListAsync();

        Assert.That(items.Select(x => x.Created.Month), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public async Task OrderOrThenByDescending_Starts_And_Continues()
    {
        var started = await _dbContext.Products
            .OrderOrThenByDescending(x => x.Title)
            .ToListAsync();
        Assert.That(started.Select(x => x.Title), Is.EqualTo(new[] { "Bravo", "Alpha", "Alpha" }));

        var continued = await _dbContext.Products
            .OrderBy(x => x.Title)
            .OrderOrThenByDescending(x => x.Created)
            .ToListAsync();
        Assert.That(continued.Select(x => x.Created.Month), Is.EqualTo(new[] { 2, 1, 3 }));
    }

    [Test]
    public void OrderOrThenBy_Prevents_The_Bare_Is_Check_Crash()
    {
        // an EF Core query satisfies `is IOrderedQueryable<T>` before any OrderBy is composed,
        // so the intuitive guard + ThenBy throws — the exact runtime failure OrderOrThenBy exists for
        IQueryable<Product> unordered = _dbContext.Products.Where(x => x.Id > 0);
        if (unordered is IOrderedQueryable<Product> misclassified)
        {
            Assert.Throws<ArgumentException>(() => misclassified.ThenBy(x => x.Title));
        }
        else
        {
            Assert.Inconclusive("EF Core no longer statically implements IOrderedQueryable on unordered queries.");
        }

        Assert.DoesNotThrow(() => unordered.OrderOrThenBy(x => x.Title));
    }
}
