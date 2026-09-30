using System.ComponentModel.DataAnnotations;
using FluentValidation;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validation.FluentValidation;

namespace Entities.Testing;

/// <summary>
/// <c>UseFluentValidation()</c> runs FluentValidation validators in the write pipeline under the same scope rule as an
/// <c>IEntityValidator</c>: an <c>AbstractValidator&lt;T&gt;</c> checks every entity that is, derives from or implements
/// <c>T</c>. Each write runs its own rule set, and only errors reject it.
/// </summary>
[TestFixture]
public class FluentEntityValidatorTests
{
    public interface IHasTenantId
    {
        string? TenantId { get; set; }
    }

    public enum OrderStatus { Pending, Shipped, Delivered }

    public class Customer : IEntityWithSerial
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Name { get; set; }
    }

    public class Order : IEntityWithSerial, IHasTenantId
    {
        public int Id { get; set; }
        [MaxLength(40)] public string? Code { get; set; }
        [MaxLength(20)] public string? TenantId { get; set; }
        public int CustomerId { get; set; }
        public OrderStatus Status { get; set; }
        [MaxLength(64)] public string? Note { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    public class OrderLine : IEntityWithSerial
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        [MaxLength(64)] public string? Product { get; set; }
        public int Quantity { get; set; }
    }

    public abstract class Party : IEntityWithSerial, IHasTenantId
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Name { get; set; }
        [MaxLength(20)] public string? TenantId { get; set; }
    }
    public class Person : Party
    {
        [MaxLength(64)] public string? GivenName { get; set; }
    }
    public class Organization : Party;

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<Party> Parties => Set<Party>();
        public DbSet<Person> Persons => Set<Person>();
        public DbSet<Organization> Organizations => Set<Organization>();
    }

    public static class OrderStatusRules
    {
        public static bool CanMove(OrderStatus from, OrderStatus to) => to >= from;
    }

    public class OrderValidator : AbstractValidator<Order>
    {
        public OrderValidator(ShopContext db)
        {
            RuleFor(x => x.Code).NotEmpty().MaximumLength(20);

            RuleFor(x => x.CustomerId)
                .MustAsync((id, ct) => db.Customers.AnyAsync(c => c.Id == id, ct))
                .WithMessage(x => $"Customer {x.CustomerId} does not exist");

            // a Related() collection is validated through its parent
            RuleForEach(x => x.Lines).ChildRules(line => line.RuleFor(l => l.Quantity).GreaterThan(0));

            RuleFor(x => x.Status)
                .Must((_, status, ctx) => ctx.GetOriginal() is not { } stored || OrderStatusRules.CanMove(stored.Status, status))
                .WithMessage("Status change not allowed");

            RuleFor(x => x.Note).MaximumLength(10).WithSeverity(Severity.Warning);

            RuleSet(EntityRuleSets.Add, () =>
                RuleFor(x => x.Status).Equal(OrderStatus.Pending).WithMessage("A new order is pending"));
            RuleSet(EntityRuleSets.Modify, () =>
                RuleFor(x => x.Code).Must((_, code, ctx) => ctx.GetOriginal()?.Code == code).WithMessage("The code cannot change"));
            RuleSet(EntityRuleSets.Remove, () =>
                RuleFor(x => x.Status).NotEqual(OrderStatus.Shipped).WithMessage("A shipped order cannot be deleted"));
        }
    }

    public class TenantValidator : AbstractValidator<IHasTenantId>
    {
        public TenantValidator() => RuleFor(x => x.TenantId).NotEmpty();
    }

    public class PartyValidator : AbstractValidator<Party>
    {
        public PartyValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator() => RuleFor(x => x.GivenName).NotEmpty();
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    [TearDown]
    public void TearDown()
    {
        _sp?.Dispose();
        _connection.Close();
    }

    private void Build(Action<EntityServiceCollectionOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o =>
            {
                o.UseDefaults();
                o.UseFluentValidation(typeof(OrderValidator).Assembly);
                options?.Invoke(o);
            })
            .For<Order>(e => e.Related(x => x.Lines))
            .For<Party>()
            .For<Customer>();
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Database.EnsureCreated();
        db.Customers.Add(new Customer { Id = 1, Name = "Ada" });
        db.SaveChanges();
    }

    private async Task<int> SeedOrder(OrderStatus status, int customerId = 1)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = new Order { Code = "ORD-1", TenantId = "acme", CustomerId = customerId, Status = status };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static Order NewOrder(OrderStatus status = OrderStatus.Pending)
        => new() { Code = "ORD-1", TenantId = "acme", CustomerId = 1, Status = status };

    private static IEnumerable<string> Keys(EntityInputException ex) => ex.Errors.Select(e => e.Key);

    [Test]
    public async Task An_Async_Rule_Queries_The_DbContext()
    {
        Build();
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var unknown = NewOrder();
        unknown.CustomerId = 42;

        await service.Add(NewOrder());
        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(unknown)))!;

        Assert.That(ex.Errors, Is.EqualTo(new[] { new EntityInputError("CustomerId", "Customer 42 does not exist") }));
    }

    [Test]
    public async Task GetOriginal_Holds_The_Stored_Row_On_Modify()
    {
        Build();
        var id = await SeedOrder(OrderStatus.Delivered);
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var back = NewOrder(OrderStatus.Pending);
        back.Id = id;

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Modify(back)))!;

        Assert.That(ex.Errors, Is.EqualTo(new[] { new EntityInputError("Status", "Status change not allowed") }));
    }

    [Test]
    public async Task Add_And_Modify_Each_Run_Their_Own_Rule_Set()
    {
        Build();
        var id = await SeedOrder(OrderStatus.Pending);
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var shippedOnAdd = NewOrder(OrderStatus.Shipped);
        var shippedOnModify = NewOrder(OrderStatus.Shipped);
        shippedOnModify.Id = id;
        var recoded = NewOrder();
        recoded.Id = id;
        recoded.Code = "ORD-2";

        var onAdd = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(shippedOnAdd)))!;
        await service.Modify(shippedOnModify);
        var onModify = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Modify(recoded)))!;

        Assert.Multiple(() =>
        {
            Assert.That(onAdd.Errors, Is.EqualTo(new[] { new EntityInputError("Status", "A new order is pending") }));
            Assert.That(onModify.Errors, Is.EqualTo(new[] { new EntityInputError("Code", "The code cannot change") }));
        });
    }

    [Test]
    public async Task Remove_Runs_The_Remove_Rule_Set_Alone()
    {
        Build();
        // the stored customer is gone: the shape rule and its lookup would fail, but a delete does not run them
        var pending = await SeedOrder(OrderStatus.Pending, customerId: 99);
        var shipped = await SeedOrder(OrderStatus.Shipped, customerId: 99);
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

        await service.Remove((await service.Details(pending))!);
        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(async () => await service.Remove((await service.Details(shipped))!)))!;

        Assert.That(ex.Errors, Is.EqualTo(new[] { new EntityInputError("Status", "A shipped order cannot be deleted") }));
    }

    [Test]
    public async Task Validators_On_An_Interface_And_A_Base_Class_Run_For_Every_Entity_In_Their_Scope()
    {
        Build();
        using var scope = _sp.CreateScope();
        var parties = scope.ServiceProvider.GetRequiredService<IEntityService<Party>>();
        var orders = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var untenanted = NewOrder();
        untenanted.TenantId = null;

        var person = (await Assert.ThrowsAsync<EntityInputException<Party>>(() => parties.Add(new Person())))!;
        var organization = (await Assert.ThrowsAsync<EntityInputException<Party>>(() => parties.Add(new Organization { TenantId = "acme" })))!;
        var order = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => orders.Add(untenanted)))!;

        Assert.Multiple(() =>
        {
            Assert.That(Keys(person), Is.EquivalentTo(new[] { "GivenName", "Name", "TenantId" }));
            Assert.That(Keys(organization), Is.EqualTo(new[] { "Name" }));
            Assert.That(Keys(order), Is.EqualTo(new[] { "TenantId" }));
        });
    }

    [Test]
    public async Task A_Warning_Does_Not_Block_The_Save()
    {
        Build();
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder();
        order.Note = "longer than ten characters";

        await service.Add(order);

        Assert.That(await service.SaveChanges(), Is.EqualTo(1));
    }

    [Test]
    public async Task A_Child_Rule_Reports_The_Indexed_Property_Path()
    {
        Build();
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder();
        order.Lines = [new OrderLine { Product = "Book", Quantity = 1 }, new OrderLine { Product = "Pen", Quantity = 0 }];

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(order)))!;

        Assert.That(Keys(ex), Is.EqualTo(new[] { "Lines[1].Quantity" }));
    }

    [Test]
    public async Task An_Entity_Without_A_Validator_Is_Left_Alone()
    {
        Build();
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Customer>>();

        await service.Add(new Customer());

        Assert.That(await service.SaveChanges(), Is.EqualTo(1));
    }

    [Test]
    public async Task A_Write_Of_An_Entity_Without_Rules_Does_Not_Scan_The_Tracker()
    {
        Build();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var scans = 0;
        db.ChangeTracker.DetectingAllChanges += (_, _) => scans++;

        await scope.ServiceProvider.GetRequiredService<IEntityService<Customer>>().Add(new Customer { Name = "Grace" });
        var customerScans = scans;
        await scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder());

        Assert.Multiple(() =>
        {
            // FluentEntityValidator is scoped to every entity, but covers only those an AbstractValidator applies to
            Assert.That(customerScans, Is.Zero);
            Assert.That(scans, Is.GreaterThan(customerScans));
        });
    }

    [Test]
    public async Task Calling_UseFluentValidation_Twice_Reports_Each_Error_Once()
    {
        Build(o => o.UseFluentValidation(typeof(OrderValidator).Assembly));
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder();
        order.Code = null;

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(order)))!;

        Assert.That(Keys(ex), Is.EqualTo(new[] { "Code" }));
    }

    public class LegacyOrderWriteService(ShopContext dbContext, IEntityReadService<Order, int> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ShopContext, Order>(dbContext, readService, preppers, loggerFactory);
    public class LegacyCustomerWriteService(ShopContext dbContext, IEntityReadService<Customer, int> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ShopContext, Customer>(dbContext, readService, preppers, loggerFactory);

    [Test]
    public async Task Startup_Reports_A_Write_Service_Without_Validators_Only_For_An_Entity_With_Rules()
    {
        var capture = new StartupValidationTests.CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.UseFluentValidation(typeof(OrderValidator).Assembly);
            })
            .For<Order>(e => e.UseWriteService<LegacyOrderWriteService>())
            .For<Customer>(e => e.UseWriteService<LegacyCustomerWriteService>());

        await using var sp = services.BuildServiceProvider();
        await StartupValidationTests.RunHostedServices(sp);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Warnings, Has.Some.Contains("'LegacyOrderWriteService'"));
            // FluentEntityValidator is scoped to every entity, but no AbstractValidator applies to a Customer
            Assert.That(capture.Warnings, Has.None.Contains("'LegacyCustomerWriteService'"));
        });
    }
}
