using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of <see cref="SaveCommand{TEntity,TKey,TDto,TInputDto}"/>.</summary>
public class SaveHandler<TEntity, TKey, TDto, TInputDto>(IServiceProvider services)
    : IEntityRequestHandler<SaveCommand<TEntity, TKey, TDto, TInputDto>, SaveResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SaveResult<TDto>?> Handle(SaveCommand<TEntity, TKey, TDto, TInputDto> request, CancellationToken token = default)
    {
        if (request.ValidateInput && request.Input is { } input)
        {
            Services.ThrowIfInvalidInput<TEntity>(input);
        }
        return await Services.Save<TEntity, TKey, TDto, TInputDto>(request.Input, request.Id, token);
    }
}
