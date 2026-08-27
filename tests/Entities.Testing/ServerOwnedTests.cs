using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Preppers;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// Server-owned fields: <c>[ServerOwned]</c> (protect-only, registered for every entity by
/// <c>UseDefaults()</c>) and the fluent <c>e.ServerOwned(x =&gt; x.Code, mint)</c> (protect + mint).
/// <see cref="A_Raw_DbContext_Write_Keeps_Its_Change"/> and
/// <see cref="Archiving_Still_Works_On_An_Entity_With_ServerOwned_Fields"/> pin what makes this a prepper
/// rather than a primer: a restore from <c>entry.OriginalValues</c> on every <c>SaveChanges</c> would revert
/// a second writer, and — since <c>ArchivablePrimer</c> turns a delete into an update — every archive too.
/// </summary>
[TestFixture]
public class ServerOwnedTests
{
    public class Order : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        [ServerOwned] public string? Code { get; set; }
        [ServerOwned] public decimal Total { get; set; }
        public string? Status { get; set; }
        public bool IsArchived { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    public class OrderLine : IEntity<int>
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string? Product { get; set; }
        [ServerOwned] public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
    }

    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public string? Subject { get; set; }
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<Ticket> Tickets => Set<Ticket>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>()
                .HasMany(x => x.Lines)
                .WithOne()
                .HasForeignKey(x => x.OrderId);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private int _mintCount;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _mintCount = 0;

        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o => o.UseDefaults())
            .For<Order>(e => e
                .Related(x => x.Lines)
                .Includes((query, _) => query.Include(x => x.Lines)))
            .For<Ticket>(e => e.ServerOwned(x => x.Code, _ => $"T-{++_mintCount:D3}"));
        _sp = services.BuildServiceProvider();

        var db = _sp.GetRequiredService<ShopContext>();
        await db.Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private IEntityService<Order, int> Orders() => _sp.GetRequiredService<IEntityService<Order, int>>();
    private IEntityService<Ticket, int> Tickets() => _sp.GetRequiredService<IEntityService<Ticket, int>>();

    private async Task<int> SeedOrder(params OrderLine[] lines)
    {
        var service = Orders();
        var order = new Order
        {
            Code = "ORD-001",
            Total = 42.5m,
            Status = "New",
            Lines = lines.Length == 0 ? null : lines
        };
        await service.Add(order);
        await service.SaveChanges();
        return order.Id;
    }

    // ── the attribute ──────────────────────────────────────────────────────────

    [Test]
    public async Task An_Update_That_Omits_A_ServerOwned_Field_Restores_It()
    {
        var id = await SeedOrder();

        // what a PATCH mapped through a TInputDto without Code/Total produces
        await Orders().Modify(new Order { Id = id, Status = "Shipped" });
        await Orders().SaveChanges();

        var persisted = await Orders().Details(id);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Code, Is.EqualTo("ORD-001"));
            Assert.That(persisted.Total, Is.EqualTo(42.5m));
            Assert.That(persisted.Status, Is.EqualTo("Shipped"));
        });
    }

    [Test]
    public async Task An_Update_That_States_A_ServerOwned_Field_Cannot_Change_It()
    {
        var id = await SeedOrder();

        await Orders().Modify(new Order { Id = id, Status = "Shipped", Code = "HACKED", Total = 0.01m });
        await Orders().SaveChanges();

        var persisted = await Orders().Details(id);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Code, Is.EqualTo("ORD-001"));
            Assert.That(persisted.Total, Is.EqualTo(42.5m));
        });
    }

    [Test]
    public async Task The_Attribute_Does_Not_Mint_On_Create()
    {
        var service = Orders();
        var order = new Order { Status = "New" };
        await service.Add(order);
        await service.SaveChanges();

        Assert.That((await service.Details(order.Id))!.Code, Is.Null);
    }

    // ── the fluent form ────────────────────────────────────────────────────────

    [Test]
    public async Task The_Fluent_Form_Mints_On_Create_When_The_Value_Is_Unset()
    {
        var service = Tickets();
        var ticket = new Ticket { Subject = "Broken" };
        await service.Add(ticket);
        await service.SaveChanges();

        Assert.That((await service.Details(ticket.Id))!.Code, Is.EqualTo("T-001"));
    }

    [Test]
    public async Task The_Fluent_Form_Keeps_A_Value_That_Is_Already_Set()
    {
        var service = Tickets();
        var ticket = new Ticket { Subject = "Imported", Code = "LEGACY-9" };
        await service.Add(ticket);
        await service.SaveChanges();

        Assert.Multiple(() =>
        {
            Assert.That(ticket.Code, Is.EqualTo("LEGACY-9"));
            Assert.That(_mintCount, Is.Zero);
        });
    }

    [Test]
    public async Task The_Fluent_Form_Restores_On_Update_Instead_Of_Re_Minting()
    {
        var service = Tickets();
        var ticket = new Ticket { Subject = "Broken" };
        await service.Add(ticket);
        await service.SaveChanges();

        await service.Modify(new Ticket { Id = ticket.Id, Subject = "Broken (edited)" });
        await service.SaveChanges();

        var persisted = await service.Details(ticket.Id);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Code, Is.EqualTo("T-001"));
            Assert.That(persisted.Subject, Is.EqualTo("Broken (edited)"));
            Assert.That(_mintCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void A_Navigation_Cannot_Be_Declared_ServerOwned()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));

        var ex = Assert.Throws<ArgumentException>(() =>
            services.UseEntities<ShopContext>(o => o.UseDefaults())
                .For<Order>(e => e.ServerOwned(x => x.Lines)));

        Assert.That(ex!.Message, Does.Contain("scalars and foreign keys"));
    }

    [Test]
    public void The_Soft_Delete_Flag_Cannot_Be_Declared_ServerOwned()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));

        var ex = Assert.Throws<ArgumentException>(() =>
            services.UseEntities<ShopContext>(o => o.UseDefaults())
                .For<Order>(e => e.ServerOwned(x => x.IsArchived)));

        Assert.That(ex!.Message, Does.Contain("restore"));
    }

    // ── prepper, not primer ────────────────────────────────────────────────────

    [Test]
    public async Task A_Raw_DbContext_Write_Keeps_Its_Change()
    {
        var id = await SeedOrder();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = await db.Orders.SingleAsync(x => x.Id == id);
            order.Total = 99m;
            await db.SaveChangesAsync();
        }

        Assert.That((await Orders().Details(id))!.Total, Is.EqualTo(99m));
    }

    [Test]
    public async Task Archiving_Still_Works_On_An_Entity_With_ServerOwned_Fields()
    {
        var id = await SeedOrder();
        var service = Orders();

        await service.Remove((await service.List(new { id })).Single());
        await service.SaveChanges();

        var persisted = await service.Details(id, ArchivedFilter.Included);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.IsArchived, Is.True);
            Assert.That(persisted.Code, Is.EqualTo("ORD-001"));
        });
    }

    [Test]
    public async Task A_Restore_Still_Clears_The_Archived_Flag()
    {
        var id = await SeedOrder();
        var service = Orders();
        await service.Remove((await service.List(new { id })).Single());
        await service.SaveChanges();

        await service.Modify(new Order { Id = id, Status = "New", IsArchived = false });
        await service.SaveChanges();

        Assert.That((await service.Details(id))!.IsArchived, Is.False);
    }

    // ── owned children ─────────────────────────────────────────────────────────

    [Test]
    public async Task An_Owned_Childs_ServerOwned_Field_Survives_The_Parents_Update()
    {
        var id = await SeedOrder(new OrderLine { Product = "Widget", UnitPrice = 10m, Quantity = 2 });
        var lineId = (await Orders().Details(id))!.Lines!.Single().Id;

        await Orders().Modify(new Order
        {
            Id = id,
            Status = "Shipped",
            Lines = [new OrderLine { Id = lineId, OrderId = id, Product = "Widget", UnitPrice = 0.01m, Quantity = 3 }]
        });
        await Orders().SaveChanges();

        var line = (await Orders().Details(id))!.Lines!.Single();
        Assert.Multiple(() =>
        {
            Assert.That(line.UnitPrice, Is.EqualTo(10m));
            Assert.That(line.Quantity, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task A_New_Owned_Child_Keeps_The_Value_It_Arrives_With()
    {
        var id = await SeedOrder();

        await Orders().Modify(new Order
        {
            Id = id,
            Status = "New",
            Lines = [new OrderLine { OrderId = id, Product = "Gadget", UnitPrice = 5m, Quantity = 1 }]
        });
        await Orders().SaveChanges();

        Assert.That((await Orders().Details(id))!.Lines!.Single().UnitPrice, Is.EqualTo(5m));
    }

    // ── the reflection contract ────────────────────────────────────────────────

    [Test]
    public void Unenforceable_Declarations_Are_Excluded_From_The_Protected_Set()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ServerOwnedProperties.Protected(typeof(Order)).Select(p => p.Name),
                Is.EquivalentTo(new[] { nameof(Order.Code), nameof(Order.Total) }));
            Assert.That(ServerOwnedProperties.Declared(typeof(Ticket)), Is.Empty);
        });
    }

    [Test]
    public void The_Prepper_Is_A_No_Op_Without_An_Original()
    {
        var prepper = new AutoServerOwnedPrepper<Order>();
        var order = new Order { Code = "KEEP" };

        Assert.DoesNotThrowAsync(() => prepper.Prepare(order, null));
        Assert.That(order.Code, Is.EqualTo("KEEP"));
    }
}
