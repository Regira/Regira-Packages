using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// <see cref="IHasConcurrencyToken"/>: <c>UseDefaults()</c> declares the property a concurrency token from the
/// context's options and registers the primer that moves it on every write. The behaviour tests use two clients
/// reading the same row — no raw SQL — because that is the case the marker exists for: the first save moves the token
/// under the second.
/// </summary>
[TestFixture]
public class ConcurrencyTokenMarkerTests
{
    public class Order : IEntity<int>, IHasConcurrencyToken, IArchivable
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public Guid ConcurrencyToken { get; set; }
        public bool IsArchived { get; set; }
    }

    /// Opted out by hand: an explicit configuration beats the convention.
    public class Draft : IEntity<int>, IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }

    /// Hard-deleted: no IArchivable, so removing one reaches the database as a DELETE.
    public class Receipt : IEntity<int>, IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string? Number { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }

    /// The marker with its stamp required: a client that leaves it out is refused instead of let through unchecked.
    public class Reservation : IEntity<int>, IHasConcurrencyToken
    {
        public int Id { get; set; }
        public string? Room { get; set; }
        [VersionStamp(Required = true)] public Guid ConcurrencyToken { get; set; }
    }

    /// Awaits before throwing, so the failure travels back through the synchronous SaveChanges bridge.
    public class ThrowingPrimer : EntityPrimerBase<Order>
    {
        public override async Task PrepareAsync(Order entity, EntityEntry entry, CancellationToken token = default)
        {
            await Task.Yield();
            throw new InvalidOperationException("primer failed");
        }
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Draft> Drafts => Set<Draft>();
        public DbSet<Receipt> Receipts => Set<Receipt>();
        public DbSet<Reservation> Reservations => Set<Reservation>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Draft>().Property(x => x.ConcurrencyToken).IsConcurrencyToken(false);
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

    private ServiceProvider Services(Action<EntityServiceCollectionOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(configure)
            .For<Order>()
            .For<Draft>()
            .For<Receipt>()
            .For<Reservation>();
        return services.BuildServiceProvider();
    }

    private async Task<ServiceProvider> Defaults()
    {
        var sp = Services(o => o.UseDefaults());
        using var scope = sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    private static bool IsToken<TEntity>(DbContext db)
        => db.Model.FindEntityType(typeof(TEntity))!.FindProperty(nameof(IHasConcurrencyToken.ConcurrencyToken))!.IsConcurrencyToken;

    /// A context outside the Regira pipeline, to read what was committed.
    private ShopContext Raw() => new(new DbContextOptionsBuilder<ShopContext>().UseSqlite(_connection).Options);

    private static Task Write(IServiceProvider sp, Order item) => Write<Order>(sp, item);

    private static async Task Write<TEntity>(IServiceProvider sp, TEntity item) where TEntity : class, IEntity<int>
    {
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<TEntity, int>>();
        await service.Save(item);
        await service.SaveChanges();
    }

    private async Task<Order> Stored(int id)
    {
        await using var db = Raw();
        return (await db.Orders.FindAsync(id))!;
    }

    // ── wiring ─────────────────────────────────────────────────────────────────

    [Test]
    public void UseDefaults_Declares_The_Marker_A_Concurrency_Token()
    {
        using var sp = Services(o => o.UseDefaults());
        using var scope = sp.CreateScope();

        Assert.That(IsToken<Order>(scope.ServiceProvider.GetRequiredService<ShopContext>()), Is.True);
    }

    /// A mapped base type declares the token without the marker; the derived type carries the marker.
    public class Paper : IEntity<int>
    {
        public int Id { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }

    public class Certificate : Paper, IHasConcurrencyToken;

    public class ArchiveContext(DbContextOptions<ArchiveContext> options) : DbContext(options)
    {
        public DbSet<Paper> Papers => Set<Paper>();
        public DbSet<Certificate> Certificates => Set<Certificate>();
    }

    [Test]
    public void The_Marker_Declares_A_Token_Its_Mapped_Base_Holds()
    {
        var services = new ServiceCollection();
        services.AddDbContext<ArchiveContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ArchiveContext>(o => o.UseDefaults()).For<Certificate>();
        using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();

        Assert.That(IsToken<Certificate>(scope.ServiceProvider.GetRequiredService<ArchiveContext>()), Is.True);
    }

    [Test]
    public void An_Explicit_Opt_Out_Wins()
    {
        using var sp = Services(o => o.UseDefaults());
        using var scope = sp.CreateScope();

        Assert.That(IsToken<Draft>(scope.ServiceProvider.GetRequiredService<ShopContext>()), Is.False);
    }

    [Test]
    public void Without_The_Wiring_Flag_It_Is_Not_A_Token()
    {
        using var sp = Services(o =>
        {
            o.UseDefaults();
            o.WireDbContext(DbContextWiring.All & ~DbContextWiring.ConcurrencyTokens);
        });
        using var scope = sp.CreateScope();

        Assert.That(IsToken<Order>(scope.ServiceProvider.GetRequiredService<ShopContext>()), Is.False);
    }

    [Test]
    public async Task A_Hand_Built_Context_Takes_The_Convention_From_Its_Options()
    {
        // outside DI the wiring never runs — the options builder carries the convention instead
        await using var without = Raw();
        await using var with = new ShopContext(new DbContextOptionsBuilder<ShopContext>().UseSqlite(_connection).AddConcurrencyTokenConvention().Options);

        Assert.Multiple(() =>
        {
            Assert.That(IsToken<Order>(without), Is.False);
            Assert.That(IsToken<Order>(with), Is.True);
        });
    }

    // ── the token moves ────────────────────────────────────────────────────────

    [Test]
    public async Task An_Insert_Mints_A_Token_Unless_One_Is_Given()
    {
        await using var sp = await Defaults();
        var minted = new Order { Status = "New" };
        var seeded = new Order { Status = "Imported", ConcurrencyToken = Guid.NewGuid() };
        var given = seeded.ConcurrencyToken;

        await Write(sp, minted);
        await Write(sp, seeded);

        Assert.Multiple(async () =>
        {
            Assert.That((await Stored(minted.Id)).ConcurrencyToken, Is.Not.EqualTo(Guid.Empty));
            Assert.That((await Stored(seeded.Id)).ConcurrencyToken, Is.EqualTo(given), "a seeded or imported token survives");
        });
    }

    [Test]
    public async Task The_Second_Of_Two_Clients_Holding_The_Same_Token_Is_Refused()
    {
        await using var sp = await Defaults();
        var order = new Order { Status = "New" };
        await Write(sp, order);
        var read = order.ConcurrencyToken;   // both clients read this

        await Write(sp, new Order { Id = order.Id, Status = "Paid", ConcurrencyToken = read });
        var ex = Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(sp, new Order { Id = order.Id, Status = "Cancelled", ConcurrencyToken = read }));

        var stored = await Stored(order.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ex, Is.Not.Null);
            Assert.That(stored.Status, Is.EqualTo("Paid"), "the first client's change survives");
            Assert.That(stored.ConcurrencyToken, Is.Not.EqualTo(read), "the first save moved the token");
        });
    }

    [Test]
    public async Task A_Soft_Delete_Moves_The_Token()
    {
        await using var sp = await Defaults();
        var order = new Order { Status = "New" };
        await Write(sp, order);
        var read = order.ConcurrencyToken;

        using (var scope = sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, int>>();
            await service.Remove((await service.Details(order.Id))!);
            await service.SaveChanges();
        }

        var stored = await Stored(order.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.IsArchived, Is.True);
            Assert.That(stored.ConcurrencyToken, Is.Not.EqualTo(read), "archiving is a write — an edit made before it is stale");
        });
    }

    [Test]
    public async Task A_Raw_DbContext_Write_Moves_The_Token_Too()
    {
        // a domain service saving through the DbContext itself: the primer still runs, so a client holding the
        // old token is refused
        await using var sp = await Defaults();
        var order = new Order { Status = "New" };
        await Write(sp, order);
        var read = order.ConcurrencyToken;

        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            (await db.Orders.FindAsync(order.Id))!.Status = "Picked";
            await db.SaveChangesAsync();
        }

        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(sp, new Order { Id = order.Id, Status = "Cancelled", ConcurrencyToken = read }));
        Assert.That((await Stored(order.Id)).Status, Is.EqualTo("Picked"));
    }

    // ── a stamp the client must send ───────────────────────────────────────────

    [Test]
    public async Task A_Required_Stamp_The_Client_Leaves_Out_Is_Refused_As_Input()
    {
        using var sp = await Defaults();
        // the insert carries no token and is not refused: there is nothing read yet to prove
        var reservation = new Reservation { Room = "A1" };
        await Write(sp, reservation);

        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Reservation, int>>();
        var ex = Assert.ThrowsAsync<EntityInputException<Reservation>>(() => service.Modify(new Reservation { Id = reservation.Id, Room = "B2" }));

        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        await using var raw = Raw();
        var stored = await raw.Reservations.FindAsync(reservation.Id);
        Assert.Multiple(() =>
        {
            Assert.That(reservation.ConcurrencyToken, Is.Not.EqualTo(Guid.Empty), "the insert minted a token");
            Assert.That(ex!.InputErrors.Keys, Is.EquivalentTo(new[] { nameof(Reservation.ConcurrencyToken) }), "the field a 400 names");
            Assert.That(ex.Item, Is.Not.Null.And.Property(nameof(Reservation.Room)).EqualTo("B2"));
            Assert.That(db.ChangeTracker.Entries<Reservation>().Where(e => e.State == EntityState.Modified), Is.Empty, "refused before anything is attached");
            Assert.That(stored!.Room, Is.EqualTo("A1"));
        });
    }

    [Test]
    public async Task A_Required_Stamp_Is_Checked_Like_Any_Other()
    {
        using var sp = await Defaults();
        var reservation = new Reservation { Room = "A1" };
        await Write(sp, reservation);
        var read = reservation.ConcurrencyToken;
        // the first client's save moves the token under the second
        await Write(sp, new Reservation { Id = reservation.Id, Room = "Edited elsewhere", ConcurrencyToken = read });

        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(sp, new Reservation { Id = reservation.Id, Room = "Stale", ConcurrencyToken = read }));

        await using var raw = Raw();
        var current = (await raw.Reservations.FindAsync(reservation.Id))!.ConcurrencyToken;
        await Write(sp, new Reservation { Id = reservation.Id, Room = "Fresh", ConcurrencyToken = current });
        raw.ChangeTracker.Clear();
        Assert.That((await raw.Reservations.FindAsync(reservation.Id))!.Room, Is.EqualTo("Fresh"));
    }

    // ── hard deletes and stubs ─────────────────────────────────────────────────

    [Test]
    public async Task A_Stub_Delete_Goes_Through_Without_A_Token()
    {
        using var sp = await Defaults();
        var id = await NewReceipt(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        // the shape every hard delete by id takes: an entity carrying its key and nothing else
        db.Remove(new Receipt { Id = id });
        await db.SaveChangesAsync();

        await using var raw = Raw();
        Assert.That(await raw.Receipts.FindAsync(id), Is.Null);
    }

    [Test]
    public async Task A_Stub_Delete_Still_Fails_On_A_Token_The_Caller_Got_Wrong()
    {
        using var sp = await Defaults();
        var id = await NewReceipt(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        // a caller that does hold a token is making a claim, and a stale one is still a conflict
        db.Remove(new Receipt { Id = id, ConcurrencyToken = Guid.NewGuid() });

        Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
    }

    [Test]
    public async Task A_Stub_Delete_Goes_Through_On_The_Synchronous_Save_Path()
    {
        using var sp = await Defaults();
        var id = await NewReceipt(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Remove(new Receipt { Id = id });
        // SaveChanges, not SaveChangesAsync: reading the stored token is the first primer work in the codebase to
        // await real I/O while the caller holds the thread, which is what the blocking bridge has to survive
        db.SaveChanges();

        await using var raw = Raw();
        Assert.That(await raw.Receipts.FindAsync(id), Is.Null);
    }

    [Test]
    public async Task A_Stub_Delete_Survives_A_Synchronous_Save_On_A_Single_Lane_Scheduler()
    {
        using var sp = await Defaults();
        var id = await NewReceipt(sp);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        db.Remove(new Receipt { Id = id });

        // an exclusive scheduler has one lane, and it is the one blocked here: the awaited query inside the primer
        // deadlocks unless the bridge starts the work on the default scheduler
        var scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;
        var save = Task.Factory.StartNew(() => db.SaveChanges(), CancellationToken.None, TaskCreationOptions.None, scheduler);

        // Wait with a timeout, so a deadlock fails the test instead of hanging the run
        Assert.That(save.Wait(TimeSpan.FromSeconds(30)), Is.True, "the synchronous save deadlocked");

        await using var raw = Raw();
        Assert.That(await raw.Receipts.FindAsync(id), Is.Null);
    }

    // ── the synchronous save path ──────────────────────────────────────────────

    [Test]
    public async Task A_Primer_That_Throws_After_Awaiting_Reaches_The_Caller_Unwrapped()
    {
        var sp = Services(o =>
        {
            o.UseDefaults();
            o.Services.AddPrimer<ThrowingPrimer>();
        });
        using (sp)
        {
            using var seed = sp.CreateScope();
            await seed.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreatedAsync();

            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
            db.Orders.Add(new Order { Status = "new" });

            // SaveChanges, not SaveChangesAsync: the blocking bridge must rethrow the original, not an AggregateException
            var ex = Assert.Throws<InvalidOperationException>(() => db.SaveChanges());

            Assert.That(ex!.Message, Is.EqualTo("primer failed"));
        }
    }

    private static async Task<int> NewReceipt(IServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Receipt, int>>();
        var receipt = new Receipt { Number = "R-1" };
        await service.Save(receipt);
        await service.SaveChanges();
        return receipt.Id;
    }
}
