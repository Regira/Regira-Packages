using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Extensions;
using Regira.IO.Utilities;

namespace Regira.Entities.EFcore.Attachments;

internal static class EntityAttachmentContent
{
    /// <summary>
    /// Whether a link carries a <c>NewFileName</c> or <c>NewBytes</c> for the attachment it links.
    /// </summary>
    public static bool HasNewContent(IEntityAttachment entity)
        => !string.IsNullOrWhiteSpace(entity.NewFileName) || entity.NewBytes?.Any() == true;

    /// <summary>
    /// Gives a link that carries <c>NewBytes</c> and no attachment a new one holding them under its <c>NewFileName</c>,
    /// typed by that name as the save types it — on every path that adds a link: an owner created or updated with it, and
    /// the link's own service.
    /// </summary>
    public static void CreateFromNewContent<TKey, TObjectKey, TAttachmentKey, TAttachment>(IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> link)
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
    {
        if (link.Attachment != null || link.NewBytes?.Any() != true)
        {
            return;
        }

        link.Attachment = new TAttachment { Bytes = link.NewBytes, FileName = link.NewFileName.ToVirtualPath() };
        TypeByName(link.Attachment);
    }

    /// <summary>
    /// Applies a link's <c>NewFileName</c> and <c>NewBytes</c> to the attachment it links.
    /// </summary>
    /// <returns>Whether the attachment changed</returns>
    public static bool ApplyTo(IEntityAttachment entity)
    {
        if (entity.Attachment == null)
        {
            return false;
        }

        var changed = false;
        if (!string.IsNullOrWhiteSpace(entity.NewFileName))
        {
            // Renaming and re-filing are the same operation: a new virtual path, folders included.
            // The identifier is untouched, so the bytes never move.
            entity.Attachment.FileName = entity.NewFileName.ToVirtualPath();
            TypeByName(entity.Attachment);
            changed = true;
        }

        if (entity.NewBytes?.Any() == true)
        {
            entity.Attachment.Bytes = entity.NewBytes;
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Types <paramref name="attachment"/> by its file name, as the <c>AttachmentPrimer</c> types it at the save. Called
    /// before the save, so the validators judge the type the attachment is stored with.
    /// </summary>
    public static void TypeByName(IAttachment attachment)
    {
        if (!string.IsNullOrWhiteSpace(attachment.FileName))
        {
            attachment.ContentType = ContentTypeUtility.GetContentType(attachment.FileName);
        }
    }

    /// <summary>
    /// Keeps an updated link on the attachment it links: neither the <c>AttachmentId</c> a body sends nor a nested
    /// attachment of another id points it at another file, another owner's among them. A new attachment replaces the file,
    /// and so do new bytes.
    /// </summary>
    public static void KeepAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>(
        IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> item, IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> original)
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
    {
        item.AttachmentId = original.AttachmentId;
        if (item.Attachment != null && !EqualityComparer<TAttachmentKey>.Default.Equals(item.Attachment.Id, original.AttachmentId))
        {
            item.Attachment = original.Attachment;
        }
    }

    /// <summary>
    /// Keeps a new link to the attachments its owner already links: an <c>AttachmentId</c> the body sends, or a nested
    /// attachment's id, naming any other — another owner's file among them — is cleared. A new attachment, or new bytes,
    /// is how a link gets a file of its own; a link left with neither fails its foreign key at the save.
    /// </summary>
    public static void KeepToOwner<TKey, TObjectKey, TAttachmentKey, TAttachment>(
        IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> link, ICollection<TAttachmentKey> ownedAttachmentIds)
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
    {
        if (link.Attachment?.IsNew() == true)
        {
            return;
        }

        var comparer = EqualityComparer<TAttachmentKey>.Default;
        bool IsForeign(TAttachmentKey id) => !comparer.Equals(id, default!) && !ownedAttachmentIds.Contains(id, comparer);
        if (IsForeign(link.AttachmentId) || (link.Attachment != null && IsForeign(link.Attachment.Id)))
        {
            link.AttachmentId = default!;
            link.Attachment = null;
        }
    }

    /// <summary>
    /// For a write service's prepper: applies the link's new name and bytes before the save, typing the attachment by
    /// its new name, so the validators judge them, and marks the attachment modified, so the save primes it — the
    /// <c>AttachmentPrimer</c> stores its new bytes. A link holding the stored attachment its original also holds gets a
    /// copy first: the original the validators compare with keeps the stored values.
    /// </summary>
    public static void ApplyBeforeSave<TAttachment>(DbContext dbContext, IEntityAttachment entity, TAttachment? stored)
        where TAttachment : class, IAttachment
    {
        if (entity.Attachment is not TAttachment attachment || !HasNewContent(entity))
        {
            return;
        }

        if (ReferenceEquals(attachment, stored))
        {
            entity.Attachment = (TAttachment)dbContext.Entry(attachment).CurrentValues.ToObject();
        }

        ApplyTo(entity);
        dbContext.Entry(entity.Attachment).State = EntityState.Modified;
    }
}
