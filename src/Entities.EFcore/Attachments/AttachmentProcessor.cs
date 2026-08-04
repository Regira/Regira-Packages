using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Models;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentProcessor(IAttachmentFileService<Attachment, int> fileService) : AttachmentProcessor<Attachment, int>(fileService);
public class AttachmentProcessor<TAttachment, TKey>(IAttachmentFileService<TAttachment, TKey> fileService) : EntityProcessor<TAttachment, EntityIncludes>
    where TAttachment : class, IAttachment<TKey>, new()
{
    public override async Task Process(IList<TAttachment> items, EntityIncludes? includes, CancellationToken token = default)
    {
        foreach (var item in items)
        {
            item.Identifier = fileService.GetIdentifier(item.Path!);
            item.Prefix = fileService.GetRelativeFolder(item);

            if (includes?.HasFlag(EntityIncludes.All) == true)
            {
                item.Bytes = await fileService.GetBytes(item, token);
            }
        }
    }
}