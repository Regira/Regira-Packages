using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments;

public class EntityAttachmentUriAfterMapper<TEntity, TEntityAttachment, TEntityAttachmentDto, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    (IAttachmentUriResolver<TEntityAttachment> uriResolver) : EntityAfterMapperBase<TEntity, object>
    where TEntity : class, IEntity<TObjectKey>, IHasAttachments<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachmentDto : EntityAttachmentDto
{
    public override void AfterMap(TEntity source, object target)
    {
        var attachmentsProp = source.GetType().GetProperty(nameof(IHasAttachments.Attachments));
        var attachmentsValues = attachmentsProp?.GetValue(source);
        if (attachmentsValues == null)
        {
            return;
        }

        var dtoAttachmentsProp = target.GetType().GetProperty(nameof(IHasAttachments.Attachments));
        // iterate loosely typed so consumers can use a derived DTO collection (ICollection<T> is invariant)
        var rawDtoAttachments = (dtoAttachmentsProp?.GetValue(target) as System.Collections.IEnumerable)?.Cast<object>().ToList();
        if (rawDtoAttachments == null)
        {
            return;
        }
        var dtoAttachments = rawDtoAttachments.OfType<EntityAttachmentDto>().ToList();
        if (rawDtoAttachments.Count > 0 && dtoAttachments.Count == 0)
        {
            // fail loudly — silently skipping would ship every response with Uri = null and nothing in the logs
            throw new InvalidOperationException(
                $"{target.GetType().Name}.{nameof(IHasAttachments.Attachments)} items are {rawDtoAttachments[0].GetType().Name}, " +
                $"which does not derive from {nameof(EntityAttachmentDto)} — attachment Uris cannot be resolved.");
        }

        var attachments = ((System.Collections.IEnumerable)attachmentsValues).OfType<TEntityAttachment>().ToList();
        // key by the string form: TEntityAttachmentKey need not be int (the DTO's Id is), and a boxed
        // Equals(long, int) is always false — "5" == "5" pairs them regardless of the numeric key type
        var attachmentsById = attachments
            .Where(a => a.Id != null)
            .GroupBy(a => a.Id!.ToString()!)
            .ToDictionary(g => g.Key, g => g.First());

        // pass 1 — persisted DTOs (real Id) pair by Id only: an unmatched Id gets NO Uri rather than some other attachment's
        var matchedIds = new HashSet<string>();
        var unsavedDtos = new List<EntityAttachmentDto>();
        foreach (var dto in dtoAttachments)
        {
            if (dto.Id != 0)
            {
                var idKey = dto.Id.ToString();
                if (attachmentsById.TryGetValue(idKey, out var attachment))
                {
                    dto.Uri = uriResolver.Resolve(attachment);
                    matchedIds.Add(idKey);
                }
            }
            else
            {
                unsavedDtos.Add(dto);
            }
        }

        // pass 2 — unsaved DTOs (default Id) pair with the LEFTOVER attachments in order; both sides keep
        // their relative order, so a mixed saved/unsaved list can't cross-assign another row's Uri
        var leftoverAttachments = attachments.Where(a => a.Id == null || !matchedIds.Contains(a.Id.ToString()!)).ToList();
        for (var i = 0; i < unsavedDtos.Count && i < leftoverAttachments.Count; i++)
        {
            unsavedDtos[i].Uri = uriResolver.Resolve(leftoverAttachments[i]);
        }
    }
}
