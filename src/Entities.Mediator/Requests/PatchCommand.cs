using System.Text.Json;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// Applies a JSON Merge Patch (RFC 7386) to an existing entity, as <c>PATCH {id}</c> does. The stored entity is
/// serialized as the merge base, patched, and read back as <typeparamref name="TInputDto"/>, so only what the input
/// model carries can change, and its property names have to match the entity's. The merged input's DataAnnotations are
/// checked (<see cref="IEntityInputValidator"/>) before it is saved as a <see cref="SaveCommand{TEntity,TKey,TDto,TInputDto}"/>
/// would save it. Archived rows can be patched — that is how one is restored. <c>null</c> when the row does not exist.
/// <para>
/// <see cref="Patch"/> has to be a JSON object. <see cref="SerializerOptions"/> are the ones the merge reads and writes
/// with; the default is <see cref="JsonSerializerDefaults.Web"/>, ignoring cycles.
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
