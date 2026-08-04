using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.DependencyInjection.QueryBuilders;

public static class ServiceCollectionQueryFilterExtensions
{
    // QueryFilters
    public static IServiceCollection AddFilter<TEntity, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<int>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, int, SearchObject<int>>
        => services.AddFilter<TEntity, SearchObject<int>, TImplementation>();
    public static IServiceCollection AddFilter<TEntity, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<int>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, int, SearchObject<int>>
        => services.AddFilter<TEntity, SearchObject<int>, TImplementation>(factory);

    public static IServiceCollection AddFilter<TEntity, TSearchObject, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<int>
        where TSearchObject : ISearchObject<int>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, int, TSearchObject>
        => services.AddFilter<TEntity, int, TSearchObject, TImplementation>();
    public static IServiceCollection AddFilter<TEntity, TSearchObject, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<int>
        where TSearchObject : ISearchObject<int>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, int, TSearchObject>
        => services.AddFilter<TEntity, int, TSearchObject, TImplementation>(factory);

    public static IServiceCollection AddFilter<TEntity, TKey, TSearchObject, TImplementation>(this IServiceCollection services)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
        => services.AddTransient<IFilteredQueryBuilder<TEntity, TKey, TSearchObject>, TImplementation>();
    public static IServiceCollection AddFilter<TEntity, TKey, TSearchObject, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntity : IEntity<TKey>
        where TSearchObject : ISearchObject<TKey>
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
        => services.AddTransient<IFilteredQueryBuilder<TEntity, TKey, TSearchObject>>(factory);


    // Global QueryFilters
    public static IServiceCollection AddGlobalFilterQueryBuilder<TImplementation>(this IServiceCollection services)
        where TImplementation : class, IGlobalFilteredQueryBuilder
        => services
            .AddTransient<IGlobalFilteredQueryBuilder, TImplementation>();
    public static IServiceCollection AddGlobalFilterQueryBuilder<TImplementation, TKey>(this IServiceCollection services)
        where TImplementation : class, IGlobalFilteredQueryBuilder<TKey>
        => services
            .AddGlobalFilterQueryBuilder<TImplementation>()
            .AddTransient<IGlobalFilteredQueryBuilder<TKey>, TImplementation>();
    public static TServiceCollection RemoveGlobalQueryFilters<TServiceCollection>(this TServiceCollection services)
        where TServiceCollection : IServiceCollection
    {
        var globalFilters = services
            .Where(d =>
                d.ImplementationType != null
                && TypeUtility.ImplementsInterface<IGlobalFilteredQueryBuilder>(d.ImplementationType)
            );

        foreach (var descriptor in globalFilters)
        {
            services.Remove(descriptor);
        }

        return services;
    }


    /// <inheritdoc cref="AddDefaultGlobalQueryFilters{TKey}"/>>
    public static EntityServiceCollectionOptions AddDefaultGlobalQueryFilters(this EntityServiceCollectionOptions options)
        => options.AddDefaultGlobalQueryFilters<int>();
    /// <summary>
    /// Adds default filtered query builders
    /// <list type="bullet">
    /// <item><see cref="FilterIdsQueryBuilder">Id(s)</see></item>
    /// <item><see cref="FilterArchivablesQueryBuilder">Archivable</see></item>
    /// <item>Timestamps: <see cref="FilterHasCreatedQueryBuilder">Created</see> &amp; <see cref="FilterHasLastModifiedQueryBuilder">LastModified</see></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="options"></param>
    /// <returns></returns>
    public static EntityServiceCollectionOptions AddDefaultGlobalQueryFilters<TKey>(this EntityServiceCollectionOptions options)
    {
        options.AddDefaultGlobalFilter<FilterIdsQueryBuilder<TKey>>();
        options.AddDefaultGlobalFilter<FilterArchivablesQueryBuilder>();
        options.AddDefaultGlobalFilter<FilterHasCreatedQueryBuilder>();
        options.AddDefaultGlobalFilter<FilterHasLastModifiedQueryBuilder>();
        if (typeof(TKey) != typeof(int))
        {
            // the parameterless defaults above are int-keyed and ignore search objects with another key
            // type — add the TKey variants so capability filtering works for this key type too
            options.AddDefaultGlobalFilter<FilterArchivablesQueryBuilder<TKey>>();
            options.AddDefaultGlobalFilter<FilterHasCreatedQueryBuilder<TKey>>();
            options.AddDefaultGlobalFilter<FilterHasLastModifiedQueryBuilder<TKey>>();
        }

        return options;
    }

    /// <summary>
    /// Registers a global filter and records it as defaults-contributed, so the startup scope check can tell
    /// "registered because UseDefaults() registers everything" from "the app asked for this one".
    /// </summary>
    internal static EntityServiceCollectionOptions AddDefaultGlobalFilter<TImplementation>(this EntityServiceCollectionOptions options)
        where TImplementation : class, IGlobalFilteredQueryBuilder
    {
        options.AddGlobalFilterQueryBuilder<TImplementation>();
        EntityRegistrationLog.TrackDefaultGlobalFilter(options.Services, typeof(TImplementation));
        return options;
    }
    public static EntityServiceCollectionOptions AddGlobalFilterQueryBuilder<TImplementation>(this EntityServiceCollectionOptions options)
        where TImplementation : class, IGlobalFilteredQueryBuilder
    {
        options.Services.AddTransient<IGlobalFilteredQueryBuilder, TImplementation>();
        return options;
    }
    public static EntityServiceCollectionOptions AddGlobalFilterQueryBuilder<TImplementation, TKey>(this EntityServiceCollectionOptions options)
        where TImplementation : class, IGlobalFilteredQueryBuilder<TKey>
    {
        options.Services
            .AddGlobalFilterQueryBuilder<TImplementation>()
            .AddTransient<IGlobalFilteredQueryBuilder<TKey>, TImplementation>();
        return options;
    }
}