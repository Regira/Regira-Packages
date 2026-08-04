using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.EFcore.Services;

public class EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>
    (IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes> readService, IEntityWriteService<TEntity, int> writeService)
    : EntityRepository<TEntity, int, TSearchObject, TSortBy, TIncludes>(readService, writeService), IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;

public class EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes> readService,
    IEntityWriteService<TEntity, TKey> writeService)
    : IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
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
    public Task<long> Count(TSearchObject? so, CancellationToken token = default)
        => readService.Count(so, token);

    public virtual Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => readService.List(so, sortBy, includes, pagingInfo, token);

    public virtual Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default)
        => readService.Count(so, token);

    Task<IList<TEntity>> IEntityReadService<TEntity, TKey>.List(object? so, PagingInfo? pagingInfo, CancellationToken token)
        => readService.List(so, pagingInfo, token);
    Task<long> IEntityReadService<TEntity, TKey>.Count(object? so, CancellationToken token)
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