using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// Creates or updates an entity from <see cref="Input"/>, as <c>POST</c>, <c>PUT {id}</c> and <c>POST save</c> do: a
/// non-default <see cref="Id"/> targets that row, otherwise the input's own id decides. The answer is the saved entity
/// re-read and mapped to <typeparamref name="TDto"/> (see <c>EntityReadOptions.RefetchAfterSave</c>); <c>null</c> when
/// the row to update does not exist. An update keeps a persisted <c>IsArchived</c> that the input cannot express.
/// <para>
/// <see cref="ValidateInput"/> checks the input's DataAnnotations first (<see cref="IEntityInputValidator"/>). The
/// controllers turn it off: MVC has validated the request body by then, and an app that suppressed that answer chose to.
/// </para>
/// </summary>
public sealed record SaveCommand<TEntity, TKey, TDto, TInputDto>(TInputDto Input, TKey? Id = default, bool ValidateInput = true)
    : IEntityRequest<SaveResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Save;
}
