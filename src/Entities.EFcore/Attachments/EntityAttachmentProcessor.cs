using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.Models;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentProcessor<TEntityAttachment>(IEntityProcessor<Attachment, EntityIncludes> attachmentProcessor) : EntityAttachmentProcessor<TEntityAttachment, int, int, int, Attachment>(attachmentProcessor)
    where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new();
public class EntityAttachmentProcessor<TEntityAttachment, TKey, TObjectKey, TAttachmentKey, TAttachment>(IEntityProcessor<TAttachment, EntityIncludes> attachmentProcessor) : EntityProcessor<TEntityAttachment, EntityIncludes>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachment : class, IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>
{
    public override Task Process(IList<TEntityAttachment> items, EntityIncludes? includes, CancellationToken token = default)
    {
        var attachments = items.Select(x => x.Attachment!).ToArray();

        return attachmentProcessor.Process(attachments, includes, token);
    }
}