using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Regira.DAL.EFcore.Extensions;

namespace Entities.Testing;

/// <summary>
/// Covers <c>AddUtcDateTimeConvention()</c> — the <c>DbContextOptionsBuilder</c> wiring of the UTC
/// convention (counterpart of <c>SetUtcDateTimeConvention</c> in <c>ConfigureConventions</c>,
/// which <see cref="ProductContext"/> covers).
/// </summary>
[TestFixture]
public class UtcDateTimeConventionTests
{
    // deliberately NO ConfigureConventions override — the convention comes in via the options builder
    public class OptionsWiredContext(DbContextOptions<OptionsWiredContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProductTag> ProductTags { get; set; } = null!;
    }

    // explicit per-property conversion on LastModified — must win over the convention (opt-out)
    public class OptOutContext(DbContextOptions<OptOutContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProductTag> ProductTags { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Product>()
                .Property(x => x.LastModified)
                .HasConversion(new ValueConverter<DateTime, DateTime>(v => v, v => v));
    }

    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    [TearDown]
    public void TearDown() => _connection.Close();

    private TContext Create<TContext>() where TContext : DbContext
        => (TContext)Activator.CreateInstance(typeof(TContext), new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .AddUtcDateTimeConvention()
            .Options)!;

    [Test]
    public async Task OptionsBuilder_Wiring_Applies_The_Utc_RoundTrip()
    {
        var utcInstant = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);

        await using (var db = Create<OptionsWiredContext>())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.AddRange(
                new Product { Id = 1, Title = "utc-kind", Created = utcInstant },
                // local kind: the converter's write side must normalize this to the same instant
                new Product { Id = 2, Title = "local-kind", Created = utcInstant.ToLocalTime() });
            await db.SaveChangesAsync();
        }

        await using var read = Create<OptionsWiredContext>();
        var fetched1 = await read.Products.AsNoTracking().SingleAsync(x => x.Id == 1);
        var fetched2 = await read.Products.AsNoTracking().SingleAsync(x => x.Id == 2);

        Assert.That(fetched1.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(fetched1.Created, Is.EqualTo(utcInstant));
        Assert.That(fetched2.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        Assert.That(fetched2.Created, Is.EqualTo(utcInstant));
    }

    [Test]
    public async Task Explicit_Property_Conversion_Wins_Over_The_Convention()
    {
        var utcInstant = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);

        await using (var db = Create<OptOutContext>())
        {
            await db.Database.EnsureCreatedAsync();
            db.Products.Add(new Product { Id = 1, Title = "P", Created = utcInstant, LastModified = utcInstant });
            await db.SaveChangesAsync();
        }

        await using var read = Create<OptOutContext>();
        var fetched = await read.Products.AsNoTracking().SingleAsync(x => x.Id == 1);

        // Created: handled by the convention
        Assert.That(fetched.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        // LastModified: identity converter opted out — no Utc kind stamped on read
        Assert.That(fetched.LastModified!.Value.Kind, Is.EqualTo(DateTimeKind.Unspecified));
        Assert.That(fetched.LastModified, Is.EqualTo(utcInstant)); // ticks untouched either way
    }
}
