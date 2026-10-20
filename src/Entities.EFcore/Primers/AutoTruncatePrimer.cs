using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Primers;

[Obsolete("Truncates beside AutoTruncateDbContextInterceptor, which UseDefaults() wires (twice when both are). Use the interceptor, or AddAutoTruncateInterceptors() on the options builder. Will be removed in v7.", DiagnosticId = "REGIRA0001")]
public class AutoTruncatePrimer : EntityPrimerBase<IEntity>
{
    public override Task PrepareAsync(IEntity entity, EntityEntry entry, CancellationToken token = default)
    {
        entry.AutoTruncate();
        return Task.CompletedTask;
    }
}