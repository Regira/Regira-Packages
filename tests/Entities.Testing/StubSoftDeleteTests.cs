using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Normalizing;

namespace Entities.Testing;

/// <summary>
/// A soft delete writes the archive flag and what the primers stamp on it — never the rest of the row. The case that
/// needs it is the delete-by-key idiom, <c>Remove(new Supplier { Id = id })</c>: the stub carries nothing but its key,
/// and an update of every column would write its empty values over the stored row. The entity carries every member a
/// default primer or the normalizer touches, and each test runs on <c>SaveChanges()</c> and <c>SaveChangesAsync()</c>.
/// </summary>
[TestFixture]
public class StubSoftDeleteTests
{
    public class Supplier : IEntityWithSerial, IHasTitle, IHasTimestamps, IHasNormalizedContent, IHasConcurrencyToken, IArchivable
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Title { get; set; }
        public SupplierAddress Address { get; set; } = new();
        [MaxLength(1024), Normalized(SourceProperties = [nameof(Title)])]
        public string? NormalizedContent { get; set; }
        public Guid ConcurrencyToken { get; set; }
        public bool IsArchived { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class SupplierAddress
    {
        [MaxLength(64)] public string? City { get; set; }
    }

    /// <summary>No concurrency token: the save goes through, so what it writes is visible.</summary>
    public class Category : IEntityWithSerial, IHasTitle, IHasTimestamps, IArchivable
    {
        public int Id { get; set; }
        [MaxLength(64)] public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class SupplierContext(DbContextOptions<SupplierContext> options) : DbContext(options)
    {
        public DbSet<Supplier> Suppliers => Set<Supplier>();
        public DbSet<Category> Categories => Set<Category>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Supplier>().ComplexProperty(x => x.Address);
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<SupplierContext>(db => db.UseSqlite(_connection));
        services.UseEntities<SupplierContext>(o => o.UseDefaults())
            .For<Supplier>()
            .For<Category>();
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<SupplierContext>().Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private static Task<int> Save(DbContext db, bool synchronous)
        => synchronous ? Task.FromResult(db.SaveChanges()) : db.SaveChangesAsync();

    private async Task<Supplier> Seed(bool archived = false)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
        var supplier = new Supplier { Title = "Acme", Address = new SupplierAddress { City = "Gent" }, IsArchived = archived };
        db.Suppliers.Add(supplier);
        await db.SaveChangesAsync();
        return await Stored(supplier.Id);
    }

    private async Task<Supplier> Stored(int id)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
        return await db.Suppliers.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == id);
    }

    /// <summary>Archived, the columns nobody changed kept, and the soft delete's own stamps written.</summary>
    private static void AssertArchivedAndKept(Supplier stored, Supplier seeded)
        => Assert.Multiple(() =>
        {
            Assert.That(stored.IsArchived, Is.True);
            Assert.That(stored.Title, Is.EqualTo("Acme"));
            Assert.That(stored.Address.City, Is.EqualTo("Gent"));
            Assert.That(stored.NormalizedContent, Is.EqualTo(seeded.NormalizedContent).And.Not.Null);
            Assert.That(stored.Created, Is.EqualTo(seeded.Created));
            Assert.That(stored.LastModified, Is.Not.Null, "a soft delete is an update: LastModified is stamped");
            Assert.That(stored.ConcurrencyToken, Is.Not.EqualTo(seeded.ConcurrencyToken), "a soft delete moves the token");
        });

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Stub_Delete_Archives_The_Row_And_Keeps_Its_Other_Columns(bool synchronous)
    {
        var seeded = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            db.Suppliers.Remove(new Supplier { Id = seeded.Id });
            Assert.That(await Save(db, synchronous), Is.EqualTo(1));
        }

        AssertArchivedAndKept(await Stored(seeded.Id), seeded);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Stub_Delete_Of_An_Entity_Without_A_Token_Keeps_Its_Other_Columns(bool synchronous)
    {
        Category seeded;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            seeded = new Category { Title = "Books" };
            db.Categories.Add(seeded);
            await db.SaveChangesAsync();
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            db.Categories.Remove(new Category { Id = seeded.Id });
            Assert.That(await Save(db, synchronous), Is.EqualTo(1));
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            var stored = await db.Categories.IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == seeded.Id);
            Assert.Multiple(() =>
            {
                Assert.That(stored.IsArchived, Is.True);
                Assert.That(stored.Title, Is.EqualTo("Books"));
                Assert.That(stored.Created, Is.EqualTo(seeded.Created));
                Assert.That(stored.LastModified, Is.Not.Null);
            });
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Tracked_Delete_Archives_The_Row_And_Keeps_Its_Other_Columns(bool synchronous)
    {
        var seeded = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            db.Suppliers.Remove(await db.Suppliers.SingleAsync(x => x.Id == seeded.Id));
            Assert.That(await Save(db, synchronous), Is.EqualTo(1));
        }

        AssertArchivedAndKept(await Stored(seeded.Id), seeded);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Delete_Discards_The_Pending_Edits_Of_A_Tracked_Entity(bool synchronous)
    {
        var seeded = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            var supplier = await db.Suppliers.SingleAsync(x => x.Id == seeded.Id);
            supplier.Title = "Edited before the delete";
            db.Suppliers.Remove(supplier);
            await Save(db, synchronous);
        }

        AssertArchivedAndKept(await Stored(seeded.Id), seeded);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task An_Update_Of_An_Attached_Stub_Without_A_Token_Is_Still_Checked(bool synchronous)
    {
        // only a delete — soft or hard — carries no claim; an update built on a stub is compared with the empty token
        var seeded = await Seed();

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
        var stub = new Supplier { Id = seeded.Id };
        db.Suppliers.Attach(stub);
        stub.Title = "Renamed";

        Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Save(db, synchronous));
    }

    [Test]
    public async Task A_Service_Delete_Archives_The_Row_And_Keeps_Its_Other_Columns()
    {
        var seeded = await Seed();

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Supplier>>();
            await service.Remove((await service.Details(seeded.Id))!);
            Assert.That(await service.SaveChanges(), Is.EqualTo(1));
        }

        AssertArchivedAndKept(await Stored(seeded.Id), seeded);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Stub_Delete_Of_An_Archived_Row_Keeps_It_Archived_And_Intact(bool synchronous)
    {
        var seeded = await Seed(archived: true);

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SupplierContext>();
            db.Suppliers.Remove(new Supplier { Id = seeded.Id });
            Assert.That(await Save(db, synchronous), Is.EqualTo(1), "a repeated delete still reports the row it wrote");
        }

        AssertArchivedAndKept(await Stored(seeded.Id), seeded);
    }
}
