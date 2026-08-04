using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Primers;

public class ArchivablePrimer : EntityPrimerBase<IArchivable>
{
    public override Task PrepareAsync(IArchivable entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Deleted)
        {
            entity.IsArchived = true;
            entry.State = EntityState.Modified;
        }
        return Task.CompletedTask;
    }
}