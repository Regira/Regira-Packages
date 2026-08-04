using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentPrimer(IAttachmentFileService<Attachment, int> fileService) : AttachmentPrimer<Attachment, int>(fileService);
public class AttachmentPrimer<TAttachment, TKey>(IAttachmentFileService<TAttachment, TKey> fileService) : EntityPrimerBase<TAttachment>
    where TAttachment : class, IAttachment<TKey>, new()
{
    public override async Task PrepareAsync(TAttachment entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entity is { Length: 0, Bytes: not null })
        {
            entity.Length = entity.GetLength();
        }

        if (string.IsNullOrWhiteSpace(entity.ContentType) && !string.IsNullOrWhiteSpace(entity.FileName))
        {
            entity.ContentType = ContentTypeUtility.GetContentType(entity.FileName);
        }

        if (entry.State is EntityState.Added or EntityState.Modified)
        {
            if (entity.HasContent())
            {
                await fileService.SaveFile(entity, token);
            }
        }

        // delete file if entity is marked as deleted
        if (entry.State == EntityState.Deleted)
        {
            if (string.IsNullOrWhiteSpace(entity.Path))
            {
                // make sure Path is known to physically delete the file
                entity = (await entry.Context.Set<TAttachment>().SingleAsync(x => x.Id!.Equals(entity.Id), token))!;
            }

            await fileService.RemoveFile(entity, token);
        }
    }
}