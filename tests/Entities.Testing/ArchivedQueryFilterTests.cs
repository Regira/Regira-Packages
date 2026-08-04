using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The archived opt-ins must widen the result by the archived flag and by nothing else — in particular
/// they must never suspend a query filter the application configured itself, which is how a consumer may
/// legitimately express tenant/row security.
/// <para>
/// The mechanism differs per target framework, and these tests pin both. On <c>net10.0</c>
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> installs a <em>named</em> filter and the
/// opt-ins drop that name only. On <c>net8.0</c> (EF Core 9, no named filters) the call is a no-op, nothing
/// is ever ignored, and archived rows are excluded by the query builder at the root of the query instead —
/// so the app's own filters are structurally out of reach.
/// </para>
/// </summary>
[TestFixture]
public class ArchivedQueryFilterTests
{
    public class Doc : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public string? Tenant { get; set; }
    }

    /// <summary>Archived filter applied; the app additionally scopes rows with a query filter of its own.</summary>
    public class TenantDocContext(DbContextOptions<TenantDocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // the app's own query filter, configured before ours
            modelBuilder.Entity<Doc>().HasQueryFilter(x => x.Tenant == "me");
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>
    /// The same model with neither route wired (see the wiring opt-out in the test that uses it) — the
    /// contract being pinned is that nothing filters then.
    /// </summary>
    public class UnfilteredDocContext(DbContextOptions<UnfilteredDocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();
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

    private async Task<ServiceProvider> Build<TContext>(Action<DbContext> seed, DbContextWiring? wiring = null)
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
        }).For<Doc>();
        var sp = services.BuildServiceProvider();

        var db = sp.GetRequiredService<TContext>();
        await db.Database.EnsureCreatedAsync();
        seed(db);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return sp;
    }

    private static void SeedTenantRows(DbContext db)
        => db.AddRange(
            new Doc { Title = "mine-live", Tenant = "me" },
            new Doc { Title = "mine-archived", Tenant = "me", IsArchived = true },
            new Doc { Title = "theirs-live", Tenant = "other" },
            new Doc { Title = "theirs-archived", Tenant = "other", IsArchived = true });

    [Test]
    public async Task The_Query_Filter_Hides_Archived_Rows()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-live" }));
    }

    [Test]
    public async Task Without_The_Filter_Archived_Rows_Are_Not_Hidden_From_An_Included_Collection()
    {
        // Neither route installs the filter here: the DbContext does not call it and the wiring flag is off.
        using var sp = await Build<UnfilteredDocContext>(db => db.AddRange(
                new Doc { Title = "live" },
                new Doc { Title = "archived", IsArchived = true }),
            DbContextWiring.All & ~DbContextWiring.ArchivedQueryFilter);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>().List(new SearchObject());

#if NET10_0_OR_GREATER
        // The contract on EF Core 10: the named query filter is the single switch. A model that ends up
        // without it returns archived rows everywhere — the query builder composes no predicate of its own.
        // ArchivedQueryFilterValidator turns exactly this into a startup error.
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "archived" }),
            "with no archived query filter an IArchivable entity is not filtered at all");
#else
        // EF Core 9 installs no query filter at all, so the query builder composes the predicate itself and
        // the root query is filtered whether or not the call was made. Only Include()d collections — which a
        // root-level predicate cannot reach — stay unfiltered there.
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live" }),
            "on EF Core 9 the query builder excludes archived rows at the root of the query");
#endif
    }

    [Test]
    public async Task Archived_Only_Still_Honours_The_Apps_Own_Query_Filter()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>()
            .List(new SearchObject { Archived = ArchivedFilter.Only });

        // Same on both targets: net10 drops the archived filter by name, net8 drops nothing at all.
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-archived" }),
            "the archived opt-in must never suspend a query filter the app configured itself");
    }

    [Test]
    public async Task Archived_Included_Returns_Live_And_Archived_Of_This_Tenant_Only()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);

        var items = await sp.GetRequiredService<IEntityService<Doc, int>>()
            .List(new SearchObject { Archived = ArchivedFilter.Included });

        // Is.EquivalentTo, not Does.Contain: the other tenant's rows must be ABSENT, on both targets.
        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "mine-live", "mine-archived" }));
    }

    // ── write path ─────────────────────────────────────────────────────────────

    /// <summary>
    /// The security case. Every update resolves its original archived-inclusive
    /// (<c>EntityWriteService.Modify</c>), unconditionally and with no client opt-in involved. If that
    /// lookup suspended the app's own query filter, an app expressing tenant security as
    /// <c>HasQueryFilter</c> would let any tenant overwrite another tenant's archived row.
    /// </summary>
    [Test]
    public async Task An_App_Query_Filter_Still_Constrains_The_Write_Path()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);
        var db = sp.GetRequiredService<TenantDocContext>();
        var foreignId = db.Docs.IgnoreQueryFilters().Single(x => x.Title == "theirs-archived").Id;
        db.ChangeTracker.Clear();
        var service = sp.GetRequiredService<IEntityService<Doc, int>>();

        // the three archived-inclusive lookups every write route takes
        var details = await service.Details(foreignId, ArchivedFilter.Included);
        var existenceCheck = await service.Count(new { id = foreignId, Archived = ArchivedFilter.Included });
        var writeLookup = (await service.List(new { id = foreignId, Archived = ArchivedFilter.Included })).SingleOrDefault();

        // and the write itself
        var original = await service.Modify(new Doc { Id = foreignId, Title = "hijacked", Tenant = "me" });
        await service.SaveChanges();

        Assert.Multiple(() =>
        {
            Assert.That(details, Is.Null, "Archived=Included must not bypass the app's own query filter");
            Assert.That(existenceCheck, Is.EqualTo(0));
            Assert.That(writeLookup, Is.Null);
            Assert.That(original, Is.Null, "the write path must not resolve another tenant's row");
        });

        var untouched = db.Docs.IgnoreQueryFilters().AsNoTracking().Single(x => x.Id == foreignId);
        Assert.Multiple(() =>
        {
            Assert.That(untouched.Title, Is.EqualTo("theirs-archived"), "the other tenant's row must be unchanged");
            Assert.That(untouched.Tenant, Is.EqualTo("other"));
        });
    }

    [Test]
    public async Task An_App_Query_Filter_Still_Constrains_The_Write_Path_For_A_Live_Row()
    {
        using var sp = await Build<TenantDocContext>(SeedTenantRows);
        var db = sp.GetRequiredService<TenantDocContext>();
        var foreignId = db.Docs.IgnoreQueryFilters().Single(x => x.Title == "theirs-live").Id;
        db.ChangeTracker.Clear();
        var service = sp.GetRequiredService<IEntityService<Doc, int>>();

        var original = await service.Modify(new Doc { Id = foreignId, Title = "hijacked", Tenant = "me" });
        await service.SaveChanges();

        Assert.That(original, Is.Null);
        var untouched = db.Docs.IgnoreQueryFilters().AsNoTracking().Single(x => x.Id == foreignId);
        Assert.That(untouched.Title, Is.EqualTo("theirs-live"));
    }
}
