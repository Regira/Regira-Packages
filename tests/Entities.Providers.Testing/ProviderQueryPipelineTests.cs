using Entities.Providers.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;

namespace Entities.Providers.Testing;

/// <summary>
/// Runs the query-pipeline divergence-hotspot suite (global filters, capability-interface sorting,
/// Q LIKE translation, multi-search-object Union, paging) against each EF Core provider.
///
/// Parameterized via <see cref="TestFixtureSource"/> over <see cref="DbProvider"/>:
/// SQLite always runs; PostgreSQL and SQL Server only when <c>REGIRA_PROVIDER_TESTS=containers</c> is set
/// AND Docker is available (otherwise the fixture's OneTimeSetUp calls Assert.Ignore, skipping the whole set).
/// </summary>
[TestFixtureSource(typeof(ProviderFixtureSource))]
public class ProviderQueryPipelineTests(DbProvider provider)
{
    private ProviderHarness _harness = null!;
    private ServiceProvider _serviceProvider = null!;
    private WidgetContext _dbContext = null!;

    // Seed identifiers captured after save so we can assert on stable ids per provider.
    private int _alphaId;
    private int _bravoArchivedId;
    private int _charlieId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _harness = new ProviderHarness(provider);
        // For container providers this calls Assert.Ignore when disabled / Docker missing → whole fixture skips.
        await _harness.InitializeAsync();

        _serviceProvider = _harness.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<WidgetContext>();

        await _dbContext.Database.EnsureCreatedAsync();

        // Titles are chosen so NormalizedTitle ordering is unambiguous: Alpha < Bravo < Charlie.
        // Descriptions carry distinct keywords for the Q/LIKE test; the normalizer builds NormalizedContent.
        var alpha = new Widget
        {
            Title = "Alpha widget",
            Description = "A reliable sprocket for everyday assembly",
            Created = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var bravo = new Widget
        {
            Title = "Bravo widget",
            Description = "A discontinued flange, kept for spares",
            IsArchived = true,
            Created = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        var charlie = new Widget
        {
            Title = "Charlie widget",
            Description = "A premium sprocket with a lifetime warranty",
            Created = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        _dbContext.Widgets.AddRange(alpha, bravo, charlie);
        await _dbContext.SaveChangesAsync();

        _alphaId = alpha.Id;
        _bravoArchivedId = bravo.Id;
        _charlieId = charlie.Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _dbContext?.Dispose();
        _serviceProvider?.Dispose();
        if (_harness is not null)
        {
            await _harness.DisposeAsync();
        }
    }

    private IEntityService<Widget> Service => _serviceProvider.GetRequiredService<IEntityService<Widget>>();

    // ------------------------------------------------------------------
    // Global id filter (FilterIdsQueryBuilder)
    // ------------------------------------------------------------------

    [Test]
    public async Task Details_By_Id_Returns_The_Single_Row()
    {
        var item = await Service.Details(_alphaId);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Id, Is.EqualTo(_alphaId));
        Assert.That(item.Title, Is.EqualTo("Alpha widget"));
    }

    [Test]
    public async Task Filter_By_Ids_Returns_Only_Requested_NonArchived_Rows()
    {
        var items = await Service.List(new SearchObject { Ids = new[] { _alphaId, _charlieId } });

        Assert.That(items.Select(x => x.Id), Is.EquivalentTo(new[] { _alphaId, _charlieId }));
    }

    // ------------------------------------------------------------------
    // Archive global filter (FilterArchivablesQueryBuilder, default behavior)
    // ------------------------------------------------------------------

    [Test]
    public async Task Archived_Rows_Are_Hidden_By_Default()
    {
        var items = await Service.List(new SearchObject());

        Assert.That(items.Select(x => x.Id), Does.Not.Contain(_bravoArchivedId));
        Assert.That(items, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Archived_Only_Returns_Only_Archived_Rows()
    {
        var items = await Service.List(new SearchObject { Archived = ArchivedFilter.Only });

        Assert.That(items.Select(x => x.Id), Is.EqualTo(new[] { _bravoArchivedId }));
    }

    // ------------------------------------------------------------------
    // Capability-interface sorting: ((IHasNormalizedTitle)x).NormalizedTitle cast-in-expression-tree.
    // This is the provider-divergence hotspot — assert it translates AND orders correctly.
    // ------------------------------------------------------------------

    [Test]
    public async Task Capability_Interface_Sort_Orders_By_NormalizedTitle()
    {
        // Non-archived rows, default sort (SortQuery -> OrderOrThenBy(x => ((IHasNormalizedTitle)x).NormalizedTitle)).
        var items = await Service.List(new SearchObject());

        Assert.That(items.Select(x => x.Title), Is.EqualTo(new[] { "Alpha widget", "Charlie widget" }));
    }

    // ------------------------------------------------------------------
    // Q keyword search -> EF.Functions.Like on NormalizedContent (FilterHasNormalizedContentQueryBuilder)
    // ------------------------------------------------------------------

    [Test]
    public async Task Q_Search_Translates_And_Filters_On_NormalizedContent()
    {
        // "sprocket" appears in Alpha and Charlie descriptions (both non-archived), not Bravo.
        var items = await Service.List(new SearchObject { Q = "sprocket" });

        Assert.That(items.Select(x => x.Id), Is.EquivalentTo(new[] { _alphaId, _charlieId }));
    }

    [Test]
    public async Task Q_Search_Narrows_To_A_Single_Row()
    {
        var items = await Service.List(new SearchObject { Q = "warranty" });

        Assert.That(items.Select(x => x.Id), Is.EqualTo(new[] { _charlieId }));
    }

    // ------------------------------------------------------------------
    // Multi-search-object OR via Union (QueryBuilder.FilterList).
    // This is the other provider-divergence hotspot — Union semantics + translation.
    // ------------------------------------------------------------------

    [Test]
    public async Task Multi_SearchObject_Union_Combines_Both_Sets()
    {
        var readService = _serviceProvider.GetRequiredService<IEntityReadService<Widget, int, SearchObject<int>>>();
        var complex = (IEntityReadService<Widget, int, SearchObject<int>, EntitySortBy, EntityIncludes>)readService;

        // One object selects Alpha by id; the other selects Charlie by Q. Union should yield both.
        var searchObjects = new List<SearchObject<int>?>
        {
            new() { Id = _alphaId },
            new() { Q = "warranty" }
        };

        var items = await complex.List(searchObjects, new List<EntitySortBy>());

        Assert.That(items.Select(x => x.Id), Is.EquivalentTo(new[] { _alphaId, _charlieId }));
    }

    [Test]
    public async Task Multi_SearchObject_Union_Deduplicates_Overlap()
    {
        var readService = _serviceProvider.GetRequiredService<IEntityReadService<Widget, int, SearchObject<int>>>();
        var complex = (IEntityReadService<Widget, int, SearchObject<int>, EntitySortBy, EntityIncludes>)readService;

        // Both objects select Alpha; Union must not return it twice.
        var searchObjects = new List<SearchObject<int>?>
        {
            new() { Id = _alphaId },
            new() { Ids = new[] { _alphaId } }
        };

        var items = await complex.List(searchObjects, new List<EntitySortBy>());

        Assert.That(items.Select(x => x.Id), Is.EqualTo(new[] { _alphaId }));
    }

    // ------------------------------------------------------------------
    // Paging + sorting combined
    // ------------------------------------------------------------------

    [Test]
    public async Task Paging_With_Sorting_Returns_Correct_Page()
    {
        // Non-archived rows sorted by NormalizedTitle: [Alpha, Charlie]. Page size 1, page 2 -> Charlie.
        var page2 = await Service.List(new SearchObject(), new PagingInfo { Page = 2, PageSize = 1 });

        Assert.That(page2.Select(x => x.Title), Is.EqualTo(new[] { "Charlie widget" }));

        var page1 = await Service.List(new SearchObject(), new PagingInfo { Page = 1, PageSize = 1 });
        Assert.That(page1.Select(x => x.Title), Is.EqualTo(new[] { "Alpha widget" }));
    }
}
