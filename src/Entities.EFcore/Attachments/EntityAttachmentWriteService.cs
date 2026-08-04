using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentWriteService<TContext, TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
(
    TContext dbContext,
    IEntityReadService<TEntityAttachment, TEntityAttachmentKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntityAttachment, TEntityAttachmentKey>(dbContext, readService, preppers, loggerFactory)
    where TContext : DbContext
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
{
    public override Task Remove(TEntityAttachment item, CancellationToken token = default)
    {
        // Cannot move this logic to the EntityAttachmentPrimer, since it's using an interface and not the typed Attachment class
        DbContext.Remove(item.Attachment!);
        return base.Remove(item);
    }
}
