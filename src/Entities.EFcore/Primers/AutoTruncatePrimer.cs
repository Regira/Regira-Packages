using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Primers;

public class AutoTruncatePrimer : EntityPrimerBase<IEntity>
{
    public override Task PrepareAsync(IEntity entity, EntityEntry entry, CancellationToken token = default)
    {
        entry.AutoTruncate();
        return Task.CompletedTask;
    }
}