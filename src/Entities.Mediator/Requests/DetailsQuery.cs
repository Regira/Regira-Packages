using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// One entity by id, as <c>GET {id}</c> answers it: mapped to <typeparamref name="TDto"/>, <c>null</c> when it does not
/// exist. Archived rows count as missing unless <see cref="Archived"/> opts in (<c>Included</c> or <c>Only</c>).
/// </summary>
public sealed record DetailsQuery<TEntity, TKey, TDto>(TKey Id, ArchivedFilter? Archived = null)
    : IEntityRequest<DetailsResult<TDto>>
    where TEntity : class, IEntity<TKey>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Details;
}
