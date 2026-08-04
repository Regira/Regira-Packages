using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.Processors;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;

public class EntityServiceBuilderBase<TEntity, TKey>(EntityServiceCollectionOptions options)
    : EntityServiceCollectionBase(options)
    where TEntity : class, IEntity<TKey>
{
    public bool HasEntityService() => HasService<IEntityService<TEntity, TKey>>();
    public bool HasService<TService>() => Services.Any(s => s.ServiceType == typeof(TService));


    // Entity service
    public EntityServiceBuilderBase<TEntity, TKey> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey>, IEntityService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityService<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> UseEntityService<TService>(Func<IServiceProvider, TService> factory)
        where TService : class, IEntityService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityService<TEntity, TKey, SearchObject<TKey>>>(factory);
        Services.AddTransient(factory);
        return this;
    }

    // Read service
    public EntityServiceBuilderBase<TEntity, TKey> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityReadService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityReadService<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }

    // Write service
    public EntityServiceBuilderBase<TEntity, TKey> UseWriteService<TService>()
        where TService : class, IEntityWriteService<TEntity, TKey>
    {
        Services.AddTransient<IEntityWriteService<TEntity, TKey>, TService>();
        return this;
    }

    // Entity repository
    public EntityServiceBuilderBase<TEntity, TKey> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService<TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService(factory);
        Services.AddTransient(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey, SearchObject<TKey>>>(factory);
        return this;
    }

    // Entity manager
    public EntityServiceBuilderBase<TEntity, TKey> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService<TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService(factory);
        Services.AddTransient(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey, SearchObject<TKey>>>(factory);
        return this;
    }


    /* Reading */

    // Query builders
    public EntityServiceBuilderBase<TEntity, TKey> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TKey>();
        Services.UseQueryBuilder<TEntity, TKey, QueryBuilder<TEntity, TKey>>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> UseQueryBuilder<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>(factory);
        return this;
    }

    // Query filters
    public EntityServiceBuilderBase<TEntity, TKey> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddFilter<TEntity, TKey, SearchObject<TKey>, TImplementation>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> AddFilter<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddFilter<TEntity, TKey, SearchObject<TKey>, TImplementation>(factory);
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> Filter(Func<IQueryable<TEntity>, SearchObject<TKey>?, IQueryable<TEntity>> filterFunc)
    {
        AddFilter(_ => new EntityQueryFilter<TEntity, TKey, SearchObject<TKey>>(filterFunc));
        return this;
    }

    // Default sort
    public EntityServiceBuilderBase<TEntity, TKey> SortBy(Func<IQueryable<TEntity>, IQueryable<TEntity>> sortBy)
    {
        Services.AddTransient<ISortedQueryBuilder<TEntity, TKey>>(_ => new SortedQueryBuilder<TEntity, TKey>(sortBy));
        return this;
    }

    // Default includes
    public EntityServiceBuilderBase<TEntity, TKey> Includes(Func<IQueryable<TEntity>, EntityIncludes?, IQueryable<TEntity>> addIncludes)
    {
        Services.AddTransient<IIncludableQueryBuilder<TEntity, TKey>>(_ => new IncludableQueryBuilder<TEntity, TKey>(addIncludes));
        return this;
    }

    // Paging defaults (per-entity override of the global EntityListOptions)
    /// <summary>
    /// Overrides the global default/max page size for this entity at the HTTP boundary. When called it
    /// fully defines this entity's paging: <paramref name="defaultPageSize"/> applies when the caller omits
    /// paging, <paramref name="maxPageSize"/> is the cap (and what a <c>pageSize &lt;= 0</c> opt-out falls back
    /// to); <c>null</c> means that aspect is off. Calling <c>SetPageSize()</c> with no arguments opts the
    /// entity out of paging entirely.
    /// </summary>
    public EntityServiceBuilderBase<TEntity, TKey> SetPageSize(int? defaultPageSize = null, int? maxPageSize = null)
    {
        Services.AddSingleton(new EntityListOptions<TEntity> { DefaultPageSize = defaultPageSize, MaxPageSize = maxPageSize });
        return this;
    }


    /* Writing */

    // Normalizers
    public EntityServiceBuilderBase<TEntity, TKey> AddNormalizer<TNormalizer>()
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        Services.AddNormalizer<TEntity, TNormalizer>();
        return this;
    }

    // Processors
    public EntityServiceBuilderBase<TEntity, TKey> AddProcessor<TImplementation>()
        where TImplementation : class, IEntityProcessor<TEntity, EntityIncludes>
    {
        Services.AddProcessor<TEntity, EntityIncludes, TImplementation>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> AddProcessor<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityProcessor<TEntity, EntityIncludes>
    {
        Services.AddProcessor<TEntity, EntityIncludes, TImplementation>(factory);
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> Process(Func<IList<TEntity>, EntityIncludes?, Task> process)
    {
        return AddProcessor(_ => new EntityProcessor<TEntity, EntityIncludes>(process));
    }
    public EntityServiceBuilderBase<TEntity, TKey> Process(Action<TEntity, EntityIncludes?> process)
    {
        return AddProcessor(_ => new EntityProcessor<TEntity, EntityIncludes>((items, includes) =>
        {
            foreach (var item in items)
            {
                process(item, includes);
            }
            return Task.CompletedTask;
        }));
    }

    // Preppers
    public EntityServiceBuilderBase<TEntity, TKey> AddPrepper<TPrepper>()
        where TPrepper : class, IEntityPrepper<TEntity>
    {
        Services.AddPrepper<TEntity, TPrepper>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> Prepare(Action<TEntity> prepareFunc)
    {
        Services.AddPrepper(prepareFunc);
        return this;
    }

    // Mapping
    public EntityServiceBuilderBase<TEntity, TKey> AddMapping<TSource, TTarget>()
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Services);
        mapConfig.Configure<TSource, TTarget>();
        return this;
    }
    public EntityServiceBuilderBase<TEntity, TKey> AddMapping(Type sourceType, Type targetType)
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Services);
        mapConfig.Configure(sourceType, targetType);
        return this;
    }
}
