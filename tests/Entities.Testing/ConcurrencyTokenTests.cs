using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Entities.Testing;

/// <summary>
/// Optimistic concurrency through the write path: every version stamp — a concurrency token the server moves on the
/// write — is compared with the value the client sent, not with the row the update just reloaded, which would make
/// the check compare the database with itself. A token nothing moves is a data column the client edits, and is
/// compared with the stored row. The other writer is a context of its own (or raw SQL where the store moves the
/// token), and each refusal is paired with the same write carrying the current token, which must go through: a check
/// that never runs passes every "current token" case too, so only the pair proves it.
/// </summary>
[TestFixture]
public class ConcurrencyTokenTests
{
    public interface IVersioned
    {
        Guid Version { get; set; }
    }

    /// Mints an application-owned token as <c>HasConcurrencyTokenDbPrimer</c> does: on every update, and on an
    /// insert that carries none.
    public class VersionPrimer : EntityPrimerBase<IVersioned>
    {
        public override Task PrepareAsync(IVersioned entity, EntityEntry entry, CancellationToken token = default)
        {
            if (entry.State == EntityState.Modified || (entry.State == EntityState.Added && entity.Version == Guid.Empty))
            {
                entity.Version = Guid.NewGuid();
            }
            return Task.CompletedTask;
        }
    }

    public class Order : IEntity<int>, IVersioned
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        /// An application-owned token, minted by <see cref="VersionPrimer"/> — left undeclared, so it is a stamp because
        /// the primer moves it; <see cref="Page"/> covers the declared form.
        [ConcurrencyCheck] public Guid Version { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    public class OrderLine : IEntity<int>, IVersioned
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public string? Product { get; set; }
        public int Quantity { get; set; }
        [ConcurrencyCheck] public Guid Version { get; set; }
    }

    /// A stamp the primer derives from the content: a client sending back what it read gets the same value.
    public class Page : IEntity<int>
    {
        public int Id { get; set; }
        public string? Content { get; set; }
        [ConcurrencyCheck, VersionStamp] public string? ETag { get; set; }
    }

    public class ETagPrimer : EntityPrimerBase<Page>
    {
        public static string Hash(string? content) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(content ?? "")));

        public override Task PrepareAsync(Page entity, EntityEntry entry, CancellationToken token = default)
        {
            if (entry.State is EntityState.Modified or EntityState.Added)
            {
                entity.ETag = Hash(entity.Content);
            }
            return Task.CompletedTask;
        }
    }

    /// A token on a data column: the client edits it, and nothing on the server moves it.
    public class Customer : IEntity<int>
    {
        public int Id { get; set; }
        [ConcurrencyCheck] public string? LastName { get; set; }
        public string? City { get; set; }
    }

    /// A token a prepper restores from the stored row before the entity is attached.
    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        [ConcurrencyCheck, ServerOwned] public Guid Version { get; set; }
    }

    /// A store-generated token: a trigger moves it on every UPDATE, as a SQL Server rowversion would.
    public class Article : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int RowVersion { get; set; }
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<Article> Articles => Set<Article>();
        public DbSet<Customer> Customers => Set<Customer>();
        public DbSet<Page> Pages => Set<Page>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Order>()
                .HasMany(x => x.Lines)
                .WithOne()
                .HasForeignKey(x => x.OrderId);
            // SQLite has no rowversion; the trigger created in Setup plays its part on every UPDATE.
            modelBuilder.Entity<Article>()
                .Property(x => x.RowVersion).HasDefaultValue(1).ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o => o.UseDefaults().AddPrimer<VersionPrimer>().AddPrimer<ETagPrimer>())
            .For<Order>(e => e
                .Related(x => x.Lines)
                .Includes((query, _) => query.Include(x => x.Lines)))
            .For<Ticket>()
            .For<Article>()
            .For<Customer>()
            .For<Page>();
        _sp = services.BuildServiceProvider();

        await using var db = Raw();
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER Articles_RowVersion AFTER UPDATE OF Title ON Articles " +
            "BEGIN UPDATE Articles SET RowVersion = OLD.RowVersion + 1 WHERE Id = NEW.Id; END;");
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    /// A context outside the Regira pipeline: the other writer, and a reader that sees only what was committed.
    private ShopContext Raw() => new(new DbContextOptionsBuilder<ShopContext>().UseSqlite(_connection).Options);

    /// One write through the entity service, in a scope of its own — the way a request gets one.
    private async Task Write<TEntity>(TEntity item) where TEntity : class, IEntity<int>
    {
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<TEntity, int>>();
        await service.Save(item);
        await service.SaveChanges();
    }

    private async Task<TEntity> Find<TEntity>(int id) where TEntity : class
    {
        await using var db = Raw();
        return (await db.Set<TEntity>().FindAsync(id))!;
    }

    /// Another writer saves the row after the client read it.
    private async Task OtherWriter<TEntity>(int id, Action<TEntity> change) where TEntity : class
    {
        await using var db = Raw();
        change((await db.Set<TEntity>().FindAsync(id))!);
        await db.SaveChangesAsync();
    }

    private async Task RawSql(string sql, params object[] parameters)
    {
        await using var db = Raw();
        await db.Database.ExecuteSqlRawAsync(sql, parameters);
    }

    // ── the client's token ─────────────────────────────────────────────────────

    [Test]
    public async Task A_Stale_Token_Is_Refused_And_The_Other_Writers_Change_Survives()
    {
        var read = Guid.NewGuid();
        var order = new Order { Status = "New", Version = read };
        await Write(order);
        await OtherWriter<Order>(order.Id, x => { x.Status = "Paid"; x.Version = Guid.NewGuid(); });

        // what a PUT carries: the client's snapshot, token included
        var ex = Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(new Order { Id = order.Id, Status = "Cancelled", Version = read }));

        var stored = await Find<Order>(order.Id);
        Assert.Multiple(() =>
        {
            Assert.That(ex!.InnerException, Is.InstanceOf<DbUpdateConcurrencyException>());
            Assert.That(ex.Message, Is.EqualTo(EntityConcurrencyException.ClientMessage));
            Assert.That(stored.Status, Is.EqualTo("Paid"), "the other writer's change must survive");
        });
    }

    [Test]
    public async Task The_Current_Token_Goes_Through()
    {
        var order = new Order { Status = "New", Version = Guid.NewGuid() };
        await Write(order);
        var current = Guid.NewGuid();
        await OtherWriter<Order>(order.Id, x => { x.Status = "Paid"; x.Version = current; });

        await Write(new Order { Id = order.Id, Status = "Shipped", Version = current });

        Assert.That((await Find<Order>(order.Id)).Status, Is.EqualTo("Shipped"));
    }

    [Test]
    public async Task An_Absent_Token_Is_Not_Checked_And_Is_Not_Written()
    {
        var order = new Order { Status = "New", Version = Guid.NewGuid() };
        await Write(order);
        var current = Guid.NewGuid();
        await OtherWriter<Order>(order.Id, x => x.Version = current);

        // a client whose DTO carries no token: the stale-client comparison is skipped, not failed
        await Write(new Order { Id = order.Id, Status = "Shipped" });

        var stored = await Find<Order>(order.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Status, Is.EqualTo("Shipped"));
            Assert.That(stored.Version, Is.Not.EqualTo(Guid.Empty).And.Not.EqualTo(current), "Guid.Empty stood for 'not sent': the primer mints the token");
        });
    }

    [Test]
    public async Task A_Write_Racing_The_Save_Is_Caught_Even_Without_A_Client_Token()
    {
        // with no client token the reloaded row is the baseline, so a writer landing between the reload and the
        // save is still refused
        var order = new Order { Status = "New", Version = Guid.NewGuid() };
        await Write(order);

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, int>>();
        await service.Modify(new Order { Id = order.Id, Status = "Shipped" });
        await OtherWriter<Order>(order.Id, x => x.Version = Guid.NewGuid());

        Assert.ThrowsAsync<EntityConcurrencyException>(() => service.SaveChanges());
    }

    [Test]
    public async Task A_ServerOwned_Token_Is_Still_Compared_With_The_Clients_Value()
    {
        // [ServerOwned] puts the stored value back before the entity is attached — the client's value has to be
        // read before that, or every stale write would compare the stored token with itself and pass
        var read = Guid.NewGuid();
        var ticket = new Ticket { Subject = "Printer", Version = read };
        await Write(ticket);
        var current = Guid.NewGuid();
        await OtherWriter<Ticket>(ticket.Id, x => x.Version = current);

        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(new Ticket { Id = ticket.Id, Subject = "Stale", Version = read }));
        await Write(new Ticket { Id = ticket.Id, Subject = "Fresh", Version = current });

        Assert.That((await Find<Ticket>(ticket.Id)).Subject, Is.EqualTo("Fresh"));
    }

    // ── owned children ─────────────────────────────────────────────────────────

    private static Order Snapshot(int id, Guid version, string status, int lineId, Guid lineVersion, int quantity) => new()
    {
        Id = id,
        Status = status,
        Version = version,
        Lines = [new OrderLine { Id = lineId, OrderId = id, Product = "Pen", Quantity = quantity, Version = lineVersion }]
    };

    [Test]
    public async Task A_Stale_Child_Token_Fails_The_Whole_Save()
    {
        var version = Guid.NewGuid();
        var lineRead = Guid.NewGuid();
        var order = new Order { Status = "New", Version = version, Lines = [new OrderLine { Product = "Pen", Quantity = 1, Version = lineRead }] };
        await Write(order);
        var lineId = order.Lines!.Single().Id;
        var lineCurrent = Guid.NewGuid();
        await OtherWriter<OrderLine>(lineId, x => { x.Quantity = 5; x.Version = lineCurrent; });

        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(Snapshot(order.Id, version, "Cancelled", lineId, lineRead, 2)));
        var statusAfterRefusal = (await Find<Order>(order.Id)).Status;
        var quantityAfterRefusal = (await Find<OrderLine>(lineId)).Quantity;

        await Write(Snapshot(order.Id, version, "Shipped", lineId, lineCurrent, 3));
        var quantityAfterCurrentWrite = (await Find<OrderLine>(lineId)).Quantity;

        Assert.Multiple(() =>
        {
            Assert.That(statusAfterRefusal, Is.EqualTo("New"), "the parent's update rolls back with its child's");
            Assert.That(quantityAfterRefusal, Is.EqualTo(5), "the other writer's change must survive");
            Assert.That(quantityAfterCurrentWrite, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task An_Absent_Child_Token_Is_Not_Checked()
    {
        var version = Guid.NewGuid();
        var order = new Order { Status = "New", Version = version, Lines = [new OrderLine { Product = "Pen", Quantity = 1, Version = Guid.NewGuid() }] };
        await Write(order);
        var lineId = order.Lines!.Single().Id;
        var lineCurrent = Guid.NewGuid();
        await OtherWriter<OrderLine>(lineId, x => x.Version = lineCurrent);

        await Write(Snapshot(order.Id, version, "Shipped", lineId, Guid.Empty, 3));

        var line = await Find<OrderLine>(lineId);
        Assert.Multiple(() =>
        {
            Assert.That(line.Quantity, Is.EqualTo(3));
            Assert.That(line.Version, Is.Not.EqualTo(Guid.Empty).And.Not.EqualTo(lineCurrent));
        });
    }

    // ── a token the store moves ────────────────────────────────────────────────

    [Test]
    public async Task A_Store_Generated_Token_Is_Compared_With_The_Clients_Value()
    {
        var article = new Article { Title = "Draft" };
        await Write(article);
        var read = (await Find<Article>(article.Id)).RowVersion;
        // another writer's UPDATE: the trigger moves the token, as a rowversion would
        await RawSql("UPDATE Articles SET Title = 'Edited elsewhere' WHERE Id = {0}", article.Id);
        var current = (await Find<Article>(article.Id)).RowVersion;

        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(new Article { Id = article.Id, Title = "Stale", RowVersion = read }));
        await Write(new Article { Id = article.Id, Title = "Fresh", RowVersion = current });

        // read back through a fresh query: SQLite's RETURNING does not see what an AFTER trigger wrote
        var stored = await Find<Article>(article.Id);
        Assert.Multiple(() =>
        {
            Assert.That(current, Is.Not.EqualTo(read), "the trigger must have moved the token, or the refusal proves nothing");
            Assert.That(stored.Title, Is.EqualTo("Fresh"));
            Assert.That(stored.RowVersion, Is.EqualTo(current + 1), "the store owns the token: the write must not set it");
        });
    }

    [Test]
    public async Task A_Declared_Stamp_Is_Compared_When_The_Primer_Reproduces_The_Clients_Value()
    {
        var page = new Page { Content = "Draft" };
        await Write(page);
        var read = page.ETag;
        await OtherWriter<Page>(page.Id, x => { x.Content = "Edited elsewhere"; x.ETag = ETagPrimer.Hash(x.Content); });

        // the stale client writes back exactly what it read: the primer computes the ETag it already holds
        Assert.ThrowsAsync<EntityConcurrencyException>(() => Write(new Page { Id = page.Id, Content = "Draft", ETag = read }));
        var afterRefusal = (await Find<Page>(page.Id)).Content;
        await Write(new Page { Id = page.Id, Content = "Fresh", ETag = ETagPrimer.Hash("Edited elsewhere") });
        var afterCurrentWrite = (await Find<Page>(page.Id)).Content;

        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(ETagPrimer.Hash("Draft")));
            Assert.That(afterRefusal, Is.EqualTo("Edited elsewhere"), "the other writer's change must survive");
            Assert.That(afterCurrentWrite, Is.EqualTo("Fresh"));
        });
    }

    // ── a token on a data column ───────────────────────────────────────────────

    [Test]
    public async Task A_Data_Column_Token_Takes_The_Clients_Edit()
    {
        var customer = new Customer { LastName = "Peeters", City = "Gent" };
        await Write(customer);

        // the client's value is new data, not what it read: comparing the row with it would refuse every change
        await Write(new Customer { Id = customer.Id, LastName = "Janssens", City = "Gent" });

        Assert.That((await Find<Customer>(customer.Id)).LastName, Is.EqualTo("Janssens"));
    }

    [TestCase(null)]
    [TestCase("")]
    public async Task A_Data_Column_Token_Takes_The_Clients_Clear(string? cleared)
    {
        var customer = new Customer { LastName = "Peeters", City = "Gent" };
        await Write(customer);

        // an empty value is the edit itself here, not a token left out
        await Write(new Customer { Id = customer.Id, LastName = cleared, City = "Gent" });

        Assert.That((await Find<Customer>(customer.Id)).LastName, Is.EqualTo(cleared));
    }

    [Test]
    public async Task A_Data_Column_Token_Is_Compared_With_The_Stored_Row()
    {
        var customer = new Customer { LastName = "Peeters", City = "Gent" };
        await Write(customer);

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Customer, int>>();
        await service.Modify(new Customer { Id = customer.Id, LastName = "Peeters", City = "Brugge" });
        await OtherWriter<Customer>(customer.Id, x => x.LastName = "Maes");

        Assert.ThrowsAsync<EntityConcurrencyException>(() => service.SaveChanges());
        Assert.That((await Find<Customer>(customer.Id)).LastName, Is.EqualTo("Maes"), "the other writer's change must survive");
    }

    // ── what EF reports as a concurrency failure, token or not ─────────────────

    [Test]
    public async Task A_Row_Removed_By_Another_Writer_Is_A_Concurrency_Conflict()
    {
        var order = new Order { Status = "New", Version = Guid.NewGuid() };
        await Write(order);

        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, int>>();
        await service.Modify(new Order { Id = order.Id, Status = "Shipped", Version = order.Version });
        await RawSql("DELETE FROM Orders WHERE Id = {0}", order.Id);

        var ex = Assert.ThrowsAsync<EntityConcurrencyException>(() => service.SaveChanges());

        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        Assert.Multiple(() =>
        {
            // EF's documented recovery loops the conflicting entries — the inner exception carries them
            Assert.That(((DbUpdateConcurrencyException)ex!.InnerException!).Entries.Select(e => e.Entity), Has.Some.InstanceOf<Order>());
            Assert.That(db.ChangeTracker.Entries<Order>(), Is.Not.Empty, "a failed save keeps the tracker, like stock EF Core");
        });
    }
}
