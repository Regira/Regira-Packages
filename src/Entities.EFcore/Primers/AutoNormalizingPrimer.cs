using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;
using Regira.Normalizing.Abstractions;

namespace Regira.Entities.EFcore.Primers;

public class AutoNormalizingPrimer(IObjectNormalizer? normalizer = null) : EntityPrimerBase<IEntity>
{
    readonly IObjectNormalizer _normalizer = normalizer ?? new ObjectNormalizer();

    public override Task PrepareAsync(IEntity entity, EntityEntry entry, CancellationToken token = default)
    {
        _normalizer.HandleNormalize(entity);
        return Task.CompletedTask;
    }
    public override Task PrepareManyAsync(IList<EntityEntry> entries, CancellationToken token = default)
    {
        _normalizer.HandleNormalizeMany(entries.Select(e => e.Entity));
        return Task.CompletedTask;
    }
}