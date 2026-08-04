using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Primers;

public class NormalizingPrimer<TEntity>(IEntityNormalizer<TEntity> entityNormalizer) : EntityPrimerBase<TEntity>
    where TEntity : class
{
    public override Task PrepareAsync(TEntity entity, EntityEntry entry, CancellationToken token = default)
    {
        entityNormalizer.HandleNormalize(entity, token);

        return Task.CompletedTask;
    }
}
public class NormalizingPrimer(IEntityNormalizer? entityNormalizer = null) : EntityPrimerBase<IEntity>
{
    private readonly IEntityNormalizer _entityNormalizer = entityNormalizer ?? new DefaultEntityNormalizer();

    public override Task PrepareAsync(IEntity entity, EntityEntry entry, CancellationToken token = default)
    {
        _entityNormalizer.HandleNormalize(entity, token);

        return Task.CompletedTask;
    }
}