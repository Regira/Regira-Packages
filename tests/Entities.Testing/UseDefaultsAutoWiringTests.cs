using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Utilities;

namespace Entities.Testing;

/// <summary>
/// Covers the automatic DbContext wiring of <c>UseDefaults()</c>: primer/normalizer/auto-truncate
/// interceptors and the UTC date convention are contributed to the context's options by
/// <c>UseEntities&lt;TContext&gt;()</c>, so <c>AddDbContext</c> only needs the provider.
/// </summary>
[TestFixture]
public class UseDefaultsAutoWiringTests
{
    // deliberately NO ConfigureConventions and NO interceptor wiring — everything comes from UseDefaults()
    public class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProductTag> ProductTags { get; set; } = null!;
    }

    // the multi-provider shape: the entity layer registers against the abstract base,
    // while EF builds the concrete provider-specific context
    public abstract class BaseContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<Order> Orders { get; set; } = null!;
        public DbSet<Customer> Customers { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<ProductTag> ProductTags { get; set; } = null!;
    }
    public class DerivedContext(DbContextOptions<DerivedContext> options) : BaseContext(options);

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
        DateTimeDefaults.UseUtc = true;
        _connection.Close();
    }


    [Test]
    public async Task UseDefaults_Wires_Interceptors_And_Utc_Convention_Into_AddDbContext()
    {
        var services = new ServiceCollection();
        // plain overload — no service provider, no interceptor calls
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
        services.UseEntities<PlainContext>(e => e.UseDefaults());

        await using var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Product { Title = "Product (test) " + new string('x', 80) };
            dbContext.Products.Add(item);
            await dbContext.SaveChangesAsync();

            // primer interceptor ran: Created is set, as UTC
            Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue));
            Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
            // normalizer interceptor ran: [Normalized] title is filled
            Assert.That(item.NormalizedTitle, Is.Not.Null);
            // auto-truncate interceptor ran: Title clipped to its [MaxLength(64)]
            Assert.That(item.Title!.Length, Is.EqualTo(64));
        }

        // UTC convention applied: values materialize with DateTimeKind.Utc in a fresh scope
        using (var readScope = sp.CreateScope())
        {
            var readContext = readScope.ServiceProvider.GetRequiredService<PlainContext>();
            var fetched = await readContext.Products.AsNoTracking().SingleAsync();
            Assert.That(fetched.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
        }
    }

    // UseEntities<AbstractBase>() must wire the concrete context EF actually builds (e.g. one
    // provider-specific context per database, all deriving from a shared base)
    [Test]
    public async Task Wires_Concrete_Context_Registered_Through_An_Abstract_Base()
    {
        var services = new ServiceCollection();
        services.AddDbContext<DerivedContext>(db => db.UseSqlite(_connection));
        services.UseEntities<BaseContext>(e => e.UseDefaults());

        await using var sp = services.BuildServiceProvider();
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<DerivedContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Product { Title = "Product (test)" };
            dbContext.Products.Add(item);
            await dbContext.SaveChangesAsync();

            Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue)); // primer interceptor wired
            Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(item.NormalizedTitle, Is.Not.Null); // normalizer interceptor wired
        }

        using var readScope = sp.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<DerivedContext>();
        var fetched = await readContext.Products.AsNoTracking().SingleAsync();
        Assert.That(fetched.Created.Kind, Is.EqualTo(DateTimeKind.Utc)); // UTC convention wired
    }

    [Test]
    public async Task Wiring_Is_Independent_Of_Registration_Order()
    {
        var services = new ServiceCollection();
        services.UseEntities<PlainContext>(e => e.UseDefaults()); // UseEntities BEFORE AddDbContext
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product { Title = "Product (test)" };
        dbContext.Products.Add(item);
        await dbContext.SaveChangesAsync();

        Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue));
        Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    // Regression: a second, bare UseEntities<TContext>() (e.g. another module registering extra entities
    // without re-calling UseDefaults) must not silently disable the wiring selected by the first call.
    [Test]
    public async Task Bare_UseEntities_Does_Not_Disable_Earlier_Wiring()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
        services.UseEntities<PlainContext>(e => e.UseDefaults());
        services.UseEntities<PlainContext>(); // bare — wires nothing itself

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product { Title = "Product (test)" };
        dbContext.Products.Add(item);
        await dbContext.SaveChangesAsync();

        Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue)); // primers still run
        Assert.That(item.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public async Task Repeated_UseEntities_Does_Not_Double_The_Wiring()
    {
        var callCount = new int[1];

        var services = new ServiceCollection();
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
        // e.g. two modules each configuring entities for the same context
        services.UseEntities<PlainContext>(e => e.UseDefaults());
        services.UseEntities<PlainContext>(e => e.UseDefaults());
        services.AddPrimer<Product>(_ => new CountingProductPrimer(callCount));

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Products.Add(new Product { Title = "Product (test)" });
        await dbContext.SaveChangesAsync();

        Assert.That(callCount[0], Is.EqualTo(1)); // primer interceptor ran exactly once
    }

    [Test]
    public async Task WireDbContext_None_Skips_The_Automatic_Wiring()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
        services.UseEntities<PlainContext>(e =>
        {
            e.UseDefaults();
            e.WireDbContext(DbContextWiring.None); // opt out — the app wires the DbContext itself
        });

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var item = new Product { Title = "Product (test)" };
        dbContext.Products.Add(item);
        await dbContext.SaveChangesAsync();

        Assert.That(item.Created, Is.EqualTo(DateTime.MinValue)); // no primer interceptor → untouched
    }

    [Test]
    public void AddDefaultInterceptors_Selects_The_Full_Default_Wiring()
    {
        var options = new EntityServiceCollectionOptions(new ServiceCollection());

        options.AddDefaultInterceptors();

        Assert.That(options.DbContextWiring, Is.EqualTo(DbContextWiring.All));
    }

    // à la carte without UseDefaults(): only the requested piece is wired
    [Test]
    public async Task WireDbContext_Can_Wire_Selected_Pieces_Without_UseDefaults()
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
        services.UseEntities<PlainContext>(e =>
        {
            e.AddDefaultPrimers();
            e.WireDbContext(DbContextWiring.PrimerInterceptors);
        });

        await using var sp = services.BuildServiceProvider();
        int id;
        var longTitle = "Product (test) " + new string('x', 80);
        using (var scope = sp.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlainContext>();
            await dbContext.Database.EnsureCreatedAsync();

            var item = new Product { Title = longTitle };
            dbContext.Products.Add(item);
            await dbContext.SaveChangesAsync();
            id = item.Id;

            // primer interceptor wired: timestamps run
            Assert.That(item.Created, Is.Not.EqualTo(DateTime.MinValue));
            // normalizer interceptor NOT wired
            Assert.That(item.NormalizedTitle, Is.Null);
            // auto-truncate NOT wired: title kept as-is (SQLite does not enforce [MaxLength])
            Assert.That(item.Title, Is.EqualTo(longTitle));
        }

        // UTC convention NOT wired: values materialize with the provider default (Unspecified), not Utc
        using var readScope = sp.CreateScope();
        var readContext = readScope.ServiceProvider.GetRequiredService<PlainContext>();
        var fetched = await readContext.Products.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.That(fetched.Created.Kind, Is.EqualTo(DateTimeKind.Unspecified));
    }

    private sealed class CountingProductPrimer(int[] callCount) : EntityPrimerBase<Product>
    {
        public override Task PrepareAsync(Product entity, EntityEntry _, CancellationToken token = default)
        {
            callCount[0]++;
            return Task.CompletedTask;
        }
    }
}
