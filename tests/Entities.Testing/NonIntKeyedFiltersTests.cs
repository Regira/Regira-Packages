using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Normalizing;

namespace Entities.Testing;

// Finding 5 (review): a non-int entity registered via UseDefaults() + For<T, TKey>() must get the KEYED
// variants of the default global filters (archivable/created/lastmodified/Q), not only the Ids variant —
// otherwise ?archived=only / ?minCreated= / ?q= silently drop their criteria for that entity.
[TestFixture]
public class NonIntKeyedFiltersTests
{
    public class GuidDoc : IEntity<Guid>, IArchivable, IHasNormalizedContent
    {
        public Guid Id { get; set; }
        [Normalized(SourceProperty = nameof(Title))]
        public string? NormalizedContent { get; set; }
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
    [SetUp]
    public void Setup() { _connection = new SqliteConnection("Filename=:memory:"); _connection.Open(); }
    [TearDown]
    public void TearDown() => _connection.Close();

    private ServiceProvider Build()
    {
        var services = new ServiceCollection();
        services.AddDbContext<GuidDocContext>(db => db.UseSqlite(_connection)); // UseDefaults() auto-wires the interceptors
        services.UseEntities<GuidDocContext>(o => o.UseDefaults()).For<GuidDoc, Guid>();
        return services.BuildServiceProvider();
    }

    [Test]
    public void Keyed_Variants_Of_The_Default_Filters_Are_Registered()
    {
        using var sp = Build();
        var filters = sp.GetServices<IGlobalFilteredQueryBuilder>().Select(f => f.GetType()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(filters, Does.Contain(typeof(FilterIdsQueryBuilder<Guid>)));
            Assert.That(filters, Does.Contain(typeof(FilterArchivablesQueryBuilder<Guid>)));
            Assert.That(filters, Does.Contain(typeof(FilterHasCreatedQueryBuilder<Guid>)));
            Assert.That(filters, Does.Contain(typeof(FilterHasLastModifiedQueryBuilder<Guid>)));
            Assert.That(filters, Does.Contain(typeof(FilterHasNormalizedContentQueryBuilder<Guid>)));
        });
    }

    [Test]
    public async Task Archived_Only_Returns_Archived_Rows_For_A_Guid_Entity()
    {
        using var sp = Build();
        var db = sp.GetRequiredService<GuidDocContext>();
        await db.Database.EnsureCreatedAsync();
        db.Docs.AddRange(
            new GuidDoc { Id = Guid.NewGuid(), Title = "active", IsArchived = false },
            new GuidDoc { Id = Guid.NewGuid(), Title = "archived", IsArchived = true });
        await db.SaveChangesAsync();

        var service = sp.GetRequiredService<IEntityService<GuidDoc, Guid>>();

        var active = await service.List(new SearchObject<Guid>(), new PagingInfo { PageSize = 0 });
        var archived = await service.List(new SearchObject<Guid> { Archived = ArchivedFilter.Only }, new PagingInfo { PageSize = 0 });

        Assert.Multiple(() =>
        {
            Assert.That(active.Select(x => x.Title), Is.EquivalentTo(new[] { "active" }), "default hides archived");
            Assert.That(archived.Select(x => x.Title), Is.EquivalentTo(new[] { "archived" }),
                "?archived=only must return archived rows — the keyed archivable filter honours it");
        });
    }
}
