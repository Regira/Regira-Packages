using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web;

namespace Entities.Web.Testing;

/// <summary>
/// A wrapping service (or a derived repository) that overrides <c>Details(id)</c> — to add authorization,
/// caching or auditing — must not be skipped by the archived-inclusive overload. Every save re-fetches
/// through <see cref="EntitySaveHelper.ResolveSavedItem{TEntity,TKey}"/> with the opt-in on, so an ordinary
/// <c>PUT</c> would otherwise bypass the wrapper entirely.
/// <para>
/// The opt-in only changes the query for an <see cref="IArchivable"/> entity — that is the one call shape
/// which still goes straight to the inner service (<see cref="A_Real_Archived_Read_Goes_To_The_Inner_Service"/>).
/// </para>
/// </summary>
public class WrappingServiceDetailsDispatchTests
{
    private sealed class Item : IEntity<int>
    {
        public int Id { get; set; }
    }

    private sealed class ArchivableItem : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }
    }

    /// <summary>Inner service recording which <c>Details</c> overload the wrapper reached it through.</summary>
    private sealed class RecordingInnerService<TEntity> : IEntityService<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>
        where TEntity : class, IEntity<int>, new()
    {
        public int DetailsCalls { get; private set; }
        public int ArchivedInclusiveDetailsCalls { get; private set; }
        public TEntity Fetched { get; } = new() { Id = 1 };

        public Task<TEntity?> Details(int id, CancellationToken token = default)
        {
            DetailsCalls++;
            return Task.FromResult<TEntity?>(Fetched);
        }
        public Task<TEntity?> Details(int id, ArchivedFilter? archived, CancellationToken token = default)
        {
            ArchivedInclusiveDetailsCalls++;
            return Task.FromResult<TEntity?>(Fetched);
        }

        public Task<IList<TEntity>> List(object? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
            => Task.FromResult<IList<TEntity>>([]);
        public Task<IList<TEntity>> List(SearchObject<int>? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
            => Task.FromResult<IList<TEntity>>([]);
        public Task<IList<TEntity>> List(IList<SearchObject<int>?> so, IList<EntitySortBy> sortBy, EntityIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
            => Task.FromResult<IList<TEntity>>([]);
        public Task<long> Count(object? so, CancellationToken token = default) => Task.FromResult(0L);
        public Task<long> Count(SearchObject<int>? so = null, CancellationToken token = default) => Task.FromResult(0L);
        public Task<long> Count(IList<SearchObject<int>?> so, CancellationToken token = default) => Task.FromResult(0L);

        public Task Add(TEntity item, CancellationToken token = default) => Task.CompletedTask;
        public Task<TEntity?> Modify(TEntity item, CancellationToken token = default) => Task.FromResult<TEntity?>(item);
        public Task Save(TEntity item, CancellationToken token = default) => Task.CompletedTask;
        public Task Remove(TEntity item, CancellationToken token = default) => Task.CompletedTask;
        public Task<int> SaveChanges(CancellationToken token = default) => Task.FromResult(1);
    }

    /// <summary>The consumer shape at issue: only the single-argument <c>Details</c> is overridden.</summary>
    private sealed class WrappingService(IEntityService<Item, int, SearchObject<int>> service)
        : EntityWrappingServiceBase<Item, int, SearchObject<int>>(service)
    {
        public int OverrideCalls { get; private set; }
        public override Task<Item?> Details(int id, CancellationToken token = default)
        {
            OverrideCalls++;
            return base.Details(id, token);
        }
    }

    private sealed class ArchivableWrappingService(IEntityService<ArchivableItem, int, SearchObject<int>> service)
        : EntityWrappingServiceBase<ArchivableItem, int, SearchObject<int>>(service)
    {
        public int OverrideCalls { get; private set; }
        public override Task<ArchivableItem?> Details(int id, CancellationToken token = default)
        {
            OverrideCalls++;
            return base.Details(id, token);
        }
    }

    private sealed class ComplexWrappingService(IEntityService<Item, int, SearchObject<int>, EntitySortBy, EntityIncludes> service)
        : EntityWrappingServiceBase<Item, int, SearchObject<int>, EntitySortBy, EntityIncludes>(service)
    {
        public int OverrideCalls { get; private set; }
        public override Task<Item?> Details(int id, CancellationToken token = default)
        {
            OverrideCalls++;
            return base.Details(id, token);
        }
    }

    /// <summary>A custom repository (<c>e.HasRepository&lt;T&gt;()</c>) — the same override shape.</summary>
    private sealed class CustomRepository(IEntityReadService<Item, int, SearchObject<int>> readService, IEntityWriteService<Item, int> writeService)
        : EntityRepository<Item, int, SearchObject<int>>(readService, writeService)
    {
        public int OverrideCalls { get; private set; }
        public override Task<Item?> Details(int id, CancellationToken token = default)
        {
            OverrideCalls++;
            return base.Details(id, token);
        }
    }

    private static IServiceProvider EmptyProvider() => new ServiceCollection().BuildServiceProvider();

    [Fact]
    public async Task Details_Without_An_Archived_Filter_Runs_The_Override()
    {
        var inner = new RecordingInnerService<Item>();
        var wrapper = new WrappingService(inner);

        await wrapper.Details(1, (ArchivedFilter?)null);

        Assert.Equal(1, wrapper.OverrideCalls);
        Assert.Equal(1, inner.DetailsCalls);
        Assert.Equal(0, inner.ArchivedInclusiveDetailsCalls);
    }

    [Fact]
    public async Task Details_With_An_Inert_Archived_Filter_Runs_The_Override()
    {
        var inner = new RecordingInnerService<Item>();
        var wrapper = new WrappingService(inner);

        // Item is not IArchivable, so nothing reads the flag — the wrapper must stay on its own read path
        await wrapper.Details(1, ArchivedFilter.Included);

        Assert.Equal(1, wrapper.OverrideCalls);
        Assert.Equal(1, inner.DetailsCalls);
    }

    [Fact]
    public async Task The_PostSave_Refetch_Runs_The_Override()
    {
        var inner = new RecordingInnerService<Item>();
        var wrapper = new WrappingService(inner);

        // what every PUT/PATCH/POST does after SaveChanges — archived-inclusive, on every entity
        var saved = await EntitySaveHelper.ResolveSavedItem(EmptyProvider(), wrapper, new Item { Id = 1 });

        Assert.Equal(1, wrapper.OverrideCalls);
        Assert.Same(inner.Fetched, saved);
    }

    [Fact]
    public async Task The_Complex_Variant_Dispatches_The_Same_Way()
    {
        var inner = new RecordingInnerService<Item>();
        var wrapper = new ComplexWrappingService(inner);

        await wrapper.Details(1, (ArchivedFilter?)null);
        await wrapper.Details(1, ArchivedFilter.Included);
        await EntitySaveHelper.ResolveSavedItem(EmptyProvider(), wrapper, new Item { Id = 1 });

        Assert.Equal(3, wrapper.OverrideCalls);
    }

    [Fact]
    public async Task A_Custom_Repository_Dispatches_The_Same_Way()
    {
        var inner = new RecordingInnerService<Item>();
        var repository = new CustomRepository(inner, inner);

        await repository.Details(1, (ArchivedFilter?)null);
        await repository.Details(1, ArchivedFilter.Included);
        await EntitySaveHelper.ResolveSavedItem(EmptyProvider(), repository, new Item { Id = 1 });

        Assert.Equal(3, repository.OverrideCalls);
    }

    [Fact]
    public async Task A_Real_Archived_Read_Goes_To_The_Inner_Service()
    {
        var inner = new RecordingInnerService<ArchivableItem>();
        var wrapper = new ArchivableWrappingService(inner);

        // the opt-in genuinely widens the query here, so only the inner service can answer it — a wrapper
        // that must cover archived reads too overrides this overload as well
        await wrapper.Details(1, ArchivedFilter.Included);

        Assert.Equal(0, wrapper.OverrideCalls);
        Assert.Equal(1, inner.ArchivedInclusiveDetailsCalls);
    }
}
