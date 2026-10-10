using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// Deletes one entity, as <c>DELETE {id}</c> does. For an <c>IArchivable</c> entity this is a soft delete, and the
/// lookup includes archived rows, so a repeated delete answers again instead of <c>null</c>. <c>Affected</c> is the real
/// number of rows written. <c>null</c> when the row does not exist.
/// </summary>
public sealed record DeleteCommand<TEntity, TKey, TDto>(TKey Id)
    : IEntityRequest<DeleteResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Delete;
}
