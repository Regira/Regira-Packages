using Regira.DAL.Paging;
using Regira.Entities.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Services.Abstractions;

public abstract class EntityWrappingServiceBase<TEntity>(IEntityService<TEntity, int, SearchObject<int>> service)
    : EntityWrappingServiceBase<TEntity, int, SearchObject<int>>(service), IEntityService<TEntity>
    where TEntity : class, IEntity<int>;

public abstract class EntityWrappingServiceBase<TEntity, TKey>(
    IEntityService<TEntity, TKey, SearchObject<TKey>> service)
    : EntityWrappingServiceBase<TEntity, TKey, SearchObject<TKey>>(service)//, IEntityService<TEntity, TKey> (already included)
    where TEntity : class, IEntity<TKey>;

public abstract class EntityWrappingServiceBase<TEntity, TKey, TSearchObject>(
    IEntityService<TEntity, TKey, TSearchObject> service) : IEntityService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    protected readonly IEntityService<TEntity, TKey, TSearchObject> Service = service;

    /// <summary>The archived filter is inert unless the archive filter applies — it is the only reader of it.</summary>
    private static readonly bool IsArchivableEntity = typeof(IArchivable).IsAssignableFrom(typeof(TEntity));

    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default)
        => Service.Details(id, token);
    /// <inheritdoc cref="IEntityReadService{TEntity,TKey}.Details(TKey,ArchivedFilter?,CancellationToken)"/>
    // Dispatch to this service's (possibly overridden) Details whenever the archived filter changes nothing, so
    // wrapping logic runs on every read path it can — the post-save re-fetch always asks archived-inclusive.
    // Only a real archived-explicit read goes straight to the inner service; cover that by overriding this too.
    public virtual Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default)
        => archived.HasValue && IsArchivableEntity
            ? Service.Details(id, archived, token)
            : Details(id, token);

    public virtual Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => Service.List(so, pagingInfo, token);
    public virtual Task<long> Count(TSearchObject? so = null, CancellationToken token = default)
        => Service.Count(so, token);

    public virtual Task<IList<TEntity>> List(object? so, PagingInfo? pagingInfo, CancellationToken token = default)
        => Service.List(so, pagingInfo, token);
    public virtual Task<long> Count(object? so, CancellationToken token = default)
        => Service.Count(so, token);


    public virtual Task Add(TEntity item, CancellationToken token = default)
        => Service.Add(item, token);
    /// <summary>
    /// Tracks the change; nothing is written until <see cref="SaveChanges"/>. The returned entity is the
    /// <b>detached pre-modification original</b> (the write service detaches it to attach <paramref name="item"/>
    /// in its place) — read it for old values, but mutating it persists nothing. In an override, side effects on
    /// <em>other</em> rows must target entities tracked by the DbContext (framework reads are no-tracking) or
    /// flush themselves; only <paramref name="item"/> is guaranteed to be written by the caller's
    /// <see cref="SaveChanges"/>.
    /// </summary>
    public virtual Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
        => Service.Modify(item, token);
    // Dispatch to this service's (possibly overridden) Add/Modify so wrapping logic runs on the Save path
    public virtual Task Save(TEntity item, CancellationToken token = default)
        => item.IsNew() ? Add(item, token) : Modify(item, token);
    public virtual Task Remove(TEntity item, CancellationToken token = default)
        => Service.Remove(item, token);

    /// <summary>
    /// Flushes and, on success, <b>clears the change tracker</b> — entities from an earlier flush are detached,
    /// so anything staged after this call needs a fresh read (or its own flush) to be persisted.
    /// </summary>
    public virtual Task<int> SaveChanges(CancellationToken token = default)
        => Service.SaveChanges(token);
}

public abstract class EntityWrappingServiceBase<TEntity, TSearchObject, TSortBy, TIncludes>(
    IEntityService<TEntity, int, TSearchObject, TSortBy, TIncludes> service)
    : EntityWrappingServiceBase<TEntity, int, TSearchObject, TSortBy, TIncludes>(service), IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum;
public abstract class EntityWrappingServiceBase<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes> service)
    : IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    /// <summary>The archived filter is inert unless the archive filter applies — it is the only reader of it.</summary>
    private static readonly bool IsArchivableEntity = typeof(IArchivable).IsAssignableFrom(typeof(TEntity));

    public virtual Task<TEntity?> Details(TKey id, CancellationToken token = default)
        => service.Details(id, token);
    /// <inheritdoc cref="IEntityReadService{TEntity,TKey}.Details(TKey,ArchivedFilter?,CancellationToken)"/>
    // Dispatch to this service's (possibly overridden) Details whenever the archived filter changes nothing, so
    // wrapping logic runs on every read path it can — the post-save re-fetch always asks archived-inclusive.
    // Only a real archived-explicit read goes straight to the inner service; cover that by overriding this too.
    public virtual Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default)
        => archived.HasValue && IsArchivableEntity
            ? service.Details(id, archived, token)
            : Details(id, token);
    public virtual Task<IList<TEntity>> List(object? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => service.List(so, pagingInfo, token);
    public Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => service.List(so, pagingInfo, token);
    public virtual Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default)
        => service.List(so, sortBy, includes, pagingInfo, token);

    public virtual Task<long> Count(object? so, CancellationToken token = default)
        => service.Count(so, token);
    public Task<long> Count(TSearchObject? so = null, CancellationToken token = default)
        => service.Count(so, token);
    public virtual Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default)
        => service.Count(so, token);

    public virtual Task Add(TEntity item, CancellationToken token = default)
        => service.Add(item, token);
    /// <summary>
    /// Tracks the change; nothing is written until <see cref="SaveChanges"/>. The returned entity is the
    /// <b>detached pre-modification original</b> (the write service detaches it to attach <paramref name="item"/>
    /// in its place) — read it for old values, but mutating it persists nothing. In an override, side effects on
    /// <em>other</em> rows must target entities tracked by the DbContext (framework reads are no-tracking) or
    /// flush themselves; only <paramref name="item"/> is guaranteed to be written by the caller's
    /// <see cref="SaveChanges"/>.
    /// </summary>
    public virtual Task<TEntity?> Modify(TEntity item, CancellationToken token = default)
        => service.Modify(item, token);
    // Dispatch to this service's (possibly overridden) Add/Modify so wrapping logic runs on the Save path
    public virtual Task Save(TEntity item, CancellationToken token = default)
        => item.IsNew() ? Add(item, token) : Modify(item, token);
    public virtual Task Remove(TEntity item, CancellationToken token = default)
        => service.Remove(item, token);

    /// <summary>
    /// Flushes and, on success, <b>clears the change tracker</b> — entities from an earlier flush are detached,
    /// so anything staged after this call needs a fresh read (or its own flush) to be persisted.
    /// </summary>
    public virtual Task<int> SaveChanges(CancellationToken token = default)
        => service.SaveChanges(token);

    public virtual TSearchObject? Convert(object? so)
        => SearchObjectCoercion.Coerce<TSearchObject>(so);
}
