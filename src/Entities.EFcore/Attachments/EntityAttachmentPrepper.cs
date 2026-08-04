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

        if (item.Attachment?.IsNew() == true && original?.Attachment != null)
        {
            dbContext.Entry(original.Attachment).State = EntityState.Deleted;
        }

        return Task.CompletedTask;
    }
}