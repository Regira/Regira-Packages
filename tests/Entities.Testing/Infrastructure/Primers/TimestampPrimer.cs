using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Entities.Testing.Infrastructure.Primers;

public class TimestampPrimer : EntityPrimerBase<IHasTimestamps>
{
    public override Task PrepareAsync(IHasTimestamps entity, EntityEntry entry, CancellationToken token = default)
    {
        entity.Created = ((DateTime)entry.OriginalValues[nameof(entity.Created)]!).AsUtc();

        if (entity.Created == DateTime.MinValue)
        {
            entity.Created = DateTime.UtcNow;
        }

        if (entry.State == EntityState.Modified)
        {
            entity.LastModified = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }
}
