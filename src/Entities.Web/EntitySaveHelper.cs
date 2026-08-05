using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Collections.Concurrent;

namespace Regira.Entities.Web;

/// <summary>Shared save-endpoint behavior at the HTTP boundary — used by the MVC controllers, reusable by any other HTTP surface.</summary>
public static class EntitySaveHelper
{
    private static readonly ConcurrentDictionary<(Type Entity, Type InputDto), bool> PreservesArchivedStateCache = new();

    /// <summary>
    /// The existence check for an update, always archived-inclusive: an archived row must stay writable
    /// (that is how a restore works) while <c>GET /{id}</c> keeps 404-ing on it. The lookup goes through
    /// the regular filtered query, so every other global filter (tenant/owner row security) still applies —
    /// only the built-in <c>IArchivable</c> filter reads <see cref="ISearchObject.Archived"/>, and no EF
    /// query filter the app configured itself is ever suspended on either target framework (see
    /// <c>QueryExtensions.FilterArchivable</c>).
    /// <para>
    /// When <typeparamref name="TEntity"/> is <see cref="IArchivable"/> but <typeparamref name="TInputDto"/>
    /// carries no <c>IsArchived</c> property, the persisted flag is copied onto <paramref name="item"/>:
    /// the mapper constructs a fresh entity and the write service marks every scalar dirty, so an
    /// input model that cannot express <c>IsArchived</c> would otherwise silently un-archive the row.
    /// An input model that <em>does</em> declare <c>IsArchived</c> stays the write boundary — sending
    /// <c>false</c> un-archives.
    /// </para>
    /// </summary>
    /// <returns><c>false</c> when the row does not exist (the caller answers 404).</returns>
    public static async Task<bool> ResolveExistingForWrite<TEntity, TKey, TInputDto>(IEntityService<TEntity, TKey> service, TEntity item, CancellationToken token = default)
        where TEntity : class, IEntity<TKey>
    {
        if (!PreservesArchivedState<TEntity, TInputDto>())
        {
            return await service.Count(new { item.Id, Archived = ArchivedFilter.Included }, token) == 1;
        }

        var persisted = (await service.List(new { item.Id, Archived = ArchivedFilter.Included }, new PagingInfo { PageSize = 1 }, token)).SingleOrDefault();
        if (persisted == null)
        {
            return false;
        }

        ((IArchivable)item).IsArchived = ((IArchivable)persisted).IsArchived;
        return true;
    }

    /// <summary>
    /// Whether the persisted <c>IsArchived</c> must survive a write through <typeparamref name="TInputDto"/>:
    /// the entity is archivable but the input model has no way to express the flag.
    /// </summary>
    private static bool PreservesArchivedState<TEntity, TInputDto>()
        => PreservesArchivedStateCache.GetOrAdd((typeof(TEntity), typeof(TInputDto)), key =>
            typeof(IArchivable).IsAssignableFrom(key.Entity)
            && key.InputDto.GetProperties().All(p => !p.Name.Equals(nameof(IArchivable.IsArchived), StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// The saved entity for the response: re-fetched via <c>Details(id)</c> (the default), or the saved
    /// input when <see cref="EntityReadOptions.RefetchAfterSave"/> is <c>Never</c> — or
    /// <c>WhenProcessorsRegistered</c> while no <c>IEntityProcessor</c> is registered for the entity.
    /// </summary>
    public static async Task<TEntity?> ResolveSavedItem<TEntity, TKey>(IServiceProvider serviceProvider, IEntityService<TEntity, TKey> service, TEntity item, CancellationToken token = default)
        where TEntity : class, IEntity<TKey>
    {
        var options = (EntityReadOptions?)serviceProvider.GetService<EntityReadOptions<TEntity>>() ?? serviceProvider.GetService<EntityReadOptions>();
        return options?.RefetchAfterSave switch
        {
            RefetchAfterSave.Never => item,
            RefetchAfterSave.WhenProcessorsRegistered when !HasProcessors<TEntity>(serviceProvider) => item,
            // Archived-inclusive: this re-fetch belongs to the write path, so a row that stays (or becomes)
            // archived must still come back processed rather than falling through to the raw input. Only the
            // archived flag is widened — row security (global filters and the app's own EF query filters)
            // keeps constraining the re-fetch.
            // Fall back to the just-saved input if the row was deleted between SaveChanges and the re-fetch
            // (concurrent delete) — the response then still carries a value instead of throwing on a null map.
            _ => await service.Details(item.Id, ArchivedFilter.Included, token) ?? item
        };
    }

    private static bool HasProcessors<TEntity>(IServiceProvider serviceProvider)
    {
        var services = serviceProvider.GetService<IServiceCollection>();
        if (services == null)
        {
            return true; // can't inspect the registrations — re-fetch to stay safe
        }
        return services.Any(d =>
            d.ServiceType.IsGenericType
            && d.ServiceType.GetGenericTypeDefinition() == typeof(IEntityProcessor<,>)
            // an open-generic processor registration (IEntityProcessor<,> applied to all entities) has a
            // type PARAMETER at [0], not typeof(TEntity) — count it (conservatively re-fetch) rather than miss it
            && (d.ServiceType.IsGenericTypeDefinition
                || d.ServiceType.GetGenericArguments()[0] == typeof(TEntity)));
    }
}
