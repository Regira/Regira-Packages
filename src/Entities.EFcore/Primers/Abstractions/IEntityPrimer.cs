using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Regira.Entities.EFcore.Primers.Abstractions;

public interface IEntityPrimer<in T> : IEntityPrimer
{
    Task PrepareAsync(T entity, EntityEntry entry, CancellationToken token = default);
    bool CanPrepare(T entity);
}

public interface IEntityPrimer
{
    Task PrepareManyAsync(IList<EntityEntry> entries, CancellationToken token = default);
    Task PrepareAsync(object entity, EntityEntry entry, CancellationToken token = default);
    bool CanPrepare(object entity);
}