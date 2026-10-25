using System.Text.Json;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// Applies a JSON Merge Patch (RFC 7386) to an existing entity, as <c>PATCH {id}</c> does. The merge is one level deep:
/// a nested object in the patch replaces the stored one whole. The stored entity is
/// serialized as the merge base, patched, and read back as <typeparamref name="TInputDto"/>, so only what the input
/// model carries can change, and its property names have to match the entity's. The merged input is checked
/// (<see cref="DataAnnotationsEntityInputValidator"/>) and then saved by the <see cref="SaveCommand{TEntity,TKey,TDto,TInputDto}"/>
/// handler — the app's override when one is registered — called directly, so the behaviours run once, around the patch.
/// Archived rows can be patched — that is how one is restored. <c>null</c> when the row does not exist.
/// <para>
/// <see cref="Patch"/> has to be a JSON object; anything else is refused with an <c>EntityInputException</c>, a 400. <see cref="SerializerOptions"/> are the ones the merge reads and writes
/// with; the default is <see cref="JsonSerializerDefaults.Web"/>, ignoring cycles, with enums read by name or by number.
/// The endpoints pass the app's JSON options; a sender that relies on converters of the app's own passes them too.
/// </para>
/// </summary>
public sealed record PatchCommand<TEntity, TKey, TDto, TInputDto>(TKey Id, JsonElement Patch, JsonSerializerOptions? SerializerOptions = null)
    : IEntityRequest<SaveResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TDto : class
    where TInputDto : class
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Patch;
}
