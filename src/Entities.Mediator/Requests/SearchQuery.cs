using Regira.DAL.Paging;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// The total count of entities matching one search object plus a page of them, as <c>GET search</c> answers it on a
/// simple entity. The paging defaults apply as on <see cref="ListQuery{TEntity,TKey,TSearchObject,TDto}"/>.
/// </summary>
public sealed record SearchQuery<TEntity, TKey, TSearchObject, TDto>(TSearchObject? SearchObject = null, PagingInfo? Paging = null)
    : IEntityRequest<SearchResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Search;
}

/// <summary>
/// The total count of entities matching any of the search objects plus a sorted page of them, as <c>GET search</c>
/// and <c>POST search</c> answer it on a complex entity.
/// </summary>
public sealed record SearchQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(
    IList<TSearchObject?> SearchObjects, PagingInfo? Paging = null, TIncludes[]? Includes = null, TSortBy[]? SortBy = null)
    : IEntityRequest<SearchResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.Search;
}
