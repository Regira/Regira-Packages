using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of <see cref="DetailsQuery{TEntity,TKey,TDto}"/>.</summary>
public class DetailsHandler<TEntity, TKey, TDto>(IServiceProvider services)
    : IEntityRequestHandler<DetailsQuery<TEntity, TKey, TDto>, DetailsResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<DetailsResult<TDto>?> Handle(DetailsQuery<TEntity, TKey, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        var item = await service.Details(request.Id, request.Archived, token);
        if (item == null)
        {
            return null;
        }

        return new DetailsResult<TDto> { Item = Services.GetMapper().Map<TDto>(item) };
    }
}
