using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>What the default handlers share: the mapper, the paging defaults, the input check and the save itself.</summary>
internal static class EntityHandlerServices
{
    public static IEntityMapper GetMapper(this IServiceProvider services) => services.GetRequiredService<IEntityMapper>();

    /// <summary>
    /// The effective paging from the configured <see cref="EntityListOptions"/>; a per-entity
    /// <see cref="EntityListOptions{TEntity}"/>, when registered, replaces the global options (see
    /// <see cref="EntityListOptionsExtensions.ApplyPagingDefaults"/>). A direct service call keeps full control.
    /// </summary>
    public static PagingInfo? WithPagingDefaults<TEntity>(this IServiceProvider services, PagingInfo? paging)
        where TEntity : class
    {
        var options = (EntityListOptions?)services.GetService<EntityListOptions<TEntity>>() ?? services.GetService<EntityListOptions>();
        return paging.ApplyPagingDefaults(options);
    }

    /// <summary>Throws the input's DataAnnotations errors as one <see cref="EntityInputException{T}"/>, the body of a validator's 400.</summary>
    public static void ThrowIfInvalidInput<TEntity>(this IServiceProvider services, object input)
    {
        var validator = services.GetService<IEntityInputValidator>() ?? DataAnnotationsEntityInputValidator.Instance;
        var errors = validator.Validate(input);
        if (errors.Count > 0)
        {
            throw new EntityInputException<TEntity>("The input is not valid.") { Errors = [.. errors] };
        }
    }

    /// <summary>
    /// Maps <paramref name="input"/> to a new or existing <typeparamref name="TEntity"/>, saves it and answers with it
    /// re-read; <c>null</c> when the row to update does not exist.
    /// </summary>
    public static async Task<SaveResult<TDto>?> Save<TEntity, TKey, TDto, TInputDto>(this IServiceProvider services, TInputDto input, TKey? id, CancellationToken token)
        where TEntity : class, IEntity<TKey>
    {
        var mapper = services.GetMapper();
        var item = mapper.Map<TEntity>(input!);
        if (!id?.Equals(default(TKey)) ?? false)
        {
            item.Id = id!;
        }
        var isNew = item.IsNew();

        var service = services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        if (!isNew)
        {
            // archived-inclusive row lookup + preservation of a persisted IsArchived that TInputDto
            // cannot express (see EntitySaveHelper.ResolveExistingForWrite)
            var exists = await EntitySaveHelper.ResolveExistingForWrite<TEntity, TKey, TInputDto>(service, item, token);
            if (!exists)
            {
                return null;
            }
        }

        await service.Save(item, token);
        var affected = await service.SaveChanges(token);

        var savedItem = await EntitySaveHelper.ResolveSavedItem(services, service, item, token);
        return new SaveResult<TDto> { Item = mapper.Map<TDto>(savedItem!), Affected = affected, IsNew = isNew };
    }
}
