using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;
using Regira.Utilities;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of the simple <see cref="SearchQuery{TEntity,TKey,TSearchObject,TDto}"/>.</summary>
public class SearchHandler<TEntity, TKey, TSearchObject, TDto>(IServiceProvider services)
    : IEntityRequestHandler<SearchQuery<TEntity, TKey, TSearchObject, TDto>, SearchResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SearchResult<TDto>?> Handle(SearchQuery<TEntity, TKey, TSearchObject, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey>>();
        var count = await service.Count(request.SearchObject, token);
        IList<TEntity> items = count == 0
            ? []
            : await service.List(request.SearchObject, Services.WithPagingDefaults<TEntity>(request.Paging), token);

        return new SearchResult<TDto> { Items = Services.GetMapper().Map<List<TDto>>(items), Count = count };
    }
}

/// <summary>The default handler of the complex <see cref="SearchQuery{TEntity,TKey,TSearchObject,TSortBy,TIncludes,TDto}"/>.</summary>
public class SearchHandler<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>(IServiceProvider services)
    : IEntityRequestHandler<SearchQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto>, SearchResult<TDto>>
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SearchResult<TDto>?> Handle(SearchQuery<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        var count = await service.Count(request.SearchObjects, token);
        IList<TEntity> items = count == 0
            ? []
            : await service.List(request.SearchObjects, request.SortBy ?? [], (request.Includes ?? []).ToBitmask(),
                Services.WithPagingDefaults<TEntity>(request.Paging), token);

        return new SearchResult<TDto> { Items = Services.GetMapper().Map<List<TDto>>(items), Count = count };
    }
}
