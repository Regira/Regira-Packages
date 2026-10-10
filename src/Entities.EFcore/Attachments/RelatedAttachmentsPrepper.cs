using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.IO.Extensions;
using System.Linq.Expressions;

namespace Regira.Entities.EFcore.Attachments;

public class RelatedAttachmentsPrepper<TContext, TEntity, TEntityAttachment, TEntityKey, TEntityAttachmentKey, TAttachmentKey, TAttachment>(
        TContext dbContext,
        Expression<Func<TEntity, ICollection<TEntityAttachment>?>> navigationExpression,
        RelatedAttachmentsPrepper<TContext, TEntity, TEntityAttachment, TEntityKey, TEntityAttachmentKey, TAttachmentKey, TAttachment>.Options? options = null
    ) : EntityPrepperBase<TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity<TEntityKey>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TEntityKey, TAttachmentKey, TAttachment>
{
    public class Options
    {
        public bool IsStrictRelation { get; set; } = true;
    }
    private readonly Options _options = options ?? new Options();

    public override async Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default)
    {
        var selectorFunc = navigationExpression.Compile();
        var modifiedItems = selectorFunc(modified);
        if (modifiedItems == null)
        {
            return;
        }

        // A new link may point only at an attachment the owner already links. On an insert of the owner every link is
        // new, whatever id it carries; on an update, one without an id or one the stored links do not hold — and
        // without the stored links, only one without an id is known to be new.
        var storedItems = original != null ? selectorFunc(original) : null;
        var ownedAttachmentIds = storedItems?.Select(x => x.AttachmentId).ToArray() ?? [];
        foreach (var link in modifiedItems.Where(m => original == null || IsNew(m) || (storedItems != null && storedItems.All(o => m.Id!.Equals(o.Id) != true))))
        {
            EntityAttachmentContent.KeepToOwner(link, ownedAttachmentIds);
        }

        if (original == null)
        {
            // an owner inserted with its links: they are tracked with it, and new bytes need an attachment to hold them
            foreach (var link in modifiedItems)
            {
                EntityAttachmentContent.CreateFromNewContent(link);
            }
        }
        else
        {
            var originalItems = storedItems;

            if (originalItems == null)
            {
                return;
            }

            var relatedItemsToAdd = modifiedItems.Where(m => IsNew(m) || originalItems.All(o => m.Id!.Equals(o.Id) != true)).ToArray();
            var relatedItemsToDelete = originalItems.Where(o => modifiedItems.All(m => m.Id != null && m.Id.Equals(o.Id) != true)).ToArray();
            foreach (var entity in relatedItemsToAdd)
            {
                EntityAttachmentContent.CreateFromNewContent(entity);
                // Only add when attachment has content
                if (entity.Attachment?.HasContent() == true)
                {
                    // typed now as the save types it, so the validators judge its type
                    EntityAttachmentContent.TypeByName(entity.Attachment);
                    dbContext.Entry(entity.Attachment).State = EntityState.Added;
                    dbContext.Entry(entity).State = EntityState.Added;
                }
            }
            // Materialized before the loop: tracking a link runs EF's fixup, which may change the collection it came from
            var relatedItemsToModify = modifiedItems.Except(relatedItemsToAdd).Where(m => m.Id != null && !m.Id.Equals(default(TEntityAttachmentKey))).ToArray();
            foreach (var entity in relatedItemsToModify)
            {
                var originalEntity = originalItems.Single(p => p.Id!.Equals(entity.Id));

                if (entity.Attachment?.IsNew() != true)
                {
                    // a kept link keeps its attachment, as on the link's own route
                    EntityAttachmentContent.KeepAttachment(entity, originalEntity);
                }
                if (!string.IsNullOrWhiteSpace(entity.NewFileName) || entity.NewBytes?.Any() == true)
                {
                    entity.Attachment ??= originalEntity.Attachment;
                }

                if (entity.Attachment != null)
                {
                    if (entity.Attachment.IsNew())
                    {
                        EntityAttachmentContent.TypeByName(entity.Attachment);
                        dbContext.Entry(entity.Attachment).State = EntityState.Added;
                        if (_options.IsStrictRelation && entity.AttachmentId?.Equals(originalEntity.AttachmentId) != true)
                        {
                            var originalAttachment = originalEntity.Attachment;
                            if (originalAttachment != null)
                            {
                                // mark original Attachment entity as deleted
                                dbContext.Entry(originalAttachment).State = EntityState.Deleted;
                            }
                        }
                    }
                    else
                    {
                        // the new name and bytes before the save, as the link's own route applies them
                        EntityAttachmentContent.ApplyBeforeSave(dbContext, entity, originalEntity.Attachment);
                        dbContext.Entry(entity.Attachment).State = EntityState.Modified;
                    }
                }

                dbContext.TrackAsUpdateOf(entity, originalEntity, dbContext.CaptureClientTokens(entity));
            }
            foreach (var entity in relatedItemsToDelete)
            {
                dbContext.Entry(entity).State = EntityState.Deleted;
                // also delete Attachment entity in DB
                if (_options.IsStrictRelation)
                {
                    var attachment = entity.Attachment ?? await ResolveAttachment(entity.AttachmentId, token);
                    if (attachment != null)
                    {
                        dbContext.Entry(attachment).State = EntityState.Deleted;
                    }
                }
            }
        }
    }

    private static bool IsNew(TEntityAttachment link) => link.Id == null || link.Id.Equals(default(TEntityAttachmentKey));

    /// <summary>
    /// The attachment to mark deleted when the entity being removed never loaded it.
    /// </summary>
    /// <remarks>
    /// A stub (<c>new TAttachment { Id = ... }</c>) is enough to delete a row by key, but it carries no concurrency
    /// token: once the attachment type implements <see cref="IHasConcurrencyToken"/>, EF compares the stored row
    /// against <see cref="Guid.Empty"/>, matches nothing, and every such delete fails as a conflict, for good.
    /// <c>HasConcurrencyTokenDbPrimer</c> is the general answer — it points any stub delete at the stored token during
    /// the save — and this prepper keeps a read of its own for two reasons that primer cannot give: it holds without
    /// the primer registered, and an attachment row that is already gone is skipped rather than reported as a
    /// conflict, since the join row being removed is the point and the file it pointed at is simply no longer there.
    /// Same cost either way — one query, or none when the attachment is already tracked. Types without the marker
    /// keep the stub and its saved round trip.
    /// </remarks>
    private async Task<TAttachment?> ResolveAttachment(TAttachmentKey? attachmentId, CancellationToken token)
    {
        if (attachmentId == null)
        {
            return null;
        }

        if (!typeof(IHasConcurrencyToken).IsAssignableFrom(typeof(TAttachment)))
        {
            return new TAttachment { Id = attachmentId };
        }

        // Find returns the tracked instance when there is one, so this costs a query only for an attachment
        // nothing has loaded yet
        return await dbContext.Set<TAttachment>().FindAsync([attachmentId], token);
    }
}
