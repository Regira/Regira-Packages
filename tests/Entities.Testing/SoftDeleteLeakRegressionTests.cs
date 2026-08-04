using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// Regression guard for the soft-delete leak: a non-int-keyed <see cref="IArchivable"/> entity registered
/// through <c>UseDefaults()</c> (which registers only the int-keyed archive filter) must still hide
/// archived rows. The global-filter dispatch must not step aside and return the query unfiltered just
/// because the search object's key type doesn't match the (int) filter's key type.
/// </summary>
[TestFixture]
public class SoftDeleteLeakRegressionTests
{
    public class GuidDoc : IEntity<Guid>, IArchivable
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    public class GuidDocContext(DbContextOptions<GuidDocContext> options) : DbContext(options)
    {
        public DbSet<GuidDoc> Docs => Set<GuidDoc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private Guid _archivedId;
    private Guid _liveId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<GuidDocContext>(db => db.UseSqlite(_connection));
        // The canonical bootstrap: UseDefaults() (registers the int-keyed default global filters) + For<Guid>.
        services.UseEntities<GuidDocContext>(o => o.UseDefaults())
            .For<GuidDoc, Guid>();
        _sp = services.BuildServiceProvider();

        var db = _sp.GetRequiredService<GuidDocContext>();
        await db.Database.EnsureCreatedAsync();
        _liveId = Guid.NewGuid();
        _archivedId = Guid.NewGuid();
        db.Docs.AddRange(
            new GuidDoc { Id = _liveId, Title = "live", IsArchived = false },
            new GuidDoc { Id = _archivedId, Title = "archived", IsArchived = true });
        await db.SaveChangesAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    // Details passes a non-null SearchObject<Guid> (Convert(new { Id })) into the global-filter
    // dispatch — the case where a key-type mismatch previously made the archive filter step aside.
    [Test]
    public async Task Details_Returns_Null_For_Archived_Guid_Row()
    {
        var service = _sp.GetRequiredService<IEntityService<GuidDoc, Guid>>();

        var archived = await service.Details(_archivedId);
        var live = await service.Details(_liveId);

        Assert.That(archived, Is.Null, "an archived row must not be returned by Details for a Guid-keyed entity");
        Assert.That(live, Is.Not.Null);
    }

    [Test]
    public async Task List_With_Explicit_SearchObject_Hides_Archived_Guid_Rows()
    {
        var service = _sp.GetRequiredService<IEntityService<GuidDoc, Guid, SearchObject<Guid>>>();

        var items = await service.List(new SearchObject<Guid>());

        Assert.That(items, Has.Count.EqualTo(1), "archived rows must not leak for a Guid-keyed IArchivable entity");
        Assert.That(items[0].IsArchived, Is.False);
    }
}
