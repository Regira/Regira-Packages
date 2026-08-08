using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// A hierarchy eager-loads its own children, so deleting a node with a required parent FK makes EF fix the
/// severed relationship up client-side and throw before the provider is ever reached — an
/// <see cref="InvalidOperationException"/>, not the <see cref="DbUpdateException"/> the constraint path
/// catches. Same integrity rule, so the caller gets the same answer instead of a raw 500.
/// </summary>
[TestFixture]
public class SeveredRequiredRelationshipTests
{
    public class Folder : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public ICollection<FolderLink>? Children { get; set; }
    }

    /// A many-to-many self-reference — a node under several parents — so both FKs are required.
    public class FolderLink : IEntity<int>
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public Folder? Parent { get; set; }
        public int ChildId { get; set; }
        public Folder? Child { get; set; }
    }

    public class FolderContext(DbContextOptions<FolderContext> options) : DbContext(options)
    {
        public DbSet<Folder> Folders { get; set; } = null!;
        public DbSet<FolderLink> FolderLinks { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Restrict, not ClientNoAction — this is the configuration the guides steer away from, and the
            // one whose failure has to stay a 409 rather than an unhandled 500.
            builder.Entity<FolderLink>()
                .HasOne(x => x.Parent)
                .WithMany(x => x.Children)
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.Entity<FolderLink>()
                .HasOne(x => x.Child)
                .WithMany()
                .HasForeignKey(x => x.ChildId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<FolderContext>(db => db.UseSqlite(_connection));
        services.UseEntities<FolderContext>()
            // eager-loading the children unconditionally is the documented shape for a simple entity, and
            // it is what puts the dependents in the change tracker
            .For<Folder>(e => e.Includes((query, _) => query.Include(x => x.Children!)));
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
        _connection.Close();
    }

    [Test]
    public async Task Deleting_A_Parent_With_Tracked_Children_Reports_A_Constraint_Conflict()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FolderContext>();
        await db.Database.EnsureCreatedAsync();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Folder, int>>();

        var root = new Folder { Title = "Root" };
        var child = new Folder { Title = "Child" };
        await service.Add(root);
        await service.Add(child);
        await service.SaveChanges();
        db.FolderLinks.Add(new FolderLink { ParentId = root.Id, ChildId = child.Id });
        await db.SaveChangesAsync();

        // materialising the links is what puts them in the change tracker — the delete below then severs
        // a tracked required relationship, which EF resolves client-side before reaching the provider
        var loaded = await db.Folders.Include(x => x.Children!).FirstAsync(x => x.Id == root.Id);
        Assert.That(loaded.Children, Is.Not.Empty, "the fixture only reproduces the trap while the links are tracked");

        // marking it Deleted already cascades to the tracked links, so this is where it fails — not at the flush
        var ex = Assert.ThrowsAsync<EntityConstraintException>(() => service.Remove(loaded));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Is.EqualTo(EntityConstraintException.ClientMessage));
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException, Is.Not.InstanceOf<DbUpdateException>(), "the provider was never reached");
        });
    }

    [Test]
    public async Task Deferring_The_Cascade_Moves_The_Same_Failure_Onto_SaveChanges()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FolderContext>();
        await db.Database.EnsureCreatedAsync();
        // the default is Immediate, which fails inside Remove; OnSaveChanges defers the same fixup to the
        // flush, which is the other path that has to answer with a conflict rather than a raw 500
        db.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Folder, int>>();

        var root = new Folder { Title = "Root" };
        var child = new Folder { Title = "Child" };
        await service.Add(root);
        await service.Add(child);
        await service.SaveChanges();
        db.FolderLinks.Add(new FolderLink { ParentId = root.Id, ChildId = child.Id });
        await db.SaveChangesAsync();

        var loaded = await db.Folders.Include(x => x.Children!).FirstAsync(x => x.Id == root.Id);
        await service.Remove(loaded); // deferred — nothing throws yet

        var ex = Assert.ThrowsAsync<EntityConstraintException>(() => service.SaveChanges());

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Is.EqualTo(EntityConstraintException.ClientMessage));
            Assert.That(ex.InnerException, Is.InstanceOf<InvalidOperationException>());
            Assert.That(ex.InnerException, Is.Not.InstanceOf<DbUpdateException>(), "the provider was never reached");
        });
    }

    [Test]
    public void IsSeveredRequiredRelationship_Only_Matches_That_Failure()
    {
        Assert.Multiple(() =>
        {
            // EF Core's own wording, which its resources do not localise
            Assert.That(
                new InvalidOperationException(
                    "The association between entities 'Folder' and 'Folder' with the key value '{ParentId: 1}' has been severed but the relationship is either marked as required or is implicitly required.")
                    .IsSeveredRequiredRelationship(),
                Is.True);
            Assert.That(new InvalidOperationException("Sequence contains no elements").IsSeveredRequiredRelationship(), Is.False);
            Assert.That(new InvalidOperationException().IsSeveredRequiredRelationship(), Is.False);
        });
    }
}
