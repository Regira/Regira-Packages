using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The startup gate for soft delete applied to reference data. Archiving a lookup row that other entities
/// reference through a <b>required</b> FK removes those entities from list results — the archived filter is a
/// real EF filter on the principal, so it propagates into <c>Include(...)</c> and composes as an inner join —
/// while <c>/search</c>'s count query, which carries no includes, keeps counting them. Pages come back short,
/// count disagrees with items, and nothing errors or logs.
/// <para>
/// The aggregate-parent shape (<c>Order</c> → <c>OrderLine</c>) is the same EF mechanism working as intended,
/// so the two have to be told apart or the warning is noise on every app. The discriminator is whether the
/// dependent is separately registered: a <c>Related()</c> child is only ever read through its parent.
/// </para>
/// </summary>
[TestFixture]
public class ArchivableReferenceDataValidatorTests
{
    private const string Hazard = "is the required end of a relationship with separately registered";

    public class Category : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    /// <summary>Reference data with no soft delete — the recommended shape, and the negative control.</summary>
    public class Status : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    public class Asset : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int CategoryId { get; set; }          // required — the hazard
        public Category? Category { get; set; }
        public int StatusId { get; set; }
        public Status? Status { get; set; }
        public int? OptionalCategoryId { get; set; } // optional — safe
    }

    public class Order : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }

    /// <summary>Written and read only through its parent's <c>Related()</c> — never separately registered.</summary>
    public class OrderLine : IEntity<int>
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public Order? Order { get; set; }
        public string? Description { get; set; }
    }

    public class ReferenceDataContext(DbContextOptions<ReferenceDataContext> options) : DbContext(options)
    {
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Asset> Assets => Set<Asset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Asset>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).IsRequired();
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>Same model, but the dependent carries the documented mirrored filter.</summary>
    public class MirroredContext(DbContextOptions<MirroredContext> options) : DbContext(options)
    {
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Asset> Assets => Set<Asset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Asset>().HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryId).IsRequired();
            modelBuilder.Entity<Asset>().HasQueryFilter(x => !x.Category!.IsArchived);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>Optional FK: archiving the principal leaves the dependent with a null navigation, not missing.</summary>
    public class OptionalFkContext(DbContextOptions<OptionalFkContext> options) : DbContext(options)
    {
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Asset> Assets => Set<Asset>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Asset>().Ignore(x => x.Category).Ignore(x => x.CategoryId);
            modelBuilder.Entity<Asset>().HasOne<Category>().WithMany().HasForeignKey(x => x.OptionalCategoryId).IsRequired(false);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    public class AggregateContext(DbContextOptions<AggregateContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderLine> OrderLines => Set<OrderLine>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<OrderLine>().HasOne(x => x.Order).WithMany(x => x!.Lines).HasForeignKey(x => x.OrderId).IsRequired();
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    public class Ticket : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }
        public ICollection<TicketFile>? Files { get; set; }
    }

    /// <summary>
    /// The shape every <c>EntityAttachment</c>-derived link has: a separately registered dependent that
    /// carries the owner's key as a plain scalar and has <b>no navigation back to it</b>.
    /// </summary>
    public class TicketFile : IEntity<int>
    {
        public int Id { get; set; }
        public int ObjectId { get; set; }
        public string? FileName { get; set; }
    }

    /// <summary>A second dependent of <see cref="Ticket"/> that <b>does</b> carry a navigation.</summary>
    public class TicketNote : IEntity<int>
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }
    }

    /// <summary>One principal, two dependents of different shape.</summary>
    public class MixedDependentsContext(DbContextOptions<MixedDependentsContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketFile> TicketFiles => Set<TicketFile>();
        public DbSet<TicketNote> TicketNotes => Set<TicketNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ticket>().HasMany(x => x.Files).WithOne()
                .HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id).IsRequired();
            modelBuilder.Entity<TicketNote>().HasOne(x => x.Ticket).WithMany()
                .HasForeignKey(x => x.TicketId).IsRequired();
            modelBuilder.SetArchivedQueryFilter();
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

    private async Task<List<string>> Warnings(Action<IServiceCollection> configure)
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        configure(services);

        await using var sp = services.BuildServiceProvider();
        foreach (var hostedService in sp.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        return capture.Warnings;
    }

    private static Action<Regira.Entities.DependencyInjection.ServiceCollections.Models.EntityServiceCollectionOptions> Logging()
        => o => o.ConfigureValidation(v =>
        {
            v.Enabled = true;
            v.ThrowOnError = false;
        });

    // ── the hazard ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Archivable_Reference_Data_Behind_A_Required_Fk_Is_Reported()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<ReferenceDataContext>(db => db.UseSqlite(_connection));
            services.UseEntities<ReferenceDataContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Category>()
                .For<Asset>();
        });

#if NET10_0_OR_GREATER
        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Some.Contains(Hazard).And.Some.Contains(nameof(Category)).And.Some.Contains(nameof(Asset)));
            Assert.That(warnings, Has.Some.Contains("Soft Delete"), "the message must point at the remedies");
            Assert.That(warnings, Has.Some.Contains("count"), "the items/count divergence is the reason it matters");
        });
#else
        Assert.That(warnings, Has.None.Contains(Hazard),
            "EF Core 9 installs no propagating archived filter, so the divergence cannot occur");
#endif
    }

    [Test]
    public async Task Every_Dependent_Is_Named_In_One_Line()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<MixedDependentsContext>(db => db.UseSqlite(_connection));
            services.UseEntities<MixedDependentsContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Ticket>()
                .For<TicketFile>()
                .For<TicketNote>();
        });

#if NET10_0_OR_GREATER
        Assert.That(warnings.Where(w => w.Contains(Hazard)).ToArray(), Has.Exactly(1).Contains(nameof(TicketFile)).And.Contains(nameof(TicketNote)));
#else
        Assert.That(warnings, Has.None.Contains(Hazard));
#endif
    }

    // ── false-positive guards ──────────────────────────────────────────────────

    [Test]
    public async Task An_Aggregate_Parent_Whose_Children_Are_Related_Rows_Is_Not_Reported()
    {
        // Hiding an archived Order's lines IS the intent, and OrderLine has no list endpoint of its own.
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<AggregateContext>(db => db.UseSqlite(_connection));
            services.UseEntities<AggregateContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Order>(e => e.Related(x => x.Lines));
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    [Test]
    public async Task An_Optional_Foreign_Key_Is_Not_Reported()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<OptionalFkContext>(db => db.UseSqlite(_connection));
            services.UseEntities<OptionalFkContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Category>()
                .For<Asset>();
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    [Test]
    public async Task A_Dependent_With_Its_Own_Mirrored_Filter_Is_Not_Reported()
    {
        // The documented remedy: the dependent eliminates the same rows from count and items alike.
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<MirroredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<MirroredContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Category>()
                .For<Asset>();
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    [Test]
    public async Task A_Non_Archivable_Principal_Is_Not_Reported()
    {
        // Status is reference data done the recommended way — a real DELETE plus OnDelete(Restrict).
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<ReferenceDataContext>(db => db.UseSqlite(_connection));
            services.UseEntities<ReferenceDataContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Status>();
        });

        Assert.That(warnings, Has.None.Contains($"{nameof(Status)} is IArchivable"));
    }
}
