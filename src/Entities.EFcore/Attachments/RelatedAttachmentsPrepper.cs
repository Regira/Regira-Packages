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
        if (original == null)
        {
            // an owner inserted with its links: they are tracked with it, and new bytes need an attachment to hold them
            foreach (var link in modifiedItems)
            {
                EntityAttachmentContent.PrepareLink(link, null, ownedAttachmentIds: []);
            }
        }
        else
        {
            var originalItems = selectorFunc(original);

            if (originalItems == null)
            {
                foreach (var link in modifiedItems.Where(IsNew))
                {
                    EntityAttachmentContent.KeepToOwner(link, []);
                }
                return;
            }

            var ownedAttachmentIds = originalItems.Select(x => x.AttachmentId).ToArray();
            var relatedItemsToAdd = modifiedItems.Where(m => IsNew(m) || originalItems.All(o => m.Id!.Equals(o.Id) != true)).ToArray();
            var relatedItemsToDelete = originalItems.Where(o => modifiedItems.All(m => m.Id != null && m.Id.Equals(o.Id) != true)).ToArray();
            foreach (var entity in relatedItemsToAdd)
            {
                EntityAttachmentContent.PrepareLink(entity, null, ownedAttachmentIds);
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
            var replacedLinks = new List<(TEntityAttachment Link, TAttachment Attachment)>();
            foreach (var entity in relatedItemsToModify)
            {
                var originalEntity = originalItems.Single(p => p.Id!.Equals(entity.Id));
                var replaced = EntityAttachmentContent.PrepareLink(entity, originalEntity);

                if (entity.Attachment != null)
                {
                    if (entity.Attachment.IsNew())
                    {
                        EntityAttachmentContent.TypeByName(entity.Attachment);
                        dbContext.Entry(entity.Attachment).State = EntityState.Added;
                        if (replaced != null)
                        {
                            replacedLinks.Add((entity, replaced));
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
            }
            if (_options.IsStrictRelation)
            {
                await DeleteLeftAttachments(modifiedItems, relatedItemsToDelete, replacedLinks, token);
            }
        }
    }

    /// <summary>
    /// Deletes the attachments the removed and the replaced links leave, once every link is prepared — unless another
    /// link still names one: one of the owner's links (a new link may name an attachment its owner links already), or
    /// a stored link of another owner. Deleting it would take those links with it.
    /// </summary>
    private async Task DeleteLeftAttachments(ICollection<TEntityAttachment> links, TEntityAttachment[] removedLinks,
        List<(TEntityAttachment Link, TAttachment Attachment)> replacedLinks, CancellationToken token)
    {
        var comparer = EqualityComparer<TAttachmentKey>.Default;
        var leavingLinkIds = removedLinks.Concat(replacedLinks.Select(x => x.Link)).Select(x => x.Id).ToArray();
        var left = replacedLinks.Select(x => (Id: x.Attachment.Id, Attachment: (TAttachment?)x.Attachment))
            .Concat(removedLinks.Select(x => (Id: x.AttachmentId, x.Attachment)))
            .Where(x => x.Id != null && !comparer.Equals(x.Id, default!))
            .GroupBy(x => x.Id, comparer)
            .Where(group => !links.Any(link => link.Attachment?.IsNew() != true && comparer.Equals(link.AttachmentId, group.Key)))
            .ToList();
        if (left.Count == 0)
        {
            return;
        }
        // one query for every attachment left, rather than one each
        var linkedElsewhere = await EntityAttachmentContent.LinkedElsewhere<TEntityAttachment, TEntityAttachmentKey, TEntityKey, TAttachmentKey, TAttachment>(
            dbContext, left.Select(group => group.Key).ToArray(), leavingLinkIds, token);
        foreach (var group in left.Where(group => !linkedElsewhere.Contains(group.Key)))
        {
            // a stub when the owner's read did not load it: a key is enough to delete a row, and the token primer points
            // the stub of an IHasConcurrencyToken attachment at the stored token
            var attachment = group.Select(x => x.Attachment).FirstOrDefault(x => x != null) ?? new TAttachment { Id = group.Key };
            dbContext.Entry(attachment).State = EntityState.Deleted;
        }
    }

    private static bool IsNew(TEntityAttachment link) => link.Id == null || link.Id.Equals(default(TEntityAttachmentKey));
}
