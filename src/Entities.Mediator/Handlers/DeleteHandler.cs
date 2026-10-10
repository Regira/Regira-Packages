using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of <see cref="DeleteCommand{TEntity,TKey,TDto}"/>.</summary>
public class DeleteHandler<TEntity, TKey, TDto>(IServiceProvider services)
    : IEntityRequestHandler<DeleteCommand<TEntity, TKey, TDto>, DeleteResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<DeleteResult<TDto>?> Handle(DeleteCommand<TEntity, TKey, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        // Archived-inclusive widens nothing but the archived flag — the lookup is the regular filtered query, so
        // tenant/owner row security (global filters and the app's own EF query filters) still constrains it
        var item = (await service.List(new { id = request.Id, Archived = ArchivedFilter.Included }, token: token)).SingleOrDefault();
        if (item == null)
        {
            return null;
        }

        await service.Remove(item, token);
        var affected = await service.SaveChanges(token);

        return new DeleteResult<TDto> { Item = Services.GetMapper().Map<TDto>(item), Affected = affected };
    }
}
