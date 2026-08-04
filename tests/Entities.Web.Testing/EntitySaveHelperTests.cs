using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web;

namespace Entities.Web.Testing;

/// <summary>
/// Tests for <see cref="EntitySaveHelper.ResolveSavedItem{TEntity,TKey}"/> — the RefetchAfterSave
/// behavior shared by the MVC Save extension and the FastEndpoints auto-endpoints.
/// </summary>
public class EntitySaveHelperTests
{
    private sealed class Item : IEntity<int>
    {
        public int Id { get; set; }
    }

    private sealed class RecordingService : IEntityService<Item, int>
    {
        public int DetailsCalls { get; private set; }
        public Item Fetched { get; } = new() { Id = 1 };

        public Task<Item?> Details(int id, CancellationToken token = default)
        {
            DetailsCalls++;
            return Task.FromResult<Item?>(Fetched);
        }
        public Task<IList<Item>> List(object? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
            => Task.FromResult<IList<Item>>([]);
        public Task<long> Count(object? so, CancellationToken token = default) => Task.FromResult(0L);
        public Task Add(Item item, CancellationToken token = default) => Task.CompletedTask;
        public Task<Item?> Modify(Item item, CancellationToken token = default) => Task.FromResult<Item?>(item);
        public Task Save(Item item, CancellationToken token = default) => Task.CompletedTask;
        public Task Remove(Item item, CancellationToken token = default) => Task.CompletedTask;
        public Task<int> SaveChanges(CancellationToken token = default) => Task.FromResult(1);
    }

    private sealed class NoopProcessor : IEntityProcessor<Item, EntityIncludes>
    {
        public Task Process(IList<Item> items, EntityIncludes? includes, CancellationToken token = default) => Task.CompletedTask;
    }

    // An open-generic processor applying to every entity (registered as IEntityProcessor<,>).
    private sealed class OpenProcessor<TEntity, TIncludes> : IEntityProcessor<TEntity, TIncludes>
        where TIncludes : struct, Enum
    {
        public Task Process(IList<TEntity> items, TIncludes? includes, CancellationToken token = default) => Task.CompletedTask;
    }

    private static IServiceProvider BuildProvider(RefetchAfterSave? behavior, bool withProcessor = false, bool withOpenGenericProcessor = false)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IServiceCollection>(services);
        if (behavior != null)
        {
            services.AddSingleton(new EntityReadOptions { RefetchAfterSave = behavior.Value });
        }
        if (withProcessor)
        {
            services.AddTransient<IEntityProcessor<Item, EntityIncludes>, NoopProcessor>();
        }
        if (withOpenGenericProcessor)
        {
            services.AddTransient(typeof(IEntityProcessor<,>), typeof(OpenProcessor<,>));
        }
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Default_Refetches_Via_Details()
    {
        var service = new RecordingService();
        var input = new Item { Id = 1 };

        var saved = await EntitySaveHelper.ResolveSavedItem(BuildProvider(null), service, input);

        Assert.Equal(1, service.DetailsCalls);
        Assert.Same(service.Fetched, saved);
    }

    [Fact]
    public async Task Never_Returns_The_Saved_Input_Without_Fetching()
    {
        var service = new RecordingService();
        var input = new Item { Id = 1 };

        var saved = await EntitySaveHelper.ResolveSavedItem(BuildProvider(RefetchAfterSave.Never), service, input);

        Assert.Equal(0, service.DetailsCalls);
        Assert.Same(input, saved);
    }

    [Fact]
    public async Task WhenProcessorsRegistered_Skips_Fetch_Without_Processors()
    {
        var service = new RecordingService();
        var input = new Item { Id = 1 };

        var saved = await EntitySaveHelper.ResolveSavedItem(BuildProvider(RefetchAfterSave.WhenProcessorsRegistered), service, input);

        Assert.Equal(0, service.DetailsCalls);
        Assert.Same(input, saved);
    }

    [Fact]
    public async Task WhenProcessorsRegistered_Fetches_With_Processors()
    {
        var service = new RecordingService();
        var input = new Item { Id = 1 };

        var saved = await EntitySaveHelper.ResolveSavedItem(BuildProvider(RefetchAfterSave.WhenProcessorsRegistered, withProcessor: true), service, input);

        Assert.Equal(1, service.DetailsCalls);
        Assert.Same(service.Fetched, saved);
    }

    // Lower-confidence panel item: an open-generic processor applies to every entity, so the refetch
    // must happen — the presence check used to compare an open generic's type PARAMETER to typeof(Item)
    // and miss it.
    [Fact]
    public async Task WhenProcessorsRegistered_Fetches_With_An_OpenGeneric_Processor()
    {
        var service = new RecordingService();
        var input = new Item { Id = 1 };

        var saved = await EntitySaveHelper.ResolveSavedItem(BuildProvider(RefetchAfterSave.WhenProcessorsRegistered, withOpenGenericProcessor: true), service, input);

        Assert.Equal(1, service.DetailsCalls);
        Assert.Same(service.Fetched, saved);
    }
}
