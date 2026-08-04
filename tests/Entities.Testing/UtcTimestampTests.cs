using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers;
using Regira.Utilities;
using System.Text.Json;

namespace Entities.Testing;

/// <summary>
/// Covers the UTC timestamp pipeline: <see cref="HasCreatedDbPrimer"/> / <see cref="HasLastModifiedDbPrimer"/>,
/// the UTC value converter (<c>SetUtcDateTimeConvention</c> on <see cref="ProductContext"/>), filter normalization
/// and the <see cref="DateTimeDefaults.UseUtc"/> policy.
/// </summary>
[TestFixture]
public class UtcTimestampTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _serviceProvider = new ServiceCollection()
            .AddDbContext<ProductContext>((sp, db) =>
                db.UseSqlite(_connection)
                    .AddInterceptors(new EntityPrimerContainerInterceptor(sp))
            )
            .AddPrimer<HasCreatedDbPrimer>()
            .AddPrimer<HasLastModifiedDbPrimer>()
            .BuildServiceProvider();
    }
    [TearDown]
    public void TearDown()
    {
        DateTimeDefaults.UseUtc = true; // restore the ambient policy for other fixtures
        _serviceProvider.Dispose();
        _connection.Close();
    }


    [Test]
    public async Task Created_Is_Set_To_Utc_On_Insert()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product { Title = "Product (test)" };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(item.Created, Is.EqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(5)));
        Assert.That(item.LastModified, Is.Null);
    }

    [Test]
    public async Task Client_Supplied_Local_Created_Is_Converted_To_Utc()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var localCreated = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1).ToLocalTime(), DateTimeKind.Local);
        var item = new Product
        {
            Title = "Product (test)",
            Created = localCreated
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(item.Created, Is.EqualTo(localCreated.ToUniversalTime()));
    }

    [Test]
    public async Task Client_Supplied_LastModified_Is_Normalized_To_Utc_On_Insert()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var localModified = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-2).ToLocalTime(), DateTimeKind.Local);
        var item = new Product
        {
            Title = "Product (test)",
            LastModified = localModified
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        Assert.That(item.LastModified, Is.Not.Null);
        Assert.That(item.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(item.LastModified, Is.EqualTo(localModified.ToUniversalTime()));
    }

    [Test]
    public async Task LastModified_Is_Set_To_Utc_On_Update()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product { Title = "Product (test)" };
        dbContext.Products.Add(item);
        await dbContext.SaveChangesAsync();

        var created = item.Created;
        item.Title = "Product (modified)";
        await dbContext.SaveChangesAsync();

        Assert.That(item.Created, Is.EqualTo(created));
        Assert.That(item.LastModified, Is.Not.Null);
        Assert.That(item.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(item.LastModified, Is.GreaterThanOrEqualTo(item.Created));
    }

    [Test]
    public async Task Timestamps_Materialize_With_Utc_Kind()
    {
        int id;
        DateTime created;
        DateTime? lastModified;
        using (var insertScope = _serviceProvider.CreateScope())
        {
            var dbContext = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Product { Title = "Product (test)" };
            dbContext.Products.Add(item);
            await dbContext.SaveChangesAsync();

            // update so the nullable LastModified column holds a value to round-trip as well
            item.Title = "Product (modified)";
            await dbContext.SaveChangesAsync();

            id = item.Id;
            created = item.Created;
            lastModified = item.LastModified;
        }

        using var readScope = _serviceProvider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ProductContext>();
        var fetched = await readContext.Products
            .AsNoTracking()
            .SingleAsync(x => x.Id == id);

        Assert.That(fetched.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(fetched.Created, Is.EqualTo(created));
        Assert.That(fetched.LastModified, Is.Not.Null);
        Assert.That(fetched.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(fetched.LastModified, Is.EqualTo(lastModified));
    }

    [Test]
    public async Task Materialized_Timestamps_Serialize_To_Json_With_Utc_Suffix()
    {
        using (var insertScope = _serviceProvider.CreateScope())
        {
            var dbContext = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.Products.Add(new Product { Title = "Product (test)" });
            await dbContext.SaveChangesAsync();
        }

        using var readScope = _serviceProvider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ProductContext>();
        var fetched = await readContext.Products.AsNoTracking().SingleAsync();

        var json = JsonSerializer.Serialize(fetched.Created);

        // ISO 8601 with Z suffix, so SPA clients interpret the value as UTC
        Assert.That(json, Does.EndWith("Z\""));
    }

    [Test]
    public async Task Created_Uses_Local_Time_When_Utc_Disabled_And_Survives_The_RoundTrip()
    {
        DateTimeDefaults.UseUtc = false;

        int id;
        DateTime created;
        using (var insertScope = _serviceProvider.CreateScope())
        {
            var dbContext = insertScope.ServiceProvider.GetRequiredService<ProductContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Product { Title = "Product (test)" };
            dbContext.Products.Add(item);

            await dbContext.SaveChangesAsync();

            Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Local));
            Assert.That(item.Created, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromSeconds(5)));

            id = item.Id;
            created = item.Created;
        }

        // ProductContext still applies SetUtcDateTimeConvention, but the converter follows the disabled
        // policy and passes values through — the stored value must not shift against the in-memory one
        using var readScope = _serviceProvider.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<ProductContext>();
        var fetched = await readContext.Products
            .AsNoTracking()
            .SingleAsync(x => x.Id == id);

        Assert.That(fetched.Created, Is.EqualTo(created)); // same wall-clock ticks
        Assert.That(fetched.Created.Kind, Is.Not.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public async Task Client_Supplied_Value_Is_Kept_As_Given_When_Utc_Disabled()
    {
        DateTimeDefaults.UseUtc = false;

        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var localCreated = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1).ToLocalTime(), DateTimeKind.Local);
        var item = new Product
        {
            Title = "Product (test)",
            Created = localCreated
        };
        dbContext.Products.Add(item);

        await dbContext.SaveChangesAsync();

        // no conversion: value and kind untouched
        Assert.That(item.Created, Is.EqualTo(localCreated));
        Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Local));

        item.Title = "Product (modified)";
        await dbContext.SaveChangesAsync();

        Assert.That(item.LastModified, Is.Not.Null);
        Assert.That(item.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Local));
    }

    [Test]
    public void UseUtc_Option_Sets_The_Ambient_Policy()
    {
        var options = new EntityServiceCollectionOptions(new ServiceCollection());

        options.UseUtc(false);
        Assert.That(DateTimeDefaults.UseUtc, Is.False);

        options.UseUtc();
        Assert.That(DateTimeDefaults.UseUtc, Is.True);
    }

    // UseDefaults() does not touch the ambient policy, so an explicit UseUtc(false) survives in any order.
    [Test]
    public void UseUtc_OptOut_Before_UseDefaults_Is_Respected()
    {
        var options = new EntityServiceCollectionOptions(new ServiceCollection());

        options.UseUtc(false).UseDefaults();

        Assert.That(DateTimeDefaults.UseUtc, Is.False);
    }

    [Test]
    public void UseUtc_OptOut_After_UseDefaults_Is_Respected()
    {
        var options = new EntityServiceCollectionOptions(new ServiceCollection());

        options.UseDefaults().UseUtc(false);

        Assert.That(DateTimeDefaults.UseUtc, Is.False);
    }

    [Test]
    public async Task FilterCreated_Normalizes_Local_Kind_Input()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Products.Add(new Product { Title = "Product (test)" });
        await dbContext.SaveChangesAsync();

        var localMinPast = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(-5).ToLocalTime(), DateTimeKind.Local);
        var matches = await dbContext.Products
            .FilterCreated(localMinPast, null)
            .ToListAsync();
        Assert.That(matches, Has.Count.EqualTo(1));

        var localMinFuture = DateTime.SpecifyKind(DateTime.UtcNow.AddMinutes(5).ToLocalTime(), DateTimeKind.Local);
        var noMatches = await dbContext.Products
            .FilterCreated(localMinFuture, null)
            .ToListAsync();
        Assert.That(noMatches, Is.Empty);
    }

    // The converter must not change the SQL shape: comparisons stay parameterized on the bare column
    // (conversion happens client-side on the parameter → indexes stay usable), and member access on a
    // converted property still translates to SQL instead of throwing or evaluating client-side.
    [Test]
    public async Task Converter_Keeps_Query_Sql_Shape_Index_Friendly()
    {
        var dbContext = _serviceProvider.GetRequiredService<ProductContext>();
        await dbContext.Database.EnsureCreatedAsync();
        dbContext.Products.Add(new Product { Title = "Product (test)" });
        await dbContext.SaveChangesAsync();

        var filterSql = dbContext.Products.FilterCreated(DateTime.UtcNow.AddDays(-1), null).ToQueryString();
        Assert.That(filterSql, Does.Contain("\"Created\" >="));
        Assert.That(filterSql, Does.Not.Contain("strftime"));

        var yearQuery = dbContext.Products.Where(x => x.Created.Year == DateTime.UtcNow.Year);
        Assert.That(yearQuery.ToQueryString(), Does.Contain("strftime"), "member access should translate to a SQL date function");
        Assert.That(await yearQuery.CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public void FilterCreated_Uses_Dates_As_Given_When_Utc_Disabled()
    {
        DateTimeDefaults.UseUtc = false;

        var created = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);
        var items = new[] { new Product { Title = "Product (test)", Created = created } }.AsQueryable();

        // no conversion: raw ticks are compared, so the same ticks with Local kind still match ...
        var sameTicksAsLocal = DateTime.SpecifyKind(created, DateTimeKind.Local);
        Assert.That(items.FilterCreated(sameTicksAsLocal, null).Count(), Is.EqualTo(1));

        // ... and later ticks exclude, regardless of kind
        var laterTicksAsLocal = DateTime.SpecifyKind(created.AddHours(1), DateTimeKind.Local);
        Assert.That(items.FilterCreated(laterTicksAsLocal, null), Is.Empty);
    }
}
