using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Services;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The soft-delete round trip: <c>DELETE</c> archives instead of removing, list/search honour
/// <c>archived</c>, <c>Details(id)</c> keeps 404-ing on an archived row while the write path reaches it
/// (so a restore needs no client change).
/// <para>
/// On <c>net10.0</c> archived rows are hidden by the named EF query filter installed by
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/> — which is also what keeps archived
/// <em>children</em> out of an included collection; on <c>net8.0</c> that call is a no-op and
/// <see cref="FilterArchivablesQueryBuilder"/> composes the predicate at the root of the query instead, so
/// the Include-filtering tests below are net10-only. Either way <see cref="ISearchObject.Archived"/> is read
/// by that one filter and by nothing else, while every other global filter (owner/tenant row security) is a
/// plain predicate in the same aggregate loop and keeps running.
/// <see cref="Owner_Filter_Still_Applies_On_A_ById_Write_Path_With_Archived_Included"/> is the regression
/// guard for exactly that; <c>ArchivedQueryFilterTests</c> pins the same guarantee for row security
/// expressed as an EF <c>HasQueryFilter</c>.
/// </para>
/// </summary>
[TestFixture]
public class ArchivedRoundTripTests
{
    public interface IOwnedDoc
    {
        string? Owner { get; set; }
    }

    public class Doc : IEntity<int>, IArchivable, IOwnedDoc
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
        public string? Owner { get; set; }
        public ICollection<Note>? Notes { get; set; }
    }

    /// <summary>An archivable child, loaded through <c>Includes()</c>.</summary>
    public class Note : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Text { get; set; }
        public bool IsArchived { get; set; }
        public int DocId { get; set; }
    }

    public class DocContext(DbContextOptions<DocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Doc>()
                .HasMany(x => x.Notes)
                .WithOne()
                .HasForeignKey(x => x.DocId);
            // the single switch that makes archived rows invisible — root query and included collections alike
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>The ambient "current user" an owner/tenant row-security filter scopes to.</summary>
    public class CurrentOwner
    {
        public string Name { get; set; } = "me";
    }

    /// <summary>
    /// Row security in the same shape as the archive filter: an <see cref="IGlobalFilteredQueryBuilder"/>
    /// running in the same aggregate loop, carrying no metadata that sets it apart. It must keep filtering
    /// no matter what the search object says about archived rows.
    /// </summary>
    public class OwnerFilter(CurrentOwner currentOwner) : GlobalFilteredQueryBuilderBase<IOwnedDoc, int>
    {
        public override IQueryable<IOwnedDoc> Build(IQueryable<IOwnedDoc> query, ISearchObject<int>? so)
            => query.Where(x => x.Owner == currentOwner.Name);
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private readonly CurrentOwner _currentOwner = new();
    private int _liveId;
    private int _plainId;
    private int _archivedId;
    private int _foreignId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _currentOwner.Name = "me";

        var services = new ServiceCollection();
        services.AddDbContext<DocContext>(db => db.UseSqlite(_connection));
        services.AddSingleton(_currentOwner);
        services.UseEntities<DocContext>(o => o.UseDefaults().AddGlobalFilterQueryBuilder<OwnerFilter>())
            .For<Doc>(e => e.Includes((query, _) => query.Include(x => x.Notes)));
        _sp = services.BuildServiceProvider();

        var db = _sp.GetRequiredService<DocContext>();
        await db.Database.EnsureCreatedAsync();
        var live = new Doc
        {
            Title = "live",
            Owner = "me",
            Notes = [new Note { Text = "note-live" }, new Note { Text = "note-archived", IsArchived = true }]
        };
        // no notes: Remove() cascades onto a loaded archivable child graph, which would make the
        // delete tests measure two writes instead of one
        var plain = new Doc { Title = "plain", Owner = "me" };
        var archived = new Doc { Title = "archived", IsArchived = true, Owner = "me" };
        var foreign = new Doc { Title = "foreign", IsArchived = true, Owner = "someone-else" };
        db.Docs.AddRange(live, plain, archived, foreign);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        _liveId = live.Id;
        _plainId = plain.Id;
        _archivedId = archived.Id;
        _foreignId = foreign.Id;
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private IEntityService<Doc, int> Service() => _sp.GetRequiredService<IEntityService<Doc, int>>();
    private IEntityService<Doc, int, SearchObject<int>> SearchService() => _sp.GetRequiredService<IEntityService<Doc, int, SearchObject<int>>>();

    // ── the signal ─────────────────────────────────────────────────────────────

    [Test]
    public void Archived_Survives_Coercion_From_An_Anonymous_Search_Object()
    {
        // every by-id route synthesizes an anonymous search object — the enum must survive the copy
        var so = SearchObjectCoercion.Coerce<SearchObject<int>>(new { id = 7, Archived = ArchivedFilter.Included });

        Assert.Multiple(() =>
        {
            Assert.That(so!.Id, Is.EqualTo(7));
            Assert.That(so.Archived, Is.EqualTo(ArchivedFilter.Included));
        });
    }

    [Test]
    public void A_Null_Archived_Survives_Coercion_Too()
    {
        // Details(id) synthesizes `new { Id, Archived = (ArchivedFilter?)null }` — null must stay null
        // (fall back to the configured default) rather than throw on the conversion
        var so = SearchObjectCoercion.Coerce<SearchObject<int>>(new { id = 7, Archived = (ArchivedFilter?)null });

        Assert.That(so!.Archived, Is.Null);
    }

    [Test]
    public void A_SearchObject_Defaults_To_No_Opinion()
        => Assert.That(new SearchObject<int>().Archived, Is.Null);

    // ── list / search ──────────────────────────────────────────────────────────

    [Test]
    public async Task List_Excludes_Archived_By_Default()
    {
        var items = await SearchService().List(new SearchObject<int>());

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "plain" }));
    }

    [Test]
    public async Task List_With_Archived_Only_Returns_Archived_Only()
    {
        var items = await SearchService().List(new SearchObject<int> { Archived = ArchivedFilter.Only });

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "archived" }),
            "the foreign-owner row stays hidden — the owner filter runs alongside the archive filter");
    }

    [Test]
    public async Task List_With_Archived_Included_Returns_Both()
    {
        var service = SearchService();

        var items = await service.List(new SearchObject<int> { Archived = ArchivedFilter.Included });
        var count = await service.Count(new SearchObject<int> { Archived = ArchivedFilter.Included });

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "plain", "archived" }));
            Assert.That(count, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task An_Explicit_Excluded_Matches_The_Default()
    {
        var items = await SearchService().List(new SearchObject<int> { Archived = ArchivedFilter.Excluded });

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "live", "plain" }));
    }

    // ── included collections ───────────────────────────────────────────────────

    // Include-filtering is the one thing only a model-level query filter can do, so it is net10-only:
    // on net8 no archived query filter is installed (it would force the untargeted IgnoreQueryFilters()
    // on the opt-ins and suspend the app's own row security — see ModelBuilderExtensions), and the
    // root-level predicate the query builder composes instead cannot reach into an Include().
#if NET10_0_OR_GREATER
    [Test]
    public async Task An_Included_Collection_Excludes_Archived_Children_By_Default()
    {
        // Details loads the full graph; the archived note must not come along
        var item = await Service().Details(_liveId);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Notes!.Select(x => x.Text), Is.EquivalentTo(new[] { "note-live" }),
            "an archived child must not be returned inside an included collection");
    }
#endif

    [Test]
    public async Task An_Included_Collection_Returns_Archived_Children_Under_Included()
    {
        var item = await Service().Details(_liveId, ArchivedFilter.Included);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Notes!.Select(x => x.Text), Is.EquivalentTo(new[] { "note-live", "note-archived" }));
    }

#if NET10_0_OR_GREATER
    [Test]
    public async Task A_List_Include_Excludes_Archived_Children_By_Default()
    {
        var items = await SearchService().List(new SearchObject<int>());

        Assert.That(items.Single(x => x.Title == "live").Notes!.Select(x => x.Text), Is.EquivalentTo(new[] { "note-live" }));
    }
#endif

    [Test]
    public async Task A_List_Include_Returns_Archived_Children_Under_Included()
    {
        var items = await SearchService().List(new SearchObject<int> { Archived = ArchivedFilter.Included });

        Assert.That(items.Single(x => x.Title == "live").Notes!.Select(x => x.Text),
            Is.EquivalentTo(new[] { "note-live", "note-archived" }));
    }

    // ── details ────────────────────────────────────────────────────────────────

    [Test]
    public async Task Details_Returns_Null_For_An_Archived_Row()
        => Assert.That(await Service().Details(_archivedId), Is.Null,
            "GET /{id} must keep 404-ing on an archived row");

    [Test]
    public async Task Details_With_Archived_Included_Resolves_An_Archived_Row()
    {
        var item = await Service().Details(_archivedId, ArchivedFilter.Included);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Title, Is.EqualTo("archived"));
    }

    [Test]
    public async Task Details_With_Archived_Only_Resolves_An_Archived_Row()
    {
        var service = Service();

        var archived = await service.Details(_archivedId, ArchivedFilter.Only);
        var live = await service.Details(_liveId, ArchivedFilter.Only);

        Assert.Multiple(() =>
        {
            Assert.That(archived!.Title, Is.EqualTo("archived"));
            Assert.That(live, Is.Null, "Only must not resolve a live row");
        });
    }

    // ── delete ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Delete_Archives_The_Row_And_Reports_The_Real_Count()
    {
        var service = Service();
        var item = (await service.List(new { id = _plainId })).Single();

        await service.Remove(item);
        var affected = await service.SaveChanges();

        var persisted = (await service.List(new { id = _plainId, Archived = ArchivedFilter.Included })).SingleOrDefault();
        Assert.Multiple(() =>
        {
            Assert.That(affected, Is.EqualTo(1), "a soft delete writes exactly one row");
            Assert.That(persisted, Is.Not.Null, "the row must survive a soft delete");
            Assert.That(persisted!.IsArchived, Is.True);
        });
    }

    [Test]
    public async Task A_Second_Delete_Still_Finds_The_Row()
    {
        var service = Service();
        var item = (await service.List(new { id = _plainId })).Single();
        await service.Remove(item);
        await service.SaveChanges();

        // the archived-inclusive lookup is what makes the repeat idempotent instead of a 404
        var again = (await service.List(new { id = _plainId, Archived = ArchivedFilter.Included })).SingleOrDefault();
        Assert.That(again, Is.Not.Null, "a repeated delete must reach the row rather than 404");

        await service.Remove(again!);
        await service.SaveChanges();

        var persisted = (await service.List(new { id = _plainId, Archived = ArchivedFilter.Included })).SingleOrDefault();
        Assert.That(persisted!.IsArchived, Is.True);
    }

    // ── write path ─────────────────────────────────────────────────────────────

    [Test]
    public async Task The_Write_Path_Reaches_An_Archived_Row()
    {
        var service = Service();

        var original = await service.Modify(new Doc { Id = _archivedId, Title = "renamed", IsArchived = true, Owner = "me" });
        await service.SaveChanges();

        Assert.That(original, Is.Not.Null, "Modify must resolve the original of an archived row");
        var persisted = await service.Details(_archivedId, ArchivedFilter.Included);
        Assert.Multiple(() =>
        {
            Assert.That(persisted!.Title, Is.EqualTo("renamed"));
            Assert.That(persisted.IsArchived, Is.True);
        });
    }

    [Test]
    public async Task An_Explicit_IsArchived_False_Restores_The_Row()
    {
        var service = Service();

        await service.Modify(new Doc { Id = _archivedId, Title = "restored", IsArchived = false, Owner = "me" });
        await service.SaveChanges();

        var persisted = await service.Details(_archivedId);
        Assert.That(persisted, Is.Not.Null, "an un-archived row is reachable through the plain read path again");
        Assert.That(persisted!.Title, Is.EqualTo("restored"));
    }

    [Test]
    public async Task The_Existence_Check_Sees_An_Archived_Row()
    {
        var service = Service();

        Assert.Multiple(async () =>
        {
            Assert.That(await service.Count(new { id = _archivedId }), Is.EqualTo(0),
                "the plain by-id count keeps hiding archived rows");
            Assert.That(await service.Count(new { id = _archivedId, Archived = ArchivedFilter.Included }), Is.EqualTo(1),
                "the write-path existence check must see it, or a restore 404s");
        });
    }

    // ── security regression guard ──────────────────────────────────────────────

    [Test]
    public async Task Owner_Filter_Still_Applies_On_A_ById_Write_Path_With_Archived_Included()
    {
        var service = Service();

        // the foreign-owner row is archived, so only the archive opt-in could ever surface it — the owner
        // filter runs in the same loop and must keep it out of every by-id path
        var details = await service.Details(_foreignId, ArchivedFilter.Included);
        var existenceCheck = await service.Count(new { id = _foreignId, Archived = ArchivedFilter.Included });
        var writeLookup = (await service.List(new { id = _foreignId, Archived = ArchivedFilter.Included })).SingleOrDefault();

        // and the write itself must not reach it: Modify's archived-inclusive original fetch is filtered too
        var original = await service.Modify(new Doc { Id = _foreignId, Title = "hijacked", Owner = "me" });
        await service.SaveChanges();

        Assert.Multiple(() =>
        {
            Assert.That(details, Is.Null, "Archived=Included must not bypass owner/tenant row security");
            Assert.That(existenceCheck, Is.EqualTo(0));
            Assert.That(writeLookup, Is.Null);
            Assert.That(original, Is.Null, "the write path must not resolve another owner's row");
        });

        _currentOwner.Name = "someone-else";
        var untouched = await Service().Details(_foreignId, ArchivedFilter.Included);
        Assert.That(untouched!.Title, Is.EqualTo("foreign"), "the foreign row must be unchanged");
    }

    [Test]
    public async Task Owner_Filter_Still_Applies_On_A_List_With_Archived_Only()
    {
        // Only takes the widest route (suspend the archive filter, then re-narrow) — row security must
        // survive it exactly like it survives Included
        var items = await SearchService().List(new SearchObject<int> { Archived = ArchivedFilter.Only });

        Assert.That(items.Select(x => x.Title), Is.EquivalentTo(new[] { "archived" }),
            "the foreign-owner archived row must stay hidden under Archived=Only");
    }
}
