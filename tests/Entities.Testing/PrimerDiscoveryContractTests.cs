using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Entities.Testing;

// Findings 6+7 (review 2026-07-12): the container path (ApplyPrimers — seeding) and the interceptor
// path (SaveChanges — production) must discover the SAME primers. These three contracts pin the two
// bugs: type-keyed dedupe collapsing distinct e.Prime lambdas, and typed-only registrations running on
// only one path.
[TestFixture]
public class PrimerDiscoveryContractTests
{
    private class TypedMarkerPrimer : EntityPrimerBase<Product>
    {
        public override Task PrepareAsync(Product entity, EntityEntry entry, CancellationToken token = default)
        {
            entity.Description += "|typed";
            return Task.CompletedTask;
        }
    }

    private class OpenGenericPrimer<TEntity> : EntityPrimerBase<TEntity>
        where TEntity : Product
    {
        public override Task PrepareAsync(TEntity entity, EntityEntry entry, CancellationToken token = default)
        {
            entity.Description += "|open";
            return Task.CompletedTask;
        }
    }

    // Registered for manual resolution (GetRequiredService<SelfRegisteredPrimer>()), NOT under the
    // IEntityPrimer interface — so it must NOT run automatically on SaveChanges.
    private class SelfRegisteredPrimer : EntityPrimerBase<Product>
    {
        public override Task PrepareAsync(Product entity, EntityEntry entry, CancellationToken token = default)
        {
            entity.Description += "|self";
            return Task.CompletedTask;
        }
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

    private ServiceProvider BuildProvider(Action<IServiceCollection> registerPrimers)
    {
        var services = new ServiceCollection();
        // UseEntities() registers the IServiceCollection; bare test setups must do it themselves
        services.AddSingleton<IServiceCollection>(services);
        services.AddDbContext<ProductContext>((sp, db) => db.UseSqlite(_connection).AddInterceptors(new EntityPrimerContainerInterceptor(sp)));
        services.RegisterPrimerContainer<ProductContext>();
        registerPrimers(services);
        return services.BuildServiceProvider();
    }

    private static async Task<string?> RunContainerPath(ServiceProvider sp)
    {
        var db = sp.GetRequiredService<ProductContext>();
        await db.Database.EnsureCreatedAsync();
        var product = new Product { Title = "container", Description = "" };
        db.Products.Add(product);
        await sp.GetRequiredService<EntityPrimerContainer>().ApplyPrimers();
        return product.Description;
    }

    private static async Task<string?> RunInterceptorPath(ServiceProvider sp)
    {
        var db = sp.GetRequiredService<ProductContext>();
        await db.Database.EnsureCreatedAsync();
        var product = new Product { Title = "interceptor", Description = "" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Description;
    }

    private static void AddTwoLambdaPrimers(IServiceCollection services)
    {
        // exactly what e.Prime(...) registers: one factory descriptor per lambda, both wrapping the
        // same closed EntityPrimer<Product> runtime type
        services.AddPrimer(_ => new EntityPrimer<Product>((item, _) =>
        {
            item.Description += "|first";
            return Task.CompletedTask;
        }));
        services.AddPrimer(_ => new EntityPrimer<Product>((item, _) =>
        {
            item.Description += "|second";
            return Task.CompletedTask;
        }));
    }

    [Test]
    public async Task Two_Prime_Lambdas_Both_Run_On_The_Container_Path()
    {
        await using var sp = BuildProvider(AddTwoLambdaPrimers);
        var description = await RunContainerPath(sp);
        Assert.That(description, Does.Contain("|first").And.Contain("|second"));
    }

    [Test]
    public async Task Two_Prime_Lambdas_Both_Run_On_The_Interceptor_Path()
    {
        await using var sp = BuildProvider(AddTwoLambdaPrimers);
        var description = await RunInterceptorPath(sp);
        Assert.That(description, Does.Contain("|first").And.Contain("|second"));
    }

    [Test]
    public async Task Dual_Registered_Class_Primer_Runs_Once_On_The_Container_Path()
    {
        await using var sp = BuildProvider(s => s.AddPrimer<Product, TypedMarkerPrimer>());
        var description = await RunContainerPath(sp);
        Assert.That(description, Is.EqualTo("|typed"), "the typed + untyped dual registration must run exactly once");
    }

    [Test]
    public async Task Dual_Registered_Class_Primer_Runs_Once_On_The_Interceptor_Path()
    {
        await using var sp = BuildProvider(s => s.AddPrimer<Product, TypedMarkerPrimer>());
        var description = await RunInterceptorPath(sp);
        Assert.That(description, Is.EqualTo("|typed"), "the typed + untyped dual registration must run exactly once");
    }

    [Test]
    public async Task Typed_Only_Registration_Runs_On_The_Container_Path()
    {
        await using var sp = BuildProvider(s => s.AddTransient<IEntityPrimer<Product>, TypedMarkerPrimer>());
        var description = await RunContainerPath(sp);
        Assert.That(description, Is.EqualTo("|typed"));
    }

    [Test]
    public async Task Typed_Only_Registration_Runs_On_The_Interceptor_Path()
    {
        await using var sp = BuildProvider(s => s.AddTransient<IEntityPrimer<Product>, TypedMarkerPrimer>());
        var description = await RunInterceptorPath(sp);
        Assert.That(description, Is.EqualTo("|typed"));
    }

    // Finding 4a: an open-generic IEntityPrimer<> registration adds instances to GetServices(closed),
    // which used to misalign the positional pairing so the closed primer resolved the wrong instance.
    [Test]
    public async Task Closed_Primer_Still_Runs_When_An_OpenGeneric_Registration_Coexists()
    {
        await using var sp = BuildProvider(s =>
        {
            s.AddTransient(typeof(IEntityPrimer<>), typeof(OpenGenericPrimer<>)); // registered FIRST
            s.AddTransient<IEntityPrimer<Product>, TypedMarkerPrimer>();
        });
        var description = await RunInterceptorPath(sp);
        Assert.That(description, Does.Contain("|typed"),
            "the closed primer must run regardless of an open-generic registration contributing extra instances");
    }

    // Finding 4b: a primer registered as its concrete type (for manual GetRequiredService) is not
    // registered "as an IEntityPrimer" and must not auto-run on SaveChanges.
    [Test]
    public async Task Concrete_Self_Registration_Does_Not_Run_On_Save()
    {
        await using var sp = BuildProvider(s => s.AddTransient<SelfRegisteredPrimer>());
        var description = await RunInterceptorPath(sp);
        Assert.That(description, Does.Not.Contain("|self"),
            "a concrete self-registration is for manual resolution only — it must not prime on SaveChanges");
    }
}
