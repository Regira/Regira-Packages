using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Services.Abstractions;

public interface IEntityWriteService<TEntity, TKey>
    where TEntity : class, IEntity<TKey>
{
    Task Add(TEntity item, CancellationToken token = default);
    Task<TEntity?> Modify(TEntity item, CancellationToken token = default);
    Task Save(TEntity item, CancellationToken token = default);
    Task Remove(TEntity item, CancellationToken token = default);
    Task<int> SaveChanges(CancellationToken token = default);
}