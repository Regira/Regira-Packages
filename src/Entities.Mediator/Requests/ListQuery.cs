using Regira.DAL.Paging;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Requests;

/// <summary>
/// A page of entities matching one search object, as <c>GET</c> answers it on a simple entity. The paging defaults
/// apply: an omitted page size takes the configured default, and the configured maximum caps it.
/// </summary>
public sealed record ListQuery<TEntity, TKey, TSearchObject, TDto>(TSearchObject? SearchObject = null, PagingInfo? Paging = null)
    : IEntityRequest<ListResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.List;
}

/// <summary>
/// A page of entities matching any of the search objects, sorted and with the includes asked for, as <c>GET</c> and
/// <c>POST list</c> answer it on a complex entity. The paging defaults apply as on the simple shape.
/// </summary>
public sealed record ListQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(
    IList<TSearchObject?> SearchObjects, PagingInfo? Paging = null, TIncludes[]? Includes = null, TSortBy[]? SortBy = null)
    : IEntityRequest<ListResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    Type IEntityRequest.EntityType => typeof(TEntity);
    EntityOperation IEntityRequest.Operation => EntityOperation.List;
}
