using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.QueryBuilders;

public static class ServiceCollectionQueryBuilderExtensions
{
    // Query Builders
    public static IServiceCollection AddDefaultQueryBuilder<TEntity>(this IServiceCollection services)
        where TEntity : IEntity<int>
        => services.AddDefaultQueryBuilder<TEntity, int>();
    public static IServiceCollection UseQueryBuilder<TEntity, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<int>
        where TImplementation : class, IQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes, TImplementation>();
    public static IServiceCollection UseQueryBuilder<TEntity, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<int>
        where TImplementation : class, IQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes, TImplementation>(factory);

    public static IServiceCollection AddDefaultQueryBuilder<TEntity, TKey>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        => services.AddDefaultQueryBuilder<TEntity, TKey, SearchObject<TKey>>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<TKey>
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>(factory);

    public static IServiceCollection AddDefaultQueryBuilder<TEntity, TKey, TSearchObject>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>, new()
        => services.AddDefaultQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TSearchObject, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>, new()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes, TImplementation>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TSearchObject, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>, new()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>
        => services.UseQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes, TImplementation>(factory);

    public static IServiceCollection AddDefaultQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>(this IServiceCollection services)
        where TEntity : IEntity<int>
        where TSearchObject : ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        => services.AddDefaultQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>();
    public static IServiceCollection UseQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<int>
        where TSearchObject : ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        where TImplementation : class, IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>
        => services.UseQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes, TImplementation>();
    public static IServiceCollection UseQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<int>
        where TSearchObject : ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        where TImplementation : class, IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>
        => services.UseQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes, TImplementation>(factory);

    public static IServiceCollection AddDefaultQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        => services.UseQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes, QueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
        => services.AddTransient<IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, TImplementation>();
    public static IServiceCollection UseQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
        => services.AddTransient<IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>(factory);
}