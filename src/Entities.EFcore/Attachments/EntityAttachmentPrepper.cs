using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Extensions;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentPrepper<TContext, TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(TContext dbContext)
    : EntityPrepperBase<TEntityAttachment>
    where TContext : DbContext
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
{
    public override async Task Prepare(TEntityAttachment item, TEntityAttachment? original, CancellationToken token = default)
    {
        item.Attachment ??= original?.Attachment;
        var replaced = EntityAttachmentContent.PrepareLink(item, original);

        if (item.Attachment?.IsNew() == true)
        {
            // a new file, typed now as the save types it, so the validators judge its type
            EntityAttachmentContent.TypeByName(item.Attachment);
            // the attachment it replaces goes, unless another link still names it
            if (replaced != null
                && !await EntityAttachmentContent.IsLinkedElsewhere<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(
                    dbContext, replaced.Id, [item.Id], token))
            {
                dbContext.Entry(replaced).State = EntityState.Deleted;
            }
        }
        else if (item.Attachment != null)
        {
            EntityAttachmentContent.ApplyBeforeSave(dbContext, item, original?.Attachment);
        }
    }
}