using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// A soft delete archives the row and leaves the rows that depend on it as they are. EF cascades a delete to the loaded
/// dependents the moment the parent is removed — deleting the dependents of a cascading relationship, nulling the
/// foreign key of an optional one — before the archivable primer turns the delete into an update, so the primer undoes
/// that; a delete through the service is marked without the cascade in the first place. The order carries one
/// dependent of each kind: lines (cascading, with options of their own), shipments (cascading and archivable
/// themselves), notes (optional) and invoices (required, <c>Restrict</c>).
/// </summary>
[TestFixture]
public class SoftDeleteDependentsTests
{
    public class Order : IEntityWithSerial, IArchivable
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public List<OrderLine> Lines { get; set; } = [];
        public List<Shipment> Shipments { get; set; } = [];
        public List<Note> Notes { get; set; } = [];
        public List<Invoice> Invoices { get; set; } = [];
    }

    public class OrderLine
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        [MaxLength(64)] public string? Product { get; set; }
        public List<LineOption> Options { get; set; } = [];
    }

    public class LineOption
    {
        public int Id { get; set; }
        public int OrderLineId { get; set; }
        [MaxLength(64)] public string? Name { get; set; }
    }

    public class Shipment : IArchivable
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public bool IsArchived { get; set; }
    }

    public class Note
    {
        public int Id { get; set; }
        public int? OrderId { get; set; }
    }

    public class Invoice
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<LineOption> LineOptions => Set<LineOption>();
        public DbSet<Shipment> Shipments => Set<Shipment>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Invoice> Invoices => Set<Invoice>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Order>().HasMany(x => x.Invoices).WithOne().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>Breaks off a save when armed: a primer after the archivable one that throws.</summary>
    public class PrimerFailure
    {
        public bool Armed { get; set; }
    }

    public class FailingPrimer(PrimerFailure failure) : EntityPrimerBase<Order>
    {
        public override Task PrepareAsync(Order entity, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, CancellationToken token = default)
            => failure.Armed ? throw new InvalidOperationException("A primer broke off the save") : Task.CompletedTask;
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<PrimerFailure>();
        services.UseEntities<ShopContext>(o =>
            {
                o.UseDefaults();
                o.AddPrimer<FailingPrimer>();
            })
            .For<Order>(e => e.Includes((query, _) => WithDependents(query, invoices: true)));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private static IQueryable<Order> WithDependents(IQueryable<Order> query, bool invoices)
    {
        query = query.Include(x => x.Lines).ThenInclude(x => x.Options).Include(x => x.Shipments).Include(x => x.Notes);
        return invoices ? query.Include(x => x.Invoices) : query;
    }

    private static Task<int> Save(DbContext db, bool synchronous)
        => synchronous ? Task.FromResult(db.SaveChanges()) : db.SaveChangesAsync();

    private async Task<int> Seed()
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var order = new Order
        {
            Title = "Books",
            Lines = [new OrderLine { Product = "Pen", Options = [new LineOption { Name = "Blue" }] }, new OrderLine { Product = "Ink" }],
            Shipments = [new Shipment()],
            Notes = [new Note()],
            Invoices = [new Invoice()]
        };
        db.Orders.Add(order);
        await db.SaveChangesAsync();
        return order.Id;
    }

    private async Task<(bool Archived, string?[] Products, int Options, bool[] ShipmentsArchived, int?[] NoteOrders, int Invoices)> Stored(int id)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        return (
            await db.Orders.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => x.IsArchived).SingleAsync(),
            await db.OrderLines.Where(x => x.OrderId == id).OrderBy(x => x.Id).Select(x => x.Product).ToArrayAsync(),
            await db.LineOptions.CountAsync(),
            await db.Shipments.IgnoreQueryFilters().Where(x => x.OrderId == id).Select(x => x.IsArchived).ToArrayAsync(),
            await db.Notes.Select(x => x.OrderId).ToArrayAsync(),
            await db.Invoices.CountAsync(x => x.OrderId == id));
    }

    private static void AssertArchivedWithDependentsKept((bool Archived, string?[] Products, int Options, bool[] ShipmentsArchived, int?[] NoteOrders, int Invoices) stored,
        int id, params string[] products)
        => Assert.Multiple(() =>
        {
            Assert.That(stored.Archived, Is.True);
            Assert.That(stored.Products, Is.EqualTo(products.Length == 0 ? new[] { "Pen", "Ink" } : products), "cascading dependents are kept");
            Assert.That(stored.Options, Is.EqualTo(1), "and theirs");
            Assert.That(stored.ShipmentsArchived, Is.EqualTo(new[] { false }), "an archivable dependent is kept, not archived");
            Assert.That(stored.NoteOrders, Is.EqualTo(new int?[] { id }), "an optional dependent keeps its foreign key");
            Assert.That(stored.Invoices, Is.EqualTo(1));
        });

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Soft_Delete_Leaves_Its_Loaded_Dependents_As_They_Are(bool synchronous)
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            // Restrict throws on Remove when its dependents are loaded, before any primer runs: see the service test
            db.Orders.Remove(await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == id));
            await Save(db, synchronous);
        }

        AssertArchivedWithDependentsKept(await Stored(id), id);
    }

    [Test]
    public async Task A_Service_Delete_Leaves_The_Dependents_It_Loaded_As_They_Are()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
            var order = (await service.Details(id))!;
            Assert.That(order.Lines, Has.Count.EqualTo(2), "Details loads the dependents");
            await service.Remove(order);
            Assert.That(await service.SaveChanges(), Is.EqualTo(1), "the archive flag is all that is written");
        }

        AssertArchivedWithDependentsKept(await Stored(id), id);
    }

    [Test]
    public async Task A_Pending_Edit_Of_A_Dependent_Is_Saved_With_The_Soft_Delete()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == id);
            order.Lines.Single(x => x.Product == "Ink").Product = "Ink refill";
            order.Title = "Edited with the delete";
            db.Orders.Remove(order);
            await db.SaveChangesAsync();
        }

        AssertArchivedWithDependentsKept(await Stored(id), id, "Pen", "Ink refill");
        using var check = _sp.CreateScope();
        Assert.That(await check.ServiceProvider.GetRequiredService<ShopContext>().Orders.IgnoreQueryFilters().Where(x => x.Id == id).Select(x => x.Title).SingleAsync(),
            Is.EqualTo("Books"), "the soft-deleted row itself writes the flag alone");
    }

    [Test]
    public async Task A_Dependent_Added_With_The_Soft_Delete_Is_Inserted()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == id);
            order.Lines.Add(new OrderLine { Product = "Paper" });
            db.ChangeTracker.DetectChanges();
            db.Orders.Remove(order);    // EF detaches the added line
            await db.SaveChangesAsync();
        }

        AssertArchivedWithDependentsKept(await Stored(id), id, "Pen", "Ink", "Paper");
    }

    [Test]
    public async Task A_Dependent_Removed_Alongside_A_Service_Delete_Stays_Removed()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order>>();
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var order = (await service.Details(id))!;
            await service.Remove(order);
            db.OrderLines.Remove(order.Lines.Single(x => x.Product == "Ink"));
            await service.SaveChanges();
        }

        var stored = await Stored(id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Archived, Is.True);
            Assert.That(stored.Products, Is.EqualTo(new[] { "Pen" }));
        });
    }

    [Test]
    public async Task A_Context_That_Defers_Its_Cascades_Keeps_A_Dependent_Removed_Alongside()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;   // nothing is cascaded before the primer
            var order = await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == id);
            db.Orders.Remove(order);
            db.OrderLines.Remove(order.Lines.Single(x => x.Product == "Ink"));
            await db.SaveChangesAsync();
        }

        var stored = await Stored(id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Archived, Is.True);
            Assert.That(stored.Products, Is.EqualTo(new[] { "Pen" }));
            Assert.That(stored.ShipmentsArchived, Is.EqualTo(new[] { false }));
        });
    }

    [Test]
    public async Task A_Save_A_Primer_Broke_Off_Does_Not_Stop_The_Next_Soft_Delete_Keeping_Its_Dependents()
    {
        var abandoned = await Seed();
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            var failure = scope.ServiceProvider.GetRequiredService<PrimerFailure>();
            failure.Armed = true;
            db.Orders.Remove(await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == abandoned));
            Assert.CatchAsync(() => db.SaveChangesAsync());
            failure.Armed = false;
            db.ChangeTracker.Clear();   // the caller gives up on that save and carries on with the context

            db.Orders.Remove(await WithDependents(db.Orders, invoices: false).SingleAsync(x => x.Id == id));
            await db.SaveChangesAsync();
        }

        var stored = await Stored(id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Archived, Is.True);
            Assert.That(stored.Products, Is.EqualTo(new[] { "Pen", "Ink" }));
            Assert.That(stored.ShipmentsArchived, Is.EqualTo(new[] { false }));
        });
    }

    [Test]
    public async Task A_Hard_Delete_Still_Cascades_To_Its_Loaded_Dependents()
    {
        var id = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.OrderLines.Remove(await db.OrderLines.Include(x => x.Options).SingleAsync(x => x.Product == "Pen"));
            await db.SaveChangesAsync();
        }

        var stored = await Stored(id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Products, Is.EqualTo(new[] { "Ink" }));
            Assert.That(stored.Options, Is.Zero);
        });
    }
}
