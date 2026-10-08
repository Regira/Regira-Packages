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
    public override Task Prepare(TEntityAttachment item, TEntityAttachment? original, CancellationToken token = default)
    {
        item.Attachment ??= original?.Attachment;
        EntityAttachmentContent.CreateFromNewContent(item);

        if (item.Attachment?.IsNew() == true)
        {
            // a new file, typed now as the save types it, so the validators judge its type
            EntityAttachmentContent.TypeByName(item.Attachment);
            if (original?.Attachment != null)
            {
                dbContext.Entry(original.Attachment).State = EntityState.Deleted;
            }
        }
        else
        {
            if (original != null)
            {
                EntityAttachmentContent.KeepAttachment(item, original);
            }
            if (item.Attachment != null)
            {
                EntityAttachmentContent.ApplyBeforeSave(dbContext, item, original?.Attachment);
            }
        }

        return Task.CompletedTask;
    }
}