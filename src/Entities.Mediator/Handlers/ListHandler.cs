using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;
using Regira.Utilities;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of the simple <see cref="ListQuery{TEntity,TKey,TSearchObject,TDto}"/>.</summary>
public class ListHandler<TEntity, TKey, TSearchObject, TDto>(IServiceProvider services)
    : IEntityRequestHandler<ListQuery<TEntity, TKey, TSearchObject, TDto>, ListResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<ListResult<TDto>?> Handle(ListQuery<TEntity, TKey, TSearchObject, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        var items = await service.List(request.SearchObject, Services.WithPagingDefaults<TEntity>(request.Paging), token);

        return new ListResult<TDto> { Items = Services.GetMapper().Map<List<TDto>>(items) };
    }
}

/// <summary>The default handler of the complex <see cref="ListQuery{TEntity,TKey,TSearchObject,TSortBy,TIncludes,TDto}"/>.</summary>
public class ListHandler<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(IServiceProvider services)
    : IEntityRequestHandler<ListQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>, ListResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<ListResult<TDto>?> Handle(ListQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        var items = await service.List(request.SearchObjects, request.SortBy ?? [], (request.Includes ?? []).ToBitmask(),
            Services.WithPagingDefaults<TEntity>(request.Paging), token);

        return new ListResult<TDto> { Items = Services.GetMapper().Map<List<TDto>>(items) };
    }
}
