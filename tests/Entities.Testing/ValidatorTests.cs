using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators;
using Regira.Entities.Validators.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// Validators run inside <c>EntityWriteService</c> after every prepper and on <c>Remove</c>, collect their errors into one
/// <c>EntityInputException</c> of the service's own entity, and apply to an item by its runtime type: a validator scoped to
/// the entity, a base class or an interface runs wherever an entity in that scope is written.
/// </summary>
[TestFixture]
public class ValidatorTests
{
    public interface IHasTenantId
    {
        string? TenantId { get; set; }
    }

    public enum OrderStatus { Pending, Shipped }

    public class Order : IEntityWithSerial, IHasCode, IHasTenantId, IArchivable
    {
        public int Id { get; set; }
        [MaxLength(20)] public string? Code { get; set; }
        [MaxLength(20)] public string? TenantId { get; set; }
        public int? CustomerId { get; set; }
        public OrderStatus Status { get; set; }
        public int Total { get; set; }
        public bool IsArchived { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    public class OrderLine : IEntityWithSerial
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        [MaxLength(64)] public string? Product { get; set; }
        public int Quantity { get; set; }
    }

    public class Customer : IEntityWithSerial
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Name { get; set; }
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
    public class Organization : Party
    {
        [MaxLength(20)] public string? VatNumber { get; set; }
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Party> Parties => Set<Party>();
        public DbSet<Person> Persons => Set<Person>();
        public DbSet<Organization> Organizations => Set<Organization>();
    }

    /// <summary>Every validator call, in order, so a test can tell which ran and how often.</summary>
    public sealed class ValidationLog
    {
        public List<string> Calls { get; } = [];
    }

    public class TenantValidator(ValidationLog log) : EntityValidatorBase<IHasTenantId>
    {
        public override Task Validate(IEntityValidatorContext<IHasTenantId> context, CancellationToken token = default)
        {
            log.Calls.Add($"tenant:{context.Item.GetType().Name}");
            if (string.IsNullOrEmpty(context.Item.TenantId))
            {
                context.AddError(nameof(IHasTenantId.TenantId), "A tenant is required.");
            }
            return Task.CompletedTask;
        }
    }

    public class PartyValidator(ValidationLog log) : EntityValidatorBase<Party>
    {
        public override Task Validate(IEntityValidatorContext<Party> context, CancellationToken token = default)
        {
            log.Calls.Add($"party:{context.Item.GetType().Name}");
            if (string.IsNullOrEmpty(context.Item.Name))
            {
                context.AddError(nameof(Party.Name), "A name is required.");
            }
            return Task.CompletedTask;
        }
    }

    public class PersonValidator(ValidationLog log) : EntityValidatorBase<Person>
    {
        public override Task Validate(IEntityValidatorContext<Person> context, CancellationToken token = default)
        {
            log.Calls.Add($"person:{context.Item.GetType().Name}");
            if (string.IsNullOrEmpty(context.Item.GivenName))
            {
                context.AddError(nameof(Person.GivenName), "A given name is required.");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>Checks shipped orders only — every other order is skipped before <see cref="Validate"/> runs.</summary>
    public class ShippedOrderValidator(ValidationLog log) : EntityValidatorBase<Order>
    {
        public override bool CanValidate(Order item) => item.Status == OrderStatus.Shipped;
        public override Task Validate(IEntityValidatorContext<Order> context, CancellationToken token = default)
        {
            log.Calls.Add("shipped");
            if (context.Item.Lines is not { Count: > 0 })
            {
                context.AddError(nameof(Order.Lines), "A shipped order has lines.");
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>A write service that still calls the constructor without validators.</summary>
    public class LegacyOrderWriteService(ShopContext dbContext, IEntityReadService<Order, int> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ShopContext, Order>(dbContext, readService, preppers, loggerFactory);

    /// <summary>A write service for the <c>Party</c> hierarchy that still calls the constructor without validators.</summary>
    public class LegacyPartyWriteService(ShopContext dbContext, IEntityReadService<Party, int> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ShopContext, Party>(dbContext, readService, preppers, loggerFactory);

    /// <summary>Implements the typed interface directly, without <see cref="EntityValidatorBase{TScope}"/>.</summary>
    public class DirectOrderValidator : IEntityValidator<Order>
    {
        public Task Validate(IEntityValidatorContext<Order> context, CancellationToken token = default)
        {
            if (string.IsNullOrEmpty(context.Item.Code))
            {
                context.AddError(nameof(Order.Code), "A code is required.");
            }
            return Task.CompletedTask;
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private ValidationLog _log = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _log = new ValidationLog();
    }

    [TearDown]
    public void TearDown()
    {
        _sp?.Dispose();
        _connection.Close();
    }

    private void Build(Action<EntityServiceCollection<ShopContext>> configure, Action<EntityServiceCollectionOptions>? options = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(_log);
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        configure(services.UseEntities<ShopContext>(o =>
        {
            o.UseDefaults();
            options?.Invoke(o);
        }));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreated();
    }

    private async Task<int> SeedOrder(OrderStatus status = OrderStatus.Pending)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = new Order { Code = "ORD-1", TenantId = "acme", Status = status, Lines = [new OrderLine { Product = "Book", Quantity = 1 }] };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private static Order NewOrder(string? code = "ORD-1", string? tenant = "acme")
        => new() { Code = code, TenantId = tenant };

    // ── position in the pipeline ────────────────────────────────────────────────

    [Test]
    public async Task A_Validator_Sees_What_Every_Prepper_Computed_Whatever_The_Registration_Order()
    {
        var seen = new List<string>();
        void Record(string who, Order order) => seen.Add($"{who}:{order.Code}:{order.Total}:{order.Lines!.Single().Product}");

        // the global validator is registered before the global prepper, the entity validator before the entity's own
        // prepper and its Related() sync — and both run after all of them
        Build(s => s.For<Order>(e => e
                .Validate(ctx => Record("entity", ctx.Item))
                .Related(x => x.Lines, related => related.Prepare(line => line.Product = line.Product?.Trim()))
                .Prepare(x => x.Total = x.Lines?.Sum(l => l.Quantity) ?? 0)),
            o =>
            {
                o.AddValidator<Order>(ctx => Record("global", ctx.Item));
                o.AddPrepper<IHasCode>(x => x.Code = x.Code?.ToUpperInvariant());
            });

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder("ord-7");
        order.Lines = [new OrderLine { Product = "  Book  ", Quantity = 3 }];
        await service.Add(order);

        Assert.That(seen, Is.EqualTo(new[] { "global:ORD-7:3:Book", "entity:ORD-7:3:Book" }));
    }

    [Test]
    public async Task The_Errors_Of_Every_Validator_Arrive_In_One_Exception()
    {
        Build(s => s.For<Order>(e => e
            .Validate(ctx => ctx.AddError(nameof(Order.Code), "Code is taken."))
            .Validate(ctx =>
            {
                ctx.AddError(nameof(Order.Code), "Code must start with ORD-.");
                ctx.AddError(string.Empty, "The order is incomplete.");
            })));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder();

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(order)))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Item, Is.SameAs(order));
            Assert.That(ex.Errors, Is.EqualTo(new[]
            {
                new EntityInputError("Code", "Code is taken."),
                new EntityInputError("Code", "Code must start with ORD-."),
                new EntityInputError("", "The order is incomplete.")
            }));
            Assert.That(ex.InputErrors["Code"], Is.EqualTo("Code is taken. Code must start with ORD-."));
            Assert.That(ex.InputErrors[""], Is.EqualTo("The order is incomplete."));
        });
    }

    [Test]
    public async Task Every_Validator_Runs_And_Sees_The_Errors_Before_It()
    {
        IReadOnlyList<EntityInputError>? seenByLater = null;
        Build(s => s.For<Order>(e => e
            .Validate(ctx => ctx.AddError(nameof(Order.CustomerId), "A customer is required."))
            .Validate(ctx => seenByLater = ctx.Errors.ToArray())));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(NewOrder()));
        Assert.That(seenByLater, Is.EqualTo(new[] { new EntityInputError("CustomerId", "A customer is required.") }));
    }

    // ── scope ───────────────────────────────────────────────────────────────────

    [Test]
    public async Task A_Validator_Scoped_To_An_Entity_Runs_For_That_Entity_Only()
    {
        Build(s => s
            .For<Order>(e => e.Validate(ctx => _log.Calls.Add($"order:{ctx.Item.Code}")))
            .For<Customer>());

        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IEntityService<Customer>>().Add(new Customer { Name = "Ada" });
        await scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder());

        Assert.That(_log.Calls, Is.EqualTo(new[] { "order:ORD-1" }));
    }

    [Test]
    public async Task A_Validator_Scoped_To_A_Base_Class_Runs_For_A_Derived_Entity_Next_To_Its_Own()
    {
        Build(s => s.For<Party>(e => e.AddValidator<PartyValidator>()),
            o => o.AddValidator<PersonValidator>());

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Party>>();

        var person = (await Assert.ThrowsAsync<EntityInputException<Party>>(() => service.Add(new Person { TenantId = "acme" })))!;
        await service.Add(new Organization { Name = "Acme", TenantId = "acme" });

        Assert.Multiple(() =>
        {
            // the Person rules run although the Person is saved through the Party service
            Assert.That(person.Errors.Select(e => e.Key), Is.EquivalentTo(new[] { "Name", "GivenName" }));
            Assert.That(_log.Calls, Is.EqualTo(new[] { "person:Person", "party:Person", "party:Organization" }));
        });
    }

    [Test]
    public async Task A_Validator_Scoped_To_An_Interface_Runs_For_Every_Entity_Implementing_It()
    {
        // registered inside For<Order>(), yet the place of registration never narrows the scope
        Build(s => s
            .For<Order>(e => e.AddValidator<TenantValidator>())
            .For<Party>()
            .For<Customer>());

        using var scope = _sp.CreateScope();
        var parties = scope.ServiceProvider.GetRequiredService<IEntityService<Party>>();
        await scope.ServiceProvider.GetRequiredService<IEntityService<Customer>>().Add(new Customer { Name = "Ada" });

        var ex = (await Assert.ThrowsAsync<EntityInputException<Party>>(() => parties.Add(new Organization { Name = "Acme" })))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors.Single().Key, Is.EqualTo("TenantId"));
            Assert.That(_log.Calls, Is.EqualTo(new[] { "tenant:Organization" }), "Customer is not tenant-owned");
        });
    }

    [Test]
    public async Task A_Validator_Class_Registered_Twice_Runs_Once_While_Two_Delegates_Both_Run()
    {
        Build(s => s
            .For<Order>(e => e
                .AddValidator<TenantValidator>()
                .Validate(ctx => _log.Calls.Add("first"))
                .Validate(ctx => _log.Calls.Add("second")))
            .For<Party>(e => e.AddValidator<TenantValidator>()));

        using var scope = _sp.CreateScope();
        var ex = await Assert.ThrowsAsync<EntityInputException<Order>>(() =>
            scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder(tenant: null)))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors, Has.Count.EqualTo(1));
            Assert.That(_log.Calls, Is.EqualTo(new[] { "tenant:Order", "first", "second" }));
        });
    }

    [Test]
    public async Task CanValidate_Skips_The_Items_It_Declines()
    {
        Build(s => s.For<Order>(e => e.AddValidator<ShippedOrderValidator>()));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        await service.Add(NewOrder());
        var shipped = NewOrder();
        shipped.Status = OrderStatus.Shipped;

        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(shipped));
        Assert.That(_log.Calls, Is.EqualTo(new[] { "shipped" }));
    }

    [Test]
    public async Task A_Global_Validator_Throws_For_The_Entity_Being_Saved_Not_For_Its_Scope()
    {
        Build(s => s.For<Order>(), o => o.AddValidator<TenantValidator>());

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

        // the generated endpoints catch EntityInputException<TEntity> of their own entity
        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(NewOrder(tenant: null)));
    }

    [Test]
    public async Task A_Custom_Service_Running_The_Extension_Gets_The_Same_Errors()
    {
        Build(s => s.For<Order>(e => e.Validate(ctx => ctx.AddError(nameof(Order.Code), "Code is taken."))),
            o => o.AddValidator<TenantValidator>());

        using var scope = _sp.CreateScope();
        var fromService = await Assert.ThrowsAsync<EntityInputException<Order>>(() =>
            scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder(tenant: null)))!;
        var validators = scope.ServiceProvider.GetRequiredService<IEnumerable<IEntityValidator>>();
        var fromExtension = await Assert.ThrowsAsync<EntityInputException<Order>>(() =>
            validators.ValidateItem(NewOrder(tenant: null), null, EntityWriteOperation.Add))!;

        Assert.That(fromExtension.Errors, Is.EqualTo(fromService.Errors));
    }

    // ── what a validator receives, and what a rejection leaves behind ───────────

    [Test]
    public async Task Original_Is_Null_On_Add_And_The_Stored_Row_On_Modify()
    {
        var seen = new List<(EntityWriteOperation Operation, string? OriginalCode)>();
        Build(s => s.For<Order>(e => e.Validate(ctx => seen.Add((ctx.Operation, ctx.Original?.Code)))));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        await service.Add(NewOrder("ORD-2"));
        var modified = NewOrder("ORD-9");
        modified.Id = id;
        await service.Modify(modified);

        Assert.That(seen, Is.EqualTo(new[] { (EntityWriteOperation.Add, (string?)null), (EntityWriteOperation.Modify, "ORD-1") }));
    }

    [Test]
    public async Task A_Rejected_Add_Or_Modify_Leaves_The_Entity_Untracked()
    {
        Build(s => s.For<Order>(e => e.Validate(ctx => ctx.AddError(string.Empty, "Rejected."))));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var added = NewOrder("ORD-2");
        var modified = NewOrder("ORD-9");
        modified.Id = id;

        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(added));
        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Modify(modified));

        Assert.Multiple(() =>
        {
            Assert.That(db.Entry(added).State, Is.EqualTo(EntityState.Detached));
            Assert.That(db.Entry(modified).State, Is.EqualTo(EntityState.Detached));
        });
    }

    [Test]
    public async Task Remove_Runs_The_Validators_For_A_Soft_Delete_Too()
    {
        var seen = new List<(EntityWriteOperation Operation, bool HasOriginal)>();
        Build(s => s.For<Order>(e => e.Validate(ctx => seen.Add((ctx.Operation, ctx.Original != null)))));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = (await service.Details(id))!;
        await service.Remove(order);
        await service.SaveChanges();

        using var check = _sp.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<ShopContext>().Orders.IgnoreQueryFilters().SingleAsync(x => x.Id == id);
        Assert.Multiple(() =>
        {
            Assert.That(seen, Is.EqualTo(new[] { (EntityWriteOperation.Remove, false) }));
            Assert.That(stored.IsArchived, Is.True, "Order is IArchivable, so the delete is a soft delete");
        });
    }

    [Test]
    public async Task A_Rejected_Remove_Marks_Nothing()
    {
        Build(s => s.For<Order>(e => e
            .Related(x => x.Lines)
            .Validate(ctx =>
            {
                if (ctx.Operation == EntityWriteOperation.Remove && ctx.Item.Status == OrderStatus.Shipped)
                {
                    ctx.AddError(nameof(Order.Status), "A shipped order cannot be deleted.");
                }
            })));
        var id = await SeedOrder(OrderStatus.Shipped);

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = (await service.Details(id))!;

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Remove(order)))!;

        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors.Single().Key, Is.EqualTo("Status"));
            Assert.That(db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged && x.State != EntityState.Detached), Is.Empty);
        });
    }

    [Test]
    public async Task A_Write_Service_On_The_Constructor_Without_Validators_Runs_None()
    {
        Build(s => s.For<Order>(e => e
            .UseWriteService<LegacyOrderWriteService>()
            .Validate(ctx => ctx.AddError(string.Empty, "Rejected."))));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        await service.Add(NewOrder());

        Assert.That(await service.SaveChanges(), Is.EqualTo(1));
    }

    [Test]
    public async Task A_Rejected_Add_Takes_Back_The_Rows_Its_Related_Sync_Marked()
    {
        Build(s => s.For<Order>(e => e
            .Related(x => x.Lines)
            .Validate(ctx =>
            {
                if (ctx.Item.Code == "BAD")
                {
                    ctx.AddError(nameof(Order.Code), "Rejected.");
                }
            })));

        // a job saving several orders in one scope, skipping the ones the validators refuse
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
            var rejected = NewOrder("BAD");
            rejected.Lines = [new OrderLine { Product = "Book", Quantity = 1 }];

            await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(rejected));
            await service.Add(NewOrder("ORD-2"));
            await service.SaveChanges();
        }

        using var check = _sp.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ShopContext>();
        var codes = await db.Orders.Select(x => x.Code).ToListAsync();
        var lines = await db.OrderLines.CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(codes, Is.EqualTo(new[] { "ORD-2" }));
            Assert.That(lines, Is.Zero);
        });
    }

    [Test]
    public async Task A_Rejected_Modify_Takes_Back_The_Rows_Its_Related_Sync_Marked()
    {
        Build(s => s.For<Order>(e => e
            .Related(x => x.Lines)
            .Validate(ctx =>
            {
                if (ctx.Operation == EntityWriteOperation.Modify)
                {
                    ctx.AddError(string.Empty, "Rejected.");
                }
            })));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        // drops the stored line and adds another: the sync marks one row Deleted and one Added
        var modified = NewOrder("ORD-9");
        modified.Id = id;
        modified.Lines = [new OrderLine { Product = "Pen", Quantity = 2 }];

        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Modify(modified));

        Assert.That(db.ChangeTracker.Entries().Where(x => x.State != EntityState.Unchanged), Is.Empty);
        Assert.That(await service.SaveChanges(), Is.Zero);
    }

    [Test]
    public async Task A_Rejected_Add_Takes_Back_A_Prepper_Edit_To_A_Row_Tracked_Before_It()
    {
        Build(s => s
            .For<Order>(e => e
                .Prepare(async (_, db) => (await db.Customers.SingleAsync()).Name = "Changed by a refused write")
                .Validate(ctx => ctx.AddError(string.Empty, "Rejected.")))
            .For<Customer>());
        using (var seed = _sp.CreateScope())
        {
            var seedDb = seed.ServiceProvider.GetRequiredService<ShopContext>();
            seedDb.Customers.Add(new Customer { Name = "Ada" });
            await seedDb.SaveChangesAsync();
        }

        using (var scope = _sp.CreateScope())
        {
            // tracked before the write, as a job's earlier step would leave it; the prepper only sets a value, which EF has
            // not detected when the validators refuse
            await scope.ServiceProvider.GetRequiredService<ShopContext>().Customers.SingleAsync();
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

            await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(NewOrder()));
            await service.SaveChanges();
        }

        using var check = _sp.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<ShopContext>().Customers.SingleAsync();
        Assert.That(stored.Name, Is.EqualTo("Ada"));
    }

    [Test]
    public async Task A_Rejected_Add_Keeps_An_Edit_The_Caller_Made_Before_It()
    {
        Build(s => s
            .For<Order>(e => e.Validate(ctx => ctx.AddError(string.Empty, "Rejected.")))
            .For<Customer>());
        using (var seed = _sp.CreateScope())
        {
            var seedDb = seed.ServiceProvider.GetRequiredService<ShopContext>();
            seedDb.Customers.Add(new Customer { Name = "Ada" });
            await seedDb.SaveChangesAsync();
        }

        using (var scope = _sp.CreateScope())
        {
            // the job's own edit, not yet detected by EF when the refused write starts: it is not the write's to take back
            var customer = await scope.ServiceProvider.GetRequiredService<ShopContext>().Customers.SingleAsync();
            customer.Name = "Renamed by the job";
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

            await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(NewOrder()));
            await service.SaveChanges();
        }

        using var check = _sp.CreateScope();
        var stored = await check.ServiceProvider.GetRequiredService<ShopContext>().Customers.SingleAsync();
        Assert.That(stored.Name, Is.EqualTo("Renamed by the job"));
    }

    [Test]
    public async Task Only_A_Write_A_Validator_Runs_For_Scans_The_Tracker()
    {
        Build(s => s
            .For<Order>(e => e.Validate(_ => { }))
            .For<Customer>());

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var scans = 0;
        db.ChangeTracker.DetectingAllChanges += (_, _) => scans++;

        await scope.ServiceProvider.GetRequiredService<IEntityService<Customer>>().Add(new Customer { Name = "Ada" });
        var customerScans = scans;
        await scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder());

        Assert.Multiple(() =>
        {
            // nothing can refuse a Customer write, so it pays no pass over the tracker; the Order write does
            Assert.That(customerScans, Is.Zero);
            Assert.That(scans, Is.GreaterThan(customerScans));
        });
    }

    /// <summary>Refuses from an override of ValidateItem, which no registered validator covers.</summary>
    public class RefusingOrderWriteService(ShopContext dbContext, IEntityReadService<Order, int> readService,
        IEnumerable<IEntityPrepper> preppers, IEnumerable<IEntityValidator> validators, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ShopContext, Order>(dbContext, readService, preppers, validators, loggerFactory)
    {
        public override Task ValidateItem(Order item, Order? original, EntityWriteOperation operation, CancellationToken token = default)
            => item.Code == "BAD"
                ? throw new EntityInputException<Order>("Rejected.") { Item = item }
                : base.ValidateItem(item, original, operation, token);
        protected override bool CanBeRefused(Order item) => true;
    }

    [Test]
    public async Task An_Override_Refusing_Without_Validators_Keeps_The_Undo_Through_CanBeRefused()
    {
        Build(s => s.For<Order>(e => e.Related(x => x.Lines).UseWriteService<RefusingOrderWriteService>()));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var rejected = NewOrder("BAD");
        rejected.Lines = [new OrderLine { Product = "Book", Quantity = 1 }];

        await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(rejected));

        Assert.That(db.ChangeTracker.Entries(), Is.Empty, "the Related() sync's line is taken back");
    }

    [Test]
    public async Task A_Write_Leaves_Detection_To_A_Caller_That_Turned_It_Off()
    {
        Build(s => s.For<Order>().For<Customer>());
        using (var seed = _sp.CreateScope())
        {
            var seedDb = seed.ServiceProvider.GetRequiredService<ShopContext>();
            seedDb.Customers.Add(new Customer { Name = "Ada" });
            await seedDb.SaveChangesAsync();
        }

        using (var scope = _sp.CreateScope())
        {
            // a bulk job that turned detection off for speed: the write service does not detect on its behalf, so an edit
            // the job never detects is not saved — EF's own contract with detection off
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            var customer = await db.Customers.SingleAsync();
            customer.Name = "Renamed by the job";
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

            await service.Add(NewOrder());
            await service.SaveChanges();
        }

        using var check = _sp.CreateScope();
        var checkDb = check.ServiceProvider.GetRequiredService<ShopContext>();
        Assert.Multiple(async () =>
        {
            Assert.That(await checkDb.Orders.CountAsync(), Is.EqualTo(1));
            Assert.That((await checkDb.Customers.SingleAsync()).Name, Is.EqualTo("Ada"));
        });
    }

    [Test]
    public async Task Modify_Of_A_Row_That_Does_Not_Exist_Runs_No_Validator_And_Answers_Null()
    {
        var ran = false;
        Build(s => s.For<Order>(e => e.Validate(ctx =>
        {
            ran = true;
            ctx.AddError(string.Empty, "Rejected.");
        })));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var missing = NewOrder();
        missing.Id = 404;

        var result = await service.Modify(missing);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(ran, Is.False);
        });
    }

    [Test]
    public async Task A_Class_Implementing_The_Typed_Interface_Directly_Runs_Its_Typed_Validate()
    {
        Build(s => s.For<Order>(e => e.AddValidator<DirectOrderValidator>()));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();

        var ex = (await Assert.ThrowsAsync<EntityInputException<Order>>(() => service.Add(NewOrder(code: null))))!;
        Assert.That(ex.Errors.Single().Key, Is.EqualTo(nameof(Order.Code)));
    }

    [Test]
    public async Task A_DbContext_Validator_Receives_The_Token_Of_The_Write()
    {
        var seen = CancellationToken.None;
        Build(s => s.For<Order>(e => e.Validate(async (ctx, db, token) =>
        {
            seen = token;
            await db.Customers.AnyAsync(token);
        })));

        using var scope = _sp.CreateScope();
        using var cts = new CancellationTokenSource();
        await scope.ServiceProvider.GetRequiredService<IEntityService<Order>>().Add(NewOrder(), cts.Token);

        Assert.That(seen, Is.EqualTo(cts.Token));
    }

    [Test]
    public void InputErrors_Is_A_View_Over_Errors()
    {
        var ex = new EntityInputException<Order>("rejected")
        {
            Errors =
            {
                new EntityInputError("Code", "Code is taken."),
                new EntityInputError("Name", "A name is required."),
                new EntityInputError("Code", "Too long.")
            }
        };

        Assert.That(ex.InputErrors["Code"], Is.EqualTo("Code is taken. Too long."));

        ex.InputErrors["Code"] = "Upper case only.";
        ex.InputErrors["Total"] = "Must be positive.";
        ex.InputErrors.Remove("Name");

        Assert.Multiple(() =>
        {
            Assert.That(ex.Errors, Is.EqualTo(new[] { new EntityInputError("Code", "Upper case only."), new EntityInputError("Total", "Must be positive.") }));
            Assert.That(ex.InputErrors.Keys, Is.EqualTo(new[] { "Code", "Total" }));
            Assert.Throws<ArgumentException>(() => ex.InputErrors.Add("Code", "Again."));
        });
    }

    // ── startup checks ──────────────────────────────────────────────────────────

    private async Task<List<string>> StartupWarnings(Action<EntityServiceCollection<ShopContext>> configure, Action<EntityServiceCollectionOptions>? options = null)
    {
        var capture = new StartupValidationTests.CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddSingleton(_log);
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        configure(services.UseEntities<ShopContext>(o =>
        {
            o.UseDefaults();
            o.ConfigureValidation(v => v.Enabled = true);
            options?.Invoke(o);
        }));

        await using var sp = services.BuildServiceProvider();
        await StartupValidationTests.RunHostedServices(sp);
        return capture.Warnings;
    }

    [Test]
    public async Task Startup_Does_Not_Report_A_Validator_Scoped_To_A_Type_Deriving_From_A_Registered_Entity()
    {
        // a Person is saved through the Party service, and its validator runs there
        var warnings = await StartupWarnings(s => s.For<Party>(), o => o.AddValidator<PersonValidator>());

        Assert.That(warnings, Has.None.Contains("PersonValidator"));
    }

    [Test]
    public async Task Startup_Reports_A_Write_Service_Without_Validators_For_A_Validator_Scoped_To_A_Derived_Type()
    {
        var warnings = await StartupWarnings(s => s.For<Party>(e => e.UseWriteService<LegacyPartyWriteService>()),
            o => o.AddValidator<PersonValidator>());

        Assert.That(warnings, Has.Some.Contains("PersonValidator").And.Some.Contains("'LegacyPartyWriteService'"));
    }
}

/// <summary>
/// An attachment link marks its <c>Attachment</c> principal for removal alongside itself — after the validators, so a
/// rejected delete of the link leaves the file's row alone.
/// </summary>
[TestFixture]
public class ValidatorAttachmentTests
{
    public class Ticket : IEntity<int>, IHasAttachments, IHasAttachments<TicketAttachment>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<TicketAttachment>? Attachments { get; set; } = new List<TicketAttachment>();
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<TicketAttachment>().ToList();
        }
    }

    public class TicketAttachment : EntityAttachment
    {
        public TicketAttachment() => ObjectType = nameof(Ticket);
        public Ticket? Ticket { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ticket>()
                .HasMany(x => x.Attachments)
                .WithOne(a => a.Ticket!)
                .HasForeignKey(x => x.ObjectId)
                .HasPrincipalKey(x => x.Id);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-validators-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o =>
            {
                o.UseDefaults();
                o.AddValidator<TicketAttachment>(ctx =>
                {
                    if (ctx.Operation == EntityWriteOperation.Remove)
                    {
                        ctx.AddError(string.Empty, "Attachments of a ticket are kept.");
                    }
                });
            })
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e =>
            {
                e.Includes((query, _) => query.IncludeEntityAttachments());
                e.HasAttachments(x => x.Attachments);
            });
        _sp = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    [Test]
    public async Task A_Rejected_Delete_Of_A_Link_Leaves_Its_Attachment_Unmarked()
    {
        int linkId;
        using (var scope = _sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeskContext>().Database.EnsureCreatedAsync();
            var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
            var ticket = new Ticket { Subject = "Printer" };
            await tickets.Add(ticket);
            await tickets.SaveChanges();

            var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketAttachment, int>>();
            var link = new TicketAttachment
            {
                ObjectId = ticket.Id,
                Attachment = new Attachment { FileName = "log.txt", ContentType = "text/plain", Bytes = "content"u8.ToArray() }
            };
            await links.Add(link);
            await links.SaveChanges();
            linkId = link.Id;
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskContext>();
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketAttachment, int>>();
            var link = (await links.Details(linkId))!;
            Assert.That(link.Attachment, Is.Not.Null, "the link is loaded with its attachment");

            await Assert.ThrowsAsync<EntityInputException<TicketAttachment>>(() => links.Remove(link));

            Assert.That(db.ChangeTracker.Entries().Where(x => x.State == EntityState.Deleted), Is.Empty);
        }
    }
}
