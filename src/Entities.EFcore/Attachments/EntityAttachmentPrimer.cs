using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.Attachments.Abstractions;
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
            // the entity services' preppers have applied this already; a save that bypasses them relies on it here
            EntityAttachmentContent.ApplyTo(entity);
        }
    }
}