using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentWriteService<TContext, TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
(
    TContext dbContext,
    IEntityReadService<TEntityAttachment, TEntityAttachmentKey> readService,
    IEnumerable<IEntityPrepper> preppers,
    IEnumerable<IEntityValidator> validators,
    ILoggerFactory? loggerFactory = null)
    : EntityWriteService<TContext, TEntityAttachment, TEntityAttachmentKey>(dbContext, readService, preppers, validators, loggerFactory)
    where TContext : DbContext
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
{
    /// <summary>
    /// An attachment write service without validators: nothing registered with <c>AddValidator</c> or <c>Validate</c> runs for it.
    /// </summary>
    public EntityAttachmentWriteService(TContext dbContext, IEntityReadService<TEntityAttachment, TEntityAttachmentKey> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : this(dbContext, readService, preppers, [], loggerFactory)
    {
    }

    protected override Task RemoveItem(TEntityAttachment item, CancellationToken token = default)
    {
        // Cannot move this logic to the EntityAttachmentPrimer, since it's using an interface and not the typed Attachment class.
        // Guarded like the link row itself: removing the Attachment principal cascades to the still-tracked
        // link, whose AttachmentId is required, so under DeleteBehavior.Restrict this is the throw site.
        // Marked here rather than in Remove, which validates first: a rejected delete leaves the principal untouched.
        RemoveGuarded(() => DbContext.Remove(item.Attachment!), item.Id);
        return base.RemoveItem(item, token);
    }
}
