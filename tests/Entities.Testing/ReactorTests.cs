using System.ComponentModel.DataAnnotations;
using System.Data.Common;
using System.Transactions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Reactors;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Reactors;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Reactors;
using Regira.Entities.Reactors.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// Reactors run once the changes of a save are committed — never for a save that fails or a transaction that rolls
/// back — and receive each changed row with the values it was stored with before the save. Those originals come from
/// the entry when it was loaded — by a tracking query, or by the write path's own read when the entity went through
/// <c>IEntityService</c> — and from a read of the row, one query per entity type, when a writer attached it; the query
/// counts pin both.
/// </summary>
[TestFixture]
public class ReactorTests
{
    public enum OrderStatus { Pending, Processing, Shipped, Delivered }

    public class Order : IEntityWithSerial, IHasTimestamps, IArchivable
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Title { get; set; }
        public OrderStatus Status { get; set; }
        public ShippingAddress Address { get; set; } = new();
        public bool IsArchived { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    public class ShippingAddress
    {
        [MaxLength(64)] public string? City { get; set; }
    }

    public class OrderLine : IEntityWithSerial, IHasTimestamps
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        [MaxLength(64)] public string? Product { get; set; }
        public int Quantity { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class Invoice : IEntityWithSerial
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        [Required, MaxLength(32)] public string Number { get; set; } = null!;
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().ComplexProperty(x => x.Address);
    }

    public sealed class ReactionLog
    {
        public List<IEntityChange> Changes { get; } = [];
        public List<string> Events { get; } = [];
        public IEntityChange<T> Single<T>() => Changes.OfType<IEntityChange<T>>().Single();
    }

    public class TimestampReactor(ReactionLog log) : EntityReactorBase<IHasTimestamps>
    {
        public override Task React(IEntityChange<IHasTimestamps> change, CancellationToken token = default)
        {
            log.Events.Add($"timestamps:{change.Entity.GetType().Name}");
            return Task.CompletedTask;
        }
    }

    public class InvoiceReactor : EntityReactorBase<Invoice>
    {
        private readonly ReactionLog _log;

        public InvoiceReactor(ReactionLog log)
        {
            _log = log;
            log.Events.Add("created:InvoiceReactor");
        }

        public override Task React(IEntityChange<Invoice> change, CancellationToken token = default)
        {
            _log.Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    public class LineReactor(ReactionLog log) : EntityReactorBase<OrderLine>
    {
        public override Task React(IEntityChange<OrderLine> change, CancellationToken token = default)
        {
            log.Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    private sealed class CommandLog : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public int Selects => Commands.Count(c => c.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase));

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Commands.Add(command.CommandText);
            return result;
        }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Errors { get; } = [];
        public List<string> Warnings { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error) provider.Errors.Add($"{formatter(state, exception)} {exception?.Message}");
                if (logLevel == LogLevel.Warning) provider.Warnings.Add(formatter(state, exception));
            }
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private ReactionLog _log = null!;
    private CommandLog _commands = null!;
    private CaptureLoggerProvider _logs = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _log = new ReactionLog();
        _commands = new CommandLog();
        _logs = new CaptureLoggerProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _sp?.Dispose();
        _logs.Dispose();
        _connection.Close();
    }

    private void Build(Action<EntityServiceCollection<ShopContext>> configure, Action<EntityServiceCollectionOptions>? options = null,
        Action<DbContextOptionsBuilder>? dbOptions = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(_logs));
        services.AddSingleton(_log);
        services.AddDbContext<ShopContext>(db =>
        {
            db.UseSqlite(_connection).AddInterceptors(_commands);
            dbOptions?.Invoke(db);
        });
        configure(services.UseEntities<ShopContext>(o =>
        {
            o.UseDefaults();
            options?.Invoke(o);
        }));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreated();
    }

    private static Task Record(IEntityChange change, IServiceProvider services, CancellationToken token)
    {
        services.GetRequiredService<ReactionLog>().Changes.Add(change);
        return Task.CompletedTask;
    }

    private static Order NewOrder(string title = "Books", OrderStatus status = OrderStatus.Pending)
        => new() { Title = title, Status = status, Address = new ShippingAddress { City = "Gent" } };

    private async Task<int> SeedOrder(string title = "Books", OrderStatus status = OrderStatus.Pending, params OrderLine[] lines)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = NewOrder(title, status);
        order.Lines = lines;
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        _log.Changes.Clear();
        _log.Events.Clear();
        return order.Id;
    }

    private async Task<int> SeedInvoice(string number)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var invoice = new Invoice { OrderId = 1, Number = number };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();
        _log.Changes.Clear();
        return invoice.Id;
    }

    // ── what a reactor receives ─────────────────────────────────────────────────

    [Test]
    public async Task An_Insert_Reacts_With_The_Generated_Key()
    {
        Build(s => s.For<Order>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = NewOrder();
        await service.Add(order);
        await service.SaveChanges();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(EntityChangeKind.Added));
            Assert.That(change.Entity.Id, Is.EqualTo(order.Id).And.GreaterThan(0));
            Assert.That(change.Original, Is.Null);
            Assert.That(change.ChangedProperties, Is.Empty);
            Assert.That(change.Entity, Is.Not.SameAs(order), "a reactor gets a snapshot, not the caller's tracked instance");
        });
    }

    [Test]
    public async Task A_Service_Update_Reports_The_Stored_Row_Without_Reading_It_Again()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var incoming = NewOrder(status: OrderStatus.Shipped);
        incoming.Id = id;
        await service.Modify(incoming);
        _commands.Commands.Clear();
        await service.SaveChanges();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(EntityChangeKind.Modified));
            Assert.That(change.Original!.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(change.Entity.Status, Is.EqualTo(OrderStatus.Shipped));
            Assert.That(change.HasChanged(x => x.Status), Is.True);
            Assert.That(change.HasChanged(x => x.Title), Is.False);
            Assert.That(_commands.Selects, Is.Zero, "Modify already read the stored row — the save must not read it again");
        });
    }

    [Test]
    public async Task A_Raw_Update_Of_A_Detached_Entity_Reads_The_Stored_Row()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var incoming = NewOrder(status: OrderStatus.Shipped);
        incoming.Id = id;
        db.Update(incoming);
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Original!.Status, Is.EqualTo(OrderStatus.Pending), "Update() leaves the new value as the original");
            Assert.That(change.HasChanged(x => x.Status), Is.True);
            Assert.That(change.HasChanged(x => x.Title), Is.False);
            Assert.That(_commands.Selects, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Clearing_The_Tracker_Forgets_That_Modify_Loaded_The_Stored_Row()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var incoming = NewOrder(status: OrderStatus.Shipped);
        incoming.Id = id;
        await service.Modify(incoming);   // the stored row becomes this instance's originals
        db.ChangeTracker.Clear();
        db.Update(incoming);              // the same instance again, now with its own values as originals
        await db.SaveChangesAsync();

        Assert.That(_log.Single<Order>().Original!.Status, Is.EqualTo(OrderStatus.Pending));
    }

    [Test]
    public async Task An_Update_Of_A_Tracked_Entity_Uses_Its_Loaded_Originals()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        order.Status = OrderStatus.Delivered;
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Original!.Status, Is.EqualTo(OrderStatus.Pending));
            Assert.That(change.ChangedTo(x => x.Status, OrderStatus.Delivered), Is.True);
            Assert.That(_commands.Selects, Is.Zero);
        });
    }

    [Test]
    public async Task A_Stub_Delete_Reports_The_Stored_Row()
    {
        Build(s => s.For<Invoice>(e => e.React(Record)));
        var id = await SeedInvoice("INV-1");

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Remove(new Invoice { Id = id });
        await db.SaveChangesAsync();

        var change = _log.Single<Invoice>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(EntityChangeKind.Deleted));
            Assert.That(change.Entity.Number, Is.EqualTo("INV-1"));
            Assert.That(change.Original!.Number, Is.EqualTo("INV-1"));
        });
    }

    [Test]
    public async Task A_Soft_Delete_Is_An_Update_That_Archives()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var order = (await service.Details(id))!;
        await service.Remove(order);
        await service.SaveChanges();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(EntityChangeKind.Modified));
            Assert.That(change.ChangedTo(x => x.IsArchived, true), Is.True);
            Assert.That(change.Original!.Title, Is.EqualTo("Books"));
        });
    }

    [Test]
    public async Task A_Stub_Soft_Delete_Reports_The_Stored_Row_As_Original()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Remove(new Order { Id = id });
        await db.SaveChangesAsync();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Kind, Is.EqualTo(EntityChangeKind.Modified));
            Assert.That(change.ChangedTo(x => x.IsArchived, true), Is.True);
            Assert.That(change.Original!.Title, Is.EqualTo("Books"), "the stub carries no values of its own");
            Assert.That(change.Entity.Title, Is.EqualTo("Books"), "the soft delete writes the flag, not the stub's values");
            Assert.That(change.HasChanged(x => x.Title), Is.False);
        });
    }

    [Test]
    public async Task A_Soft_Delete_Reacts_To_The_Archived_Row_Alone()
    {
        Build(s => s.For<Order>(e => e.React(Record)), o => o.AddReactor<LineReactor>());
        var id = await SeedOrder(lines: [new OrderLine { Product = "Pen", Quantity = 1 }, new OrderLine { Product = "Ink", Quantity = 2 }]);

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Remove(await db.Orders.Include(x => x.Lines).SingleAsync(x => x.Id == id));   // EF cascades to the loaded lines
            await db.SaveChangesAsync();
        }

        using var check = _sp.CreateScope();
        Assert.Multiple(async () =>
        {
            Assert.That(_log.Changes.Select(c => c.Entity.GetType().Name), Is.EqualTo(new[] { nameof(Order) }));
            Assert.That(_log.Single<Order>().ChangedTo(x => x.IsArchived, true), Is.True);
            Assert.That(await check.ServiceProvider.GetRequiredService<ShopContext>().OrderLines.CountAsync(x => x.OrderId == id), Is.EqualTo(2));
        });
    }

    [Test]
    public async Task A_Complex_Property_Change_Is_Reported_As_A_Path()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        order.Address.City = "Brugge";
        await db.SaveChangesAsync();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.ChangedProperties, Does.Contain("Address.City"));
            Assert.That(change.HasChanged(x => x.Address.City), Is.True);
            Assert.That(change.HasChanged(x => x.Address), Is.True);
            Assert.That(change.Original!.Address.City, Is.EqualTo("Gent"));
            Assert.That(change.Entity.Address.City, Is.EqualTo("Brugge"));
        });
    }

    [Test]
    public async Task Related_Children_React_With_Their_Stored_Rows_Without_Extra_Reads()
    {
        Build(s => s.For<Order>(e => e.Related(x => x.Lines)), o => o.AddReactor<LineReactor>());
        var id = await SeedOrder(lines: [new OrderLine { Product = "Pen", Quantity = 1 }, new OrderLine { Product = "Ink", Quantity = 2 }]);

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
        var penId = await scope.ServiceProvider.GetRequiredService<ShopContext>().OrderLines.Where(x => x.Product == "Pen").Select(x => x.Id).SingleAsync();
        var incoming = NewOrder();
        incoming.Id = id;
        incoming.Lines = [new OrderLine { Id = penId, OrderId = id, Product = "Pen", Quantity = 5 }];
        await service.Modify(incoming);
        _commands.Commands.Clear();
        await service.SaveChanges();

        var lineChanges = _log.Changes.OfType<IEntityChange<OrderLine>>().ToArray();
        var modified = lineChanges.Single(c => c.Kind == EntityChangeKind.Modified);
        var deleted = lineChanges.Single(c => c.Kind == EntityChangeKind.Deleted);
        Assert.Multiple(() =>
        {
            Assert.That(modified.Original!.Quantity, Is.EqualTo(1));
            Assert.That(modified.Entity.Quantity, Is.EqualTo(5));
            Assert.That(deleted.Entity.Product, Is.EqualTo("Ink"));
            Assert.That(_commands.Selects, Is.Zero);
        });
    }

    [Test]
    public async Task An_Attached_Stub_Update_Reports_The_Stored_Row()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder(status: OrderStatus.Shipped);

        for (var save = 1; save <= 2; save++)
        {
            using var scope = _sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var stub = new Order { Id = id };
            db.Attach(stub);
            stub.Status = OrderStatus.Delivered;
            await db.SaveChangesAsync();
        }

        var changes = _log.Changes.OfType<IEntityChange<Order>>().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(changes, Has.Length.EqualTo(2));
            Assert.That(changes[0].Entity.Title, Is.EqualTo("Books"), "the stub's empty values were never written");
            Assert.That(changes[0].Entity.Address.City, Is.EqualTo("Gent"));
            Assert.That(changes[0].Original!.Status, Is.EqualTo(OrderStatus.Shipped), "the stored value, not the stub's default");
            Assert.That(changes[0].ChangedTo(x => x.Status, OrderStatus.Delivered), Is.True);
            Assert.That(changes[1].ChangedTo(x => x.Status, OrderStatus.Delivered), Is.False, "the row was delivered already");
            Assert.That(changes[1].ChangedProperties, Does.Not.Contain(nameof(Order.Status)));
        });
    }

    [Test]
    public async Task An_Entity_Attached_Again_After_A_Detach_Is_Read_Again()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = await db.Orders.SingleAsync(x => x.Id == id);
        db.Entry(order).State = EntityState.Detached;
        order.Title = "Renamed while detached";
        db.Attach(order);                 // attached as it is now: the rename is its original
        order.Status = OrderStatus.Shipped;
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        var change = _log.Single<Order>();
        Assert.Multiple(() =>
        {
            Assert.That(change.Original!.Title, Is.EqualTo("Books"));
            Assert.That(change.Entity.Title, Is.EqualTo("Books"), "the rename was attached as unchanged, so it was not written");
            Assert.That(change.HasChanged(x => x.Status), Is.True);
            Assert.That(_commands.Selects, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Raw_Updates_Of_Many_Detached_Entities_Read_Their_Rows_In_One_Query()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var ids = new List<int>();
        for (var i = 0; i < 20; i++)
        {
            ids.Add(await SeedOrder($"Order {i}"));
        }

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        foreach (var id in ids)
        {
            var incoming = NewOrder("Renamed", OrderStatus.Shipped);
            incoming.Id = id;
            db.Update(incoming);
        }
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        var changes = _log.Changes.OfType<IEntityChange<Order>>().OrderBy(c => c.Entity.Id).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(changes, Has.Length.EqualTo(20));
            Assert.That(changes.Select(c => c.Original!.Title), Is.EqualTo(ids.Select((_, i) => $"Order {i}")));
            Assert.That(changes.All(c => c.Original!.Status == OrderStatus.Pending && c.Original.Address.City == "Gent"), Is.True);
            Assert.That(changes.All(c => c.HasChanged(x => x.Status) && c.HasChanged(x => x.Title) && !c.HasChanged(x => x.Address)), Is.True);
            Assert.That(_commands.Selects, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task Stub_Deletes_Read_Their_Rows_In_One_Query()
    {
        Build(s => s.For<Invoice>(e => e.React(Record)));
        var ids = new[] { await SeedInvoice("INV-1"), await SeedInvoice("INV-2"), await SeedInvoice("INV-3") };

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        foreach (var id in ids)
        {
            db.Remove(new Invoice { Id = id });
        }
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_log.Changes.OfType<IEntityChange<Invoice>>().Select(c => c.Entity.Number).Order(), Is.EqualTo(new[] { "INV-1", "INV-2", "INV-3" }));
            Assert.That(_commands.Selects, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task A_Pooled_Context_Uses_What_It_Loads_On_Every_Lease()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();
        var options = new DbContextOptionsBuilder<ShopContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new EntityReactorInterceptor(_sp), _commands)
            .Options;
        var factory = new PooledDbContextFactory<ShopContext>(options);

        foreach (var status in new[] { OrderStatus.Shipped, OrderStatus.Delivered })
        {
            await using var db = factory.CreateDbContext();
            var order = await db.Orders.SingleAsync(x => x.Id == id);
            order.Status = status;
            _commands.Commands.Clear();
            await db.SaveChangesAsync();
            Assert.That(_commands.Selects, Is.Zero, $"the lease saving {status}");
        }

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Original!.Status), Is.EqualTo(new[] { OrderStatus.Pending, OrderStatus.Shipped }));
    }

    // ── which reactors run ──────────────────────────────────────────────────────

    [Test]
    public async Task ChangedTo_Reacts_To_The_Transition_Only()
    {
        Build(s => s.For<Order>(e => e.React(x => x.Status, OrderStatus.Shipped, Record)));
        var id = await SeedOrder();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = await db.Orders.SingleAsync(x => x.Id == id);
            order.Status = OrderStatus.Shipped;
            await db.SaveChangesAsync();
            order.Title = "Comics";
            await db.SaveChangesAsync();
            db.Orders.Add(NewOrder("Created shipped", OrderStatus.Shipped));
            await db.SaveChangesAsync();
        }

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EqualTo(new[] { "Books", "Created shipped" }));
    }

    [Test]
    public async Task An_Interface_Reactor_Reacts_To_Every_Implementing_Entity()
    {
        Build(s => s.For<Order>().For<Invoice>(), o => o.AddReactor<TimestampReactor>());

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Orders.Add(NewOrder());
        db.Invoices.Add(new Invoice { OrderId = 1, Number = "INV-1" });
        await db.SaveChangesAsync();

        Assert.That(_log.Events, Is.EqualTo(new[] { "timestamps:Order" }));
    }

    [Test]
    public async Task A_Reactor_Registered_For_An_Entity_Reacts_To_That_Entity_Only()
    {
        // written against IHasTimestamps, registered on Order: the OrderLine rows another reactor has captured are not its
        Build(s => s.For<Order>(e => e.AddReactor<TimestampReactor>()), o => o.AddReactor<LineReactor>());

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = NewOrder();
        order.Lines = [new OrderLine { Product = "Pen", Quantity = 1 }];
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        Assert.Multiple(() =>
        {
            Assert.That(_log.Events, Is.EqualTo(new[] { "timestamps:Order" }));
            Assert.That(_log.Changes.OfType<IEntityChange<OrderLine>>().Count(), Is.EqualTo(1), "the line was captured and reacted to");
        });
    }

    [Test]
    public async Task A_Reactor_Is_Only_Created_For_A_Change_It_Reacts_To()
    {
        Build(s =>
        {
            s.For<Order>(e => e.React(Record));
            s.For<Invoice>(e => e.AddReactor<InvoiceReactor>());
        });

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Orders.Add(NewOrder());
            await db.SaveChangesAsync();
            Assert.That(_log.Changes, Has.Count.EqualTo(1));
            Assert.That(_log.Events, Is.Empty, "no invoice was saved");

            db.Invoices.Add(new Invoice { OrderId = 1, Number = "INV-1" });
            await db.SaveChangesAsync();
        }
        Assert.That(_log.Events, Is.EqualTo(new[] { "created:InvoiceReactor" }));
    }

    [Test]
    public async Task Reactors_Run_In_Registration_Order()
    {
        Build(s => s.For<Order>(e => e
            .React((_, sp, _) => { sp.GetRequiredService<ReactionLog>().Events.Add("first"); return Task.CompletedTask; })
            .React((_, sp, _) => { sp.GetRequiredService<ReactionLog>().Events.Add("second"); return Task.CompletedTask; })));

        await SeedOrder();
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Orders.Add(NewOrder());
        await db.SaveChangesAsync();

        Assert.That(_log.Events, Is.EqualTo(new[] { "first", "second" }));
    }

    [Test]
    public async Task A_Multi_Row_Save_Reacts_To_Each_Row_Once_After_The_Commit()
    {
        Build(s => s.For<Order>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Orders.AddRange(NewOrder("A"), NewOrder("B"), NewOrder("C"));
        await db.SaveChangesAsync();

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EquivalentTo(new[] { "A", "B", "C" }));
    }

    [Test]
    public async Task A_Synchronous_Save_Reacts()
    {
        Build(s => s.For<Order>(e => e.React(Record)));
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var incoming = NewOrder(status: OrderStatus.Shipped);
        incoming.Id = id;
        db.Update(incoming);
        db.SaveChanges();

        Assert.That(_log.Single<Order>().Original!.Status, Is.EqualTo(OrderStatus.Pending));
    }

    // ── committed or not ────────────────────────────────────────────────────────

    [Test]
    public async Task An_Explicit_Transaction_Reacts_On_Commit()
    {
        Build(s => s.For<Order>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        db.Orders.Add(NewOrder("A"));
        await db.SaveChangesAsync();
        db.Orders.Add(NewOrder("B"));
        await db.SaveChangesAsync();
        Assert.That(_log.Changes, Is.Empty, "nothing is committed yet");

        await transaction.CommitAsync();
        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EqualTo(new[] { "A", "B" }));
    }

    [Test]
    public async Task A_Rolled_Back_Transaction_Does_Not_React()
    {
        Build(s => s.For<Order>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Orders.Add(NewOrder("Rolled back"));
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        db.Orders.Add(NewOrder("Committed"));
        await db.SaveChangesAsync();

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EqualTo(new[] { "Committed" }));
    }

    [Test]
    public async Task A_Transaction_Disposed_Without_Commit_Does_Not_React()
    {
        Build(s => s.For<Order>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await using (await db.Database.BeginTransactionAsync())
        {
            db.Orders.Add(NewOrder("Abandoned"));
            await db.SaveChangesAsync();
        }
        db.ChangeTracker.Clear();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Orders.Add(NewOrder("Committed"));
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        }

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EqualTo(new[] { "Committed" }));
    }

    [Test]
    public async Task A_Transaction_Shared_By_Two_Contexts_Reacts_To_Both_When_It_Commits()
    {
        Build(s =>
        {
            s.For<Order>(e => e.React(Record));
            s.For<Invoice>(e => e.React(Record));
        });
        var id = await SeedOrder();

        using var first = _sp.CreateScope();
        using var second = _sp.CreateScope();
        var orders = first.ServiceProvider.GetRequiredService<ShopContext>();
        var invoices = second.ServiceProvider.GetRequiredService<ShopContext>();
        await using var transaction = await orders.Database.BeginTransactionAsync();
        await invoices.Database.UseTransactionAsync(transaction.GetDbTransaction());

        (await orders.Orders.SingleAsync(x => x.Id == id)).Status = OrderStatus.Shipped;
        await orders.SaveChangesAsync();
        invoices.Invoices.Add(new Invoice { OrderId = id, Number = "INV-1" });
        await invoices.SaveChangesAsync();
        Assert.That(_log.Changes, Is.Empty, "nothing is committed yet");

        await transaction.CommitAsync();
        Assert.That(_log.Changes.Select(c => c.Entity.GetType().Name), Is.EqualTo(new[] { nameof(Order), nameof(Invoice) }));
    }

    [Test]
    public async Task A_Transaction_Shared_By_Two_Contexts_Does_Not_React_When_It_Rolls_Back()
    {
        Build(s => s.For<Invoice>(e => e.React(Record)));

        using var first = _sp.CreateScope();
        using var second = _sp.CreateScope();
        var owner = first.ServiceProvider.GetRequiredService<ShopContext>();
        var invoices = second.ServiceProvider.GetRequiredService<ShopContext>();
        await using (var transaction = await owner.Database.BeginTransactionAsync())
        {
            await invoices.Database.UseTransactionAsync(transaction.GetDbTransaction());
            invoices.Invoices.Add(new Invoice { OrderId = 1, Number = "INV-1" });
            await invoices.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        Assert.That(_log.Changes, Is.Empty);
    }

    [Test]
    public async Task A_Failed_Save_Does_Not_React()
    {
        Build(s => s.For<Invoice>(e => e.React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Invoices.Add(new Invoice { OrderId = 1, Number = null! });
        Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Invoices.Add(new Invoice { OrderId = 1, Number = "INV-2" });
        await db.SaveChangesAsync();

        Assert.That(_log.Single<Invoice>().Entity.Number, Is.EqualTo("INV-2"));
    }

    [Test]
    public async Task An_Ambient_Transaction_Reacts_When_It_Completes()
    {
        // SQLite cannot enlist, so the rows are written at once — what is pinned is that the reactions wait for the scope
        Build(s => s.For<Order>(e => e.React(Record)),
            dbOptions: db => db.ConfigureWarnings(w => w.Ignore(RelationalEventId.AmbientTransactionWarning)));

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Orders.Add(NewOrder("Aborted"));
            await db.SaveChangesAsync();
        }
        Assert.That(_log.Changes, Is.Empty, "a scope disposed without Complete() aborts");

        using (var ambient = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            using (var scope = _sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
                db.Orders.Add(NewOrder("Completed"));
                await db.SaveChangesAsync();
            }
            Assert.That(_log.Changes, Is.Empty, "nothing is committed before the scope completes");
            ambient.Complete();
        }

        Assert.That(_log.Changes.OfType<IEntityChange<Order>>().Select(c => c.Entity.Title), Is.EqualTo(new[] { "Completed" }));
    }

    // ── what a reactor may do ───────────────────────────────────────────────────

    [Test]
    public async Task A_Failing_Reactor_Neither_Fails_The_Save_Nor_Stops_The_Others()
    {
        Build(s => s.For<Order>(e => e
            .React((_, _, _) => throw new InvalidOperationException("mail server down"))
            .React(Record)));

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Orders.Add(NewOrder());
        await db.SaveChangesAsync();
        var stored = await db.Orders.CountAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stored, Is.EqualTo(1));
            Assert.That(_log.Changes, Has.Count.EqualTo(1));
            Assert.That(_logs.Errors, Has.Some.Contains("mail server down"));
        });
    }

    [Test]
    public async Task A_Reactor_Writes_In_Its_Own_Scope_And_Its_Save_Triggers_Reactors()
    {
        Build(s => s
            .For<Order>(e => e.React(x => x.Status, OrderStatus.Shipped, async (change, sp, token) =>
            {
                var invoices = sp.GetRequiredService<IEntityService<Invoice>>();
                await invoices.Add(new Invoice { OrderId = change.Entity.Id, Number = $"INV-{change.Entity.Id}" });
                await invoices.SaveChanges(token);
            }))
            .For<Invoice>(e => e.React(Record)));
        var id = await SeedOrder();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = await db.Orders.SingleAsync(x => x.Id == id);
            order.Status = OrderStatus.Shipped;
            await db.SaveChangesAsync();
            Assert.That(db.ChangeTracker.Entries<Invoice>(), Is.Empty, "the reactor's writes stay out of the caller's context");
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            Assert.That(await db.Invoices.Select(x => x.Number).ToListAsync(), Is.EqualTo(new[] { $"INV-{id}" }));
        }
        Assert.That(_log.Single<Invoice>().Kind, Is.EqualTo(EntityChangeKind.Added));
    }

    [Test]
    public async Task A_Reaction_Chain_That_Never_Ends_Stops_At_The_Maximum_Depth()
    {
        Build(s => s.For<Invoice>(e => e.React(async (change, sp, token) =>
        {
            var invoices = sp.GetRequiredService<IEntityService<Invoice>>();
            await invoices.Add(new Invoice { OrderId = 1, Number = $"INV-{change.Entity.Id + 1}" });
            await invoices.SaveChanges(token);
        })));

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Invoices.Add(new Invoice { OrderId = 1, Number = "INV-1" });
            await db.SaveChangesAsync();
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            Assert.That(await db.Invoices.CountAsync(), Is.EqualTo(1 + ReactionDispatcher.MaxDepth));
        }
        Assert.That(_logs.Errors, Has.Some.Contains("nested"));
    }

    // ── wiring ──────────────────────────────────────────────────────────────────

    [Test]
    public async Task Without_Reactors_A_Save_Reads_Nothing_Extra()
    {
        Build(s => s.For<Order>());
        var id = await SeedOrder();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var incoming = NewOrder(status: OrderStatus.Shipped);
        incoming.Id = id;
        db.Update(incoming);
        _commands.Commands.Clear();
        await db.SaveChangesAsync();

        Assert.That(_commands.Selects, Is.Zero);
    }

    [Test]
    public async Task Startup_Validation_Warns_When_Reactors_Cannot_Run()
    {
        Build(s => s.For<Order>(e => e.React(Record)),
            o => o.WireDbContext(DbContextWiring.All & ~DbContextWiring.Reactors).ConfigureValidation(v => v.Enabled = true));

        foreach (var hostedService in _sp.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Orders.Add(NewOrder());
            await db.SaveChangesAsync();
        }

        Assert.Multiple(() =>
        {
            Assert.That(_logs.Warnings, Has.Some.Contains("no reactor interceptor"));
            Assert.That(_log.Changes, Is.Empty);
        });
    }

    [Test]
    public void React_Rejects_A_Selector_That_Is_Not_A_Property()
        => Assert.Throws<ArgumentException>(() => Build(s => s.For<Order>(e => e.React(x => x.Title!.ToUpper(), "BOOKS", Record))));

    [Test]
    public void A_Change_Built_By_Hand_Answers_Like_A_Committed_One()
    {
        var change = new EntityChange<Order>(EntityChangeKind.Modified,
            new Order { Status = OrderStatus.Shipped, Address = new ShippingAddress { City = "Brugge" } },
            new Order { Status = OrderStatus.Pending, Address = new ShippingAddress { City = "Gent" } },
            ["Status", "Address.City"]);

        Assert.Multiple(() =>
        {
            Assert.That(change.ChangedTo(x => x.Status, OrderStatus.Shipped), Is.True);
            Assert.That(change.ChangedTo(x => x.Status, OrderStatus.Delivered), Is.False);
            Assert.That(change.HasChanged(x => x.Address), Is.True);
            Assert.That(change.HasChanged(x => x.Title), Is.False);
            Assert.That(new EntityChange<Order>(EntityChangeKind.Deleted, new Order { Status = OrderStatus.Shipped })
                .ChangedTo(x => x.Status, OrderStatus.Shipped), Is.False);
        });
    }
}
