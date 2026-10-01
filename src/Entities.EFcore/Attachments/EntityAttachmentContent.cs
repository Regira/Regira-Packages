using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;

namespace Regira.Entities.EFcore.Attachments;

internal static class EntityAttachmentContent
{
    /// <summary>
    /// Whether a link carries a <c>NewFileName</c> or <c>NewBytes</c> for the attachment it links.
    /// </summary>
    public static bool HasNewContent(IEntityAttachment entity)
        => !string.IsNullOrWhiteSpace(entity.NewFileName) || entity.NewBytes?.Any() == true;

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
    /// For a write service's prepper: applies the link's new name and bytes before the save, so the validators judge
    /// them, and marks the attachment modified, so the save primes it — the <c>AttachmentPrimer</c> types it by its new
    /// name and stores its new bytes. A link holding the stored attachment its original also holds gets a copy first:
    /// the original the validators compare with keeps the stored values.
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
