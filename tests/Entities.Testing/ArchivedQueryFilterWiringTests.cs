using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Conventions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// Soft delete must work without a single Regira call in the consumer's <c>DbContext</c>:
/// <c>UseEntities&lt;TContext&gt;(o =&gt; o.UseDefaults())</c> wires the archived query filter into the
/// context's options (<see cref="DbContextWiring.ArchivedQueryFilter"/>), exactly like the primer and
/// normalizer interceptors. <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> stays available and
/// composes with it — it is what a context built outside DI still needs.
/// <para>
/// Everything here is <c>net10.0</c> behavior. On <c>net8.0</c> (EF Core 9, no named query filters) no
/// archived query filter is installed by either route, and archived rows are excluded by
/// <c>FilterArchivablesQueryBuilder</c> at the root of the query instead.
/// </para>
/// </summary>
[TestFixture]
public class ArchivedQueryFilterWiringTests
{
    public class Doc : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public string? Tenant { get; set; }
    }

    /// <summary>The point of the exercise: a plain <c>DbContext</c>, no soft-delete plumbing.</summary>
    public class PlainDocContext(DbContextOptions<PlainDocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();
    }

    /// <summary>A query filter of the app's own, configured anonymously — and nothing else.</summary>
    public class TenantDocContext(DbContextOptions<TenantDocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Doc>().HasQueryFilter(x => x.Tenant == "me");
        }
    }

    /// <summary>
    /// An owner whose owned type also happens to implement <see cref="IArchivable"/>. EF refuses a query
    /// filter on an owned type (it is configured through its owner), so the convention must skip it — the
    /// model would otherwise fail to build.
    /// </summary>
    public class Ledger : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public Stamp? Stamp { get; set; }
    }

    public class Stamp : IArchivable
    {
        public bool IsArchived { get; set; }
        public string? By { get; set; }
    }

    public class OwnedContext(DbContextOptions<OwnedContext> options) : DbContext(options)
    {
        public DbSet<Ledger> Ledgers => Set<Ledger>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ledger>().OwnsOne(x => x.Stamp);
        }
    }

    /// <summary>Both routes at once — the explicit call must compose with the automatic wiring.</summary>
    public class ExplicitDocContext(DbContextOptions<ExplicitDocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
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

    private async Task<ServiceProvider> Build<TContext>(Action<DbContext> seed, DbContextWiring? wiring = null,
        Action<EntityServiceCollectionOptions>? configure = null)
        where TContext : DbContext
    {
        var services = new ServiceCollection();
        services.AddDbContext<TContext>(db => db.UseSqlite(_connection));
        services.UseEntities<TContext>(o =>
        {
            o.UseDefaults();
            if (wiring != null)
            {
                o.WireDbContext(wiring.Value);
            }
            configure?.Invoke(o);
        }).For<Doc>();
        var sp = services.BuildServiceProvider();

        var db = sp.GetRequiredService<TContext>();
        await db.Database.EnsureCreatedAsync();
        seed(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return sp;
    }

    private static void SeedRows(DbContext db)
        => db.AddRange(
            new Doc { Title = "live" },
            new Doc { Title = "archived", IsArchived = true });

    private static void SeedTenantRows(DbContext db)
        => db.AddRange(
            new Doc { Title = "mine-live", Tenant = "me" },
            new Doc { Title = "mine-archived", Tenant = "me", IsArchived = true },
            new Doc { Title = "theirs-live", Tenant = "other" },
            new Doc { Title = "theirs-archived", Tenant = "other", IsArchived = true });

    // ── the automatic wiring ───────────────────────────────────────────────────

    [Test]
    public async Task UseDefaults_Hides_Archived_Rows_Without_Any_DbContext_Call()
    {
        using var sp = await Build<PlainDocContext>(SeedRows);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }),
            "UseDefaults() must install the archived filter — a consumer DbContext needs no Regira call");
    }

    [Test]
    public async Task The_Wired_Filter_Reaches_The_Model_Itself()
    {
        using var sp = await Build<PlainDocContext>(SeedRows);
        var db = sp.GetRequiredService<PlainDocContext>();

        var rows = await db.Docs.AsNoTracking().ToListAsync();
        var unfiltered = await db.Docs.IgnoreQueryFilters().AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
#if NET10_0_OR_GREATER
            // a real EF query filter, so it applies to raw DbSet access and propagates into Include(...)
            Assert.That(rows.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }));
#else
            Assert.That(rows.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }),
                "EF Core 9 installs no query filter; the query builder filters at the root of the query instead");
#endif
            Assert.That(unfiltered, Has.Count.EqualTo(2), "the row is soft-deleted, not gone");
        });
    }

    [Test]
    public async Task The_Wired_Filter_Honours_The_Archived_Opt_Ins()
    {
        using var sp = await Build<PlainDocContext>(SeedRows);
        var service = sp.GetRequiredService<IEntityService<Doc, int>>();

        var included = await service.List(new SearchObject { Archived = ArchivedFilter.Included });
        var only = await service.List(new SearchObject { Archived = ArchivedFilter.Only });

        Assert.Multiple(() =>
        {
            Assert.That(included.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }));
            Assert.That(only.Select(x => x.Title), Is.EquivalentTo(new[] { "archived" }));
        });
    }

    // ── composition with the app's own model ───────────────────────────────────

    [Test]
    public async Task An_Anonymous_App_Query_Filter_Keeps_Applying()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);
        var service = sp.GetRequiredService<IEntityService<Doc, int>>();

        var defaults = await service.List(new SearchObject());
        var only = await service.List(new SearchObject { Archived = ArchivedFilter.Only });
        var included = await service.List(new SearchObject { Archived = ArchivedFilter.Included });

        Assert.Multiple(() =>
        {
#if NET10_0_OR_GREATER
            Assert.That(defaults.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-live" }));
#endif
            // Is.EquivalentTo, not Does.Contain: the archived opt-ins must never widen row security, and the
            // app's filter is re-registered under a name precisely so it survives them.
            Assert.That(only.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-archived" }));
            Assert.That(included.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-live", "mine-archived" }));
        });
    }

    [Test]
    public async Task An_Explicit_SetArchivedQueryFilter_Composes_With_The_Wiring()
    {
        using var sp = await Build<ExplicitDocContext>(SeedRows);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }),
            "calling it in OnModelCreating stays supported and must not conflict with the wired convention");
    }

    [Test]
    public async Task An_Included_Default_Still_Returns_Archived_Rows_Through_The_Wired_Filter()
    {
        // The filter is installed regardless of the query default, so an app that wants archived rows visible
        // gets that from the opt-in rather than from the absence of a filter: DefaultArchivedFilter = Included
        // makes every request suspend "Regira:Archived" by name.
        using var sp = await Build<PlainDocContext>(SeedRows,
            configure: o => o.DefaultArchivedFilter = ArchivedFilter.Included);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());
        var excluded = await sp.GetRequiredService<IEntityService<Doc, int>>()
            .List(new SearchObject { Archived = ArchivedFilter.Excluded });

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }),
                "an explicit Included default must survive the automatic wiring");
            Assert.That(excluded.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }),
                "and a per-request Excluded must still hide them");
        });
    }

    [Test]
    public async Task An_Owned_Archivable_Type_Is_Skipped_And_The_Model_Still_Builds()
    {
        var services = new ServiceCollection();
        services.AddDbContext<OwnedContext>(db => db.UseSqlite(_connection));
        services.UseEntities<OwnedContext>(o => o.UseDefaults());
        await using var sp = services.BuildServiceProvider();

        // resolving the context builds the model — EF throws here if a filter reaches the owned type
        var db = sp.GetRequiredService<OwnedContext>();
        await db.Database.EnsureCreatedAsync();
        db.AddRange(
            new Ledger { Title = "live", Stamp = new Stamp { By = "me" } },
            new Ledger { Title = "archived", IsArchived = true, Stamp = new Stamp { By = "me" } });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var owner = db.Model.FindEntityType(typeof(Ledger))!;
        // an owned type is a shared-type entity type ("Ledger.Stamp#Stamp") — reach it through the navigation
        var owned = owner.FindNavigation(nameof(Ledger.Stamp))!.TargetEntityType;
        var rows = await db.Ledgers.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(owned.IsOwned(), Is.True, "guard: the test is only meaningful while Stamp is owned");
#if NET10_0_OR_GREATER
            Assert.That(owned.GetDeclaredQueryFilters(), Is.Empty,
                "an owned type is configured through its owner and cannot carry a query filter");
            Assert.That(owner.FindDeclaredQueryFilter(ModelBuilderExtensions.ArchivedQueryFilterName), Is.Not.Null,
                "the owner is still filtered");
            Assert.That(rows.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }));
#else
            Assert.That(rows.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }));
#endif
        });
    }

    // ── opting out ─────────────────────────────────────────────────────────────

    [Test]
    public async Task Opting_Out_Of_The_Wiring_Leaves_The_Model_Unfiltered()
    {
        using var sp = await Build<PlainDocContext>(SeedRows, DbContextWiring.All & ~DbContextWiring.ArchivedQueryFilter);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());

#if NET10_0_OR_GREATER
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }),
            "without the flag nothing installs the filter — ArchivedQueryFilterValidator reports this at startup");
#else
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }),
            "on EF Core 9 the query builder excludes archived rows at the root of the query");
#endif
    }

    [Test]
    public void The_Options_Extension_Is_Only_Added_Where_A_Filter_Can_Exist()
    {
        var options = new DbContextOptionsBuilder<PlainDocContext>()
            .UseSqlite(_connection)
            .AddArchivedQueryFilter()
            .Options;

        var added = options.Extensions.OfType<ArchivedQueryFilterOptionsExtension>().Any();

#if NET10_0_OR_GREATER
        Assert.That(added, Is.True);
#else
        Assert.That(added, Is.False,
            "EF Core 9 installs no archived filter, so the call must leave the options — and EF's "
            + "internal-service-provider cache key — exactly as they were");
#endif
    }

    // ── a context built outside DI ─────────────────────────────────────────────

    [Test]
    public async Task A_Hand_Built_Context_Can_Wire_The_Filter_Through_Its_Options()
    {
        // No service collection is involved, so the options-based wiring cannot reach this context: the
        // AddDbContext counterpart of SetArchivedQueryFilter() is what a test/design-time/seeding context uses.
        await using var wired = new PlainDocContext(new DbContextOptionsBuilder<PlainDocContext>()
            .UseSqlite(_connection)
            .AddArchivedQueryFilter()
            .Options);
        await wired.Database.EnsureCreatedAsync();
        SeedRows(wired);
        await wired.SaveChangesAsync();
        wired.ChangeTracker.Clear();

        await using var bare = new PlainDocContext(new DbContextOptionsBuilder<PlainDocContext>()
            .UseSqlite(_connection)
            .Options);

        var wiredRows = await wired.Docs.AsNoTracking().ToListAsync();
        var bareRows = await bare.Docs.AsNoTracking().ToListAsync();

        Assert.Multiple(() =>
        {
#if NET10_0_OR_GREATER
            Assert.That(wiredRows.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }));
#endif
            Assert.That(bareRows, Has.Count.EqualTo(2),
                "a context constructed outside DI never sees the service collection's wiring");
        });
    }
}
