using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentPrimer(IFileIdentifierGenerator fileIdentifierGenerator) : EntityPrimerBase<IEntityAttachment>
{
    public override async Task PrepareAsync(IEntityAttachment entity, EntityEntry entry, CancellationToken token = default)
    {
        if ((entry.State == EntityState.Added || entry.State == EntityState.Modified) && entity.Attachment != null)
        {
            entity.Attachment.Identifier ??= await fileIdentifierGenerator.Generate(entity, token);
        }

        if (entry.State == EntityState.Modified)
        {
            if (entity.Attachment != null)
            {
                if (!string.IsNullOrWhiteSpace(entity.NewFileName))
                {
                    // Renaming and re-filing are the same operation: a new virtual path, folders included.
                    // The identifier is untouched, so the bytes never move.
                    entity.Attachment.FileName = entity.NewFileName.ToVirtualPath();
                }

                if (!string.IsNullOrWhiteSpace(entity.NewContentType))
                {
                    entity.Attachment.ContentType = entity.NewContentType;
                }

                if (entity.NewBytes?.Any() == true)
                {
                    entity.Attachment.Bytes = entity.NewBytes;
                }
            }
        }
    }
}