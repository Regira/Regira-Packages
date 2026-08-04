using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

[TestFixture]
public class ConstraintExceptionTests
{
    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>()
            .For<Category>();
        _serviceProvider = services.BuildServiceProvider();
    }
    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
        _connection.Close();
    }

    [Test]
    public async Task SaveChanges_Wraps_Constraint_Violation_In_EntityConstraintException()
    {
        using var scope = _serviceProvider.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ProductContext>().Database.EnsureCreatedAsync();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Category, int>>();

        await service.Add(new Category { Id = 1, Title = "First" });
        await service.SaveChanges();

        // explicit duplicate PK → SQLite UNIQUE violation → DbUpdateException wrapped by the write service
        await service.Add(new Category { Id = 1, Title = "Duplicate" });
        var ex = Assert.ThrowsAsync<EntityConstraintException>(() => service.SaveChanges());
        Assert.That(ex!.InnerException, Is.InstanceOf<DbUpdateException>());
    }

    [TestCase(19, true)]  // SQLITE_CONSTRAINT
    [TestCase(5, false)]  // SQLITE_BUSY — transient, must keep surfacing unwrapped
    [TestCase(1, false)]  // SQLITE_ERROR
    public void IsConstraintViolation_Classifies_Sqlite_Error_Codes(int errorCode, bool expected)
    {
        var ex = new DbUpdateException("save failed", new SqliteException("db error", errorCode));
        Assert.That(ex.IsConstraintViolation(), Is.EqualTo(expected));
    }

    [Test]
    public void IsConstraintViolation_Is_False_For_Concurrency_And_Non_Db_Inner_Exceptions()
    {
        Assert.That(new DbUpdateConcurrencyException("stale row").IsConstraintViolation(), Is.False);
        Assert.That(new DbUpdateException("save failed", new InvalidOperationException()).IsConstraintViolation(), Is.False);
        Assert.That(new DbUpdateException("save failed").IsConstraintViolation(), Is.False);
    }

    [Test]
    public void IsConstraintViolation_Walks_Nested_Inner_Exceptions()
    {
        // interceptors/retry strategies may wrap the provider exception one level deeper
        var nested = new DbUpdateException("outer", new DbUpdateException("wrapped", new SqliteException("db error", 19)));
        Assert.That(nested.IsConstraintViolation(), Is.True);
    }

    [Test]
    public void Wrapped_Exception_Message_Is_The_Generic_Client_Text()
    {
        // Message must be safe to render anywhere — the provider's constraint text stays on InnerException
        using var scope = _serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ProductContext>().Database.EnsureCreated();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Category, int>>();

        service.Add(new Category { Id = 1, Title = "First" }).GetAwaiter().GetResult();
        service.SaveChanges().GetAwaiter().GetResult();
        service.Add(new Category { Id = 1, Title = "Duplicate" }).GetAwaiter().GetResult();

        var ex = Assert.ThrowsAsync<EntityConstraintException>(() => service.SaveChanges());
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Is.EqualTo(EntityConstraintException.ClientMessage));
            Assert.That(ex.InnerException?.InnerException?.Message, Does.Contain("UNIQUE"));
        });
    }
}
