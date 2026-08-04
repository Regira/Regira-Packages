using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.EFcore.Services;

public class EntityRepository<TEntity>(IEntityReadService<TEntity, int, SearchObject<int>> readService, IEntityWriteService<TEntity, int> writeService)
    : EntityRepository<TEntity, int>(readService, writeService), IEntityRepository<TEntity>
    where TEntity : class, IEntity<int>;

public class EntityRepository<TEntity, TKey>(IEntityReadService<TEntity, TKey, SearchObject<TKey>> readService, IEntityWriteService<TEntity, TKey> writeService)
    : EntityRepository<TEntity, TKey, SearchObject<TKey>>(readService, writeService)
    where TEntity : class, IEntity<TKey>;

public class EntityRepository<TEntity, TKey, TSearchObject>(IEntityReadService<TEntity, TKey, TSearchObject> readService, IEntityWriteService<TEntity, TKey> writeService)
    : IEntityRepository<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    /// <summary>The archived filter is inert unless the archive filter applies — it is the only reader of it.</summary>
    private static readonly bool IsArchivableEntity = typeof(IArchivable).IsAssignableFrom(typeof(TEntity));

    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default)
    => readService.Details(id, token);
    /// <inheritdoc cref="IEntityReadService{TEntity,TKey}.Details(TKey,ArchivedFilter?,CancellationToken)"/>
    // Dispatch to this repository's (possibly overridden) Details whenever the archived filter changes nothing,
    // so a derived repository (e.HasRepository<T>()) is not skipped by the archived-inclusive call paths.
    public virtual Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default)
        => archived.HasValue && IsArchivableEntity
            ? readService.Details(id, archived, token)
            : Details(id, token);

    public virtual Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => readService.List(so, pagingInfo, token);
    public virtual Task<long> Count(TSearchObject? so, CancellationToken token = default)
        => readService.Count(so, token);

    public virtual Task<IList<TEntity>> List(object? so, PagingInfo? pagingInfo, CancellationToken token = default)
         => readService.List(so, pagingInfo, token);
    public virtual Task<long> Count(object? so, CancellationToken token = default)
         => readService.Count(so, token);



    public virtual Task Add(TEntity item, CancellationToken token = default)
        => writeService.Add(item, token);
    public virtual Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
        => writeService.Modify(item, token);
    public virtual Task Save(TEntity item, CancellationToken token = default)
        => writeService.Save(item, token);
    public virtual Task Remove(TEntity item, CancellationToken token = default)
        => writeService.Remove(item, token);

    public virtual Task<int> SaveChanges(CancellationToken token = default)
        => writeService.SaveChanges(token);
}