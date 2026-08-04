using Regira.DAL.Paging;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Services.Abstractions;

// TODO: consider reintroducing an `Exists(TKey id, ...)` existence check (filtered Any — no includes,
// no processors) and a `Details(TKey id, TIncludes? includes, ...)` overload for explicit includes.
// Both were removed for now; callers use Count(new { id }) == 1 and the includes-less Details(id).

// With ID type
public interface IEntityReadService<TEntity, in TKey>
{
    Task<TEntity?> Details(TKey id, CancellationToken token = default);
    /// <summary>
    /// Details with an explicit <see cref="ArchivedFilter"/> (<see cref="ISearchObject.Archived"/>);
    /// <c>null</c> falls back to the configured <see cref="EntityQueryOptions.DefaultArchivedFilter"/>.
    /// The by-id search object still runs through the full global-filter loop — only the built-in
    /// <c>IArchivable</c> filter reads it, so tenant/owner row security (global filters and the app's own EF
    /// query filters alike) is unaffected.<br />
    /// Declared as a default interface member (falling back to the default-filtered
    /// <see cref="Details(TKey,CancellationToken)"/>) so existing implementations keep compiling.
    /// <para>
    /// ⚠️ A custom read service must implement <b>both</b> overloads. Inheriting this default leaves the
    /// archived-inclusive write path unable to see archived rows, so <c>DELETE</c> is not idempotent and a
    /// restore 404s — silently, since everything still compiles.
    /// </para>
    /// </summary>
    Task<TEntity?> Details(TKey id, ArchivedFilter? archived, CancellationToken token = default)
        => Details(id, token);
    Task<IList<TEntity>> List(object? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    Task<long> Count(object? so, CancellationToken token = default);
}

public interface IEntityReadService<TEntity, in TKey, in TSearchObject> : IEntityReadService<TEntity, TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    Task<IList<TEntity>> List(TSearchObject? so = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    Task<long> Count(TSearchObject? so = null, CancellationToken token = default);
}

public interface IEntityReadService<TEntity, in TKey, TSearchObject, TSortBy, TIncludes> : IEntityReadService<TEntity, TKey, TSearchObject>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    Task<IList<TEntity>> List(IList<TSearchObject?> so, IList<TSortBy> sortBy, TIncludes? includes = null, PagingInfo? pagingInfo = null, CancellationToken token = default);
    Task<long> Count(IList<TSearchObject?> so, CancellationToken token = default);
}