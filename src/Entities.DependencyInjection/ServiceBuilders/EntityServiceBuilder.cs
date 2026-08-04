using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.Processors;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class EntityServiceBuilder<TContext, TEntity, TKey>(EntityServiceCollectionOptions options)
    : EntityServiceCollection<TContext>(options),
        IEntityServiceBuilder<TContext, TEntity, TKey>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    public bool HasEntityService() => HasService<IEntityService<TEntity, TKey>>();
    public bool HasService<TService>() => Services.Any(s => s.ServiceType == typeof(TService));

    // Build
    public virtual void Build()
    {
        // Query Builder
        if (!HasService<IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>>())
        {
            AddDefaultQueryBuilder();
        }
        // Read Service
        if (!HasService<IEntityReadService<TEntity, TKey, SearchObject<TKey>>>())
        {
            UseReadService<EntityReadService<TContext, TEntity, TKey, SearchObject<TKey>>>();
        }
        // Write Service
        if (!HasService<IEntityWriteService<TEntity, TKey>>())
        {
            UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }
        // Entity Repository
        if (!HasService<IEntityRepository<TEntity, TKey>>())
        {
            HasRepositoryInner<EntityRepository<TEntity, TKey>>();
        }
        // Entity Service
        if (!HasService<IEntityService<TEntity, TKey>>())
        {
            UseEntityService<EntityRepository<TEntity, TKey>>();
        }
    }


    // Entity mapping
    public MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto> UseMapping<TDto, TInputDto>(Action<IEntityMapConfigurator>? mapAction = null)
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Options.Services);
        mapConfig.Configure<TEntity, TDto>();
        mapConfig.Configure<TInputDto, TEntity>();
        mapAction?.Invoke(mapConfig);
        Services.AddSingleton(new EntityMappingRegistration(typeof(TEntity), typeof(TDto), typeof(TInputDto)));

        return new MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto>(Options);
    }
    public MappedEntityServiceBuilder<TContext, TEntity, TKey> UseMapping(Type dto, Type inputDto, Action<IEntityMapConfigurator>? mapAction = null)
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Options.Services);
        mapConfig.Configure(typeof(TEntity), dto);
        mapConfig.Configure(inputDto, typeof(TEntity));
        mapAction?.Invoke(mapConfig);
        Services.AddSingleton(new EntityMappingRegistration(typeof(TEntity), dto, inputDto));

        return new MappedEntityServiceBuilder<TContext, TEntity, TKey>(Options);
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> AddMapping<TSource, TTarget>()
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Options.Services);
        mapConfig.Configure<TSource, TTarget>();

        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> AddMapping(Type sourceType, Type targetType)
    {
        if (Options.EntityMapConfiguratorFactory == null)
        {
            throw new InvalidOperationException("Missing mapping configuration. Configure a mapper first, e.g. UseEntities(o => o.UseMapsterMapping()) or o.UseAutoMapper(), before configuring entity mappings.");
        }

        var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Options.Services);
        mapConfig.Configure(sourceType, targetType);

        return this;
    }

    // Entity service
    /// <summary>
    /// Adds <see cref="EntityRepository{TEntity}"/> as implementation for:
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity}"/></item>
    ///     <item><see cref="IEntityService{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> AddDefaultService()
        => UseEntityService<EntityRepository<TEntity, TKey>>();
    /// <summary>
    /// Adds <typeparamref name="TService"/> implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey>, IEntityService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityService<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    /// <summary>
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> UseEntityService<TService>(Func<IServiceProvider, TService> factory)
        where TService : class, IEntityService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityService<TEntity, TKey, SearchObject<TKey>>>(factory);
        Services.AddTransient(factory);
        return this;
    }

    // Read Service
    public EntityServiceBuilder<TContext, TEntity, TKey> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityReadService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityReadService<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    // Write Service
    public EntityServiceBuilder<TContext, TEntity, TKey> UseWriteService<TService>()
        where TService : class, IEntityWriteService<TEntity, TKey>
    {
        Services.AddTransient<IEntityWriteService<TEntity, TKey>, TService>();
        return this;
    }

    // Entity Repository
    protected internal EntityServiceBuilder<TContext, TEntity, TKey> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient<IEntityRepository<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    protected internal EntityServiceBuilder<TContext, TEntity, TKey> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddTransient(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey, SearchObject<TKey>>>(factory);
        return this;
    }
    /// <summary>
    /// Adds <typeparamref name="TService"/> implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityRepository{TEntity}"/></item>
    ///     <item><see cref="IEntityRepository{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService<TService>();
        HasRepositoryInner<TService>();
        return this;
    }
    /// <summary>
    /// Adds implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityRepository{TEntity}"/></item>
    ///     <item><see cref="IEntityRepository{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <param name="factory"></param>
    /// <returns></returns>
    /// 
    public EntityServiceBuilder<TContext, TEntity, TKey> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService(factory);
        HasRepositoryInner(factory);
        return this;
    }

    // Entity Manager
    /// <summary>
    /// Adds <typeparamref name="TService"/> implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityManager{TEntity}"/></item>
    ///     <item><see cref="IEntityManager{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService<TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey, SearchObject<TKey>>, TService>();
        return this;
    }
    /// <summary>
    /// Adds implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityManager{TEntity}"/></item>
    ///     <item><see cref="IEntityManager{TEntity, TKey}"/></item>
    /// </list>
    /// </summary>
    /// <param name="factory"></param>
    /// <returns></returns>
    public EntityServiceBuilder<TContext, TEntity, TKey> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TKey>, IEntityManager<TEntity, TKey, SearchObject<TKey>>
    {
        UseEntityService(factory);
        Services.AddTransient(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey, SearchObject<TKey>>>(factory);
        return this;
    }

    /* Reading */

    // Query Builders
    public EntityServiceBuilder<TContext, TEntity, TKey> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TKey>();
        Services.UseQueryBuilder<TEntity, TKey, QueryBuilder<TEntity, TKey>>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> UseQueryBuilder<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes, TImplementation>(factory);
        return this;
    }

    // Query Filters
    public EntityServiceBuilder<TContext, TEntity, TKey> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddFilter<TEntity, TKey, SearchObject<TKey>, TImplementation>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> AddFilter<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, SearchObject<TKey>>
    {
        Services.AddFilter<TEntity, TKey, SearchObject<TKey>, TImplementation>(factory);
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Filter(Func<IQueryable<TEntity>, SearchObject<TKey>?, IQueryable<TEntity>> filterFunc)
    {
        AddFilter(_ => new EntityQueryFilter<TEntity, TKey, SearchObject<TKey>>(filterFunc));
        return this;
    }

    // Default SortBy
    public EntityServiceBuilder<TContext, TEntity, TKey> SortBy(Func<IQueryable<TEntity>, IQueryable<TEntity>> sortBy)
    {
        Services.AddTransient<ISortedQueryBuilder<TEntity, TKey>>(_ => new SortedQueryBuilder<TEntity, TKey>(sortBy));
        return this;
    }

    // Default Includes
    public EntityServiceBuilder<TContext, TEntity, TKey> Includes(Func<IQueryable<TEntity>, EntityIncludes?, IQueryable<TEntity>> addIncludes)
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
    public EntityServiceBuilder<TContext, TEntity, TKey> SetPageSize(int? defaultPageSize = null, int? maxPageSize = null)
    {
        Services.AddSingleton(new EntityListOptions<TEntity> { DefaultPageSize = defaultPageSize, MaxPageSize = maxPageSize });
        return this;
    }

    // Read behavior (per-entity override of the global EntityReadOptions)
    /// <summary>
    /// Overrides the read-path behavior for this entity: whether the save endpoints re-fetch the entity via
    /// <c>Details(id)</c> for the response. When called it fully defines this entity's read behavior (no
    /// fallback to the global options).
    /// </summary>
    public EntityServiceBuilder<TContext, TEntity, TKey> SetReadBehavior(RefetchAfterSave refetchAfterSave = RefetchAfterSave.Always)
    {
        Services.AddSingleton(new EntityReadOptions<TEntity> { RefetchAfterSave = refetchAfterSave });
        return this;
    }

    // EntityNormalizers
    public EntityServiceBuilder<TContext, TEntity, TKey> AddNormalizer<TNormalizer>()
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        Services.AddNormalizer<TEntity, TNormalizer>();
        return this;
    }

    // Entity Processor
    public EntityServiceBuilder<TContext, TEntity, TKey> AddProcessor<TImplementation>()
        where TImplementation : class, IEntityProcessor<TEntity, EntityIncludes>
    {
        Services.AddProcessor<TEntity, EntityIncludes, TImplementation>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> AddProcessor<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityProcessor<TEntity, EntityIncludes>
    {
        Services.AddProcessor<TEntity, EntityIncludes, TImplementation>(factory);
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Process(Func<IList<TEntity>, EntityIncludes?, Task> process)
    {
        return AddProcessor(_ => new EntityProcessor<TEntity, EntityIncludes>(process));
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Process(Action<TEntity, EntityIncludes?> process)
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

    /* Writing */

    // Primers
    public EntityServiceBuilder<TContext, TEntity, TKey> AddPrimer<TPrimer>()
        where TPrimer : class, IEntityPrimer<TEntity>
    {
        Services.AddPrimer<TEntity, TPrimer>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> AddPrimer(Func<IServiceProvider, IEntityPrimer> factory)
    {
        Services.AddPrimer(factory);
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Prime(Action<TEntity> primeFunc)
    {
        Services.AddPrimer(_ => new EntityPrimer<TEntity>((item, _) =>
        {
            primeFunc(item);
            return Task.CompletedTask;
        }));
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Prime(Func<TEntity, EntityEntry, TContext, Task> primeFunc)
    {
        Services.AddPrimer(p => new EntityPrimer<TEntity>((item, entry)
            => primeFunc(item, entry, p.GetRequiredService<TContext>())));
        return this;
    }

    // Preppers
    public EntityServiceBuilder<TContext, TEntity, TKey> AddPrepper<TPrepper>()
        where TPrepper : class, IEntityPrepper<TEntity>
    {
        Services.AddPrepper<TEntity, TPrepper>();
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Prepare(Action<TEntity> prepareFunc)
    {
        Services.AddPrepper(prepareFunc);
        return this;
    }
    public EntityServiceBuilder<TContext, TEntity, TKey> Prepare(Func<TEntity, TContext, Task> prepareFunc)
    {
        Services.AddPrepper(prepareFunc);
        return this;
    }

    // Related
    /// <summary>
    /// Registers a <see cref="RelatedCollectionPrepper{TContext,TEntity,TRelated,TKey,TRelatedKey}"/> that automatically
    /// synchronises <paramref name="navigationExpression"/> with the incoming child collection before
    /// <c>SaveChanges()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>For owned child collections.</b> Use this when the child entities belong exclusively to the
    /// parent (e.g. order lines, invoice line items, join-table rows). Child entities registered with
    /// <c>Related()</c> do <b>not</b> need their own <c>.For&lt;TRelated&gt;()</c> registration.
    /// </para>
    /// <para>
    /// <b>Adding a <c>.For&lt;TRelated&gt;()</c> as well is allowed, but conditional.</b> The two registrations
    /// never conflict in DI — <c>Related()</c> registers only a save-time prepper for <i>this</i> parent, nothing
    /// for <typeparamref name="TRelated"/>. The hazard is at save time, and it depends entirely on what the
    /// parent's input DTO carries:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Collection absent (<c>null</c>) on the parent DTO — safe.</b> The sync short-circuits and touches no
    /// rows, so the child's own service is the only writer. Combine freely: give the child its own registration
    /// for dedicated read/PATCH endpoints, and keep the collection off the parent's <c>TInputDto</c>.
    /// </description></item>
    /// <item><description>
    /// <b>Collection present on the parent DTO — the parent wins.</b> Its next save re-diffs the collection and
    /// silently reverts (or deletes) rows written through the standalone service. Note an <b>empty list is not
    /// the same as <c>null</c></b>: <c>[]</c> deletes every existing row, while <c>null</c> is a no-op. This
    /// holds even when the navigation was never eager-loaded — the prepper falls back to loading the rows from
    /// the store, so there is no "I didn't include it, so it can't be deleted" safety net.
    /// </description></item>
    /// </list>
    /// <para>
    /// <b>Key-type caveat on deletes.</b> The diff only deletes originals when <i>every</i> incoming item has a
    /// non-null <c>Id</c>. With <c>int</c>/<c>Guid</c> keys that always holds. With a reference key type (e.g.
    /// <c>string</c>), a single new child carrying a null <c>Id</c> silently suppresses <b>all</b> deletions in
    /// that save — so a payload mixing additions and removals may add without removing. An empty list still
    /// deletes everything, since there is no incoming item to trip the check.
    /// </para>
    /// <para>
    /// So the rule is not "never both" — it is <b>one writer per save path</b>. If the parent must send the
    /// collection, don't also write those rows through the child's service; either drop the <c>.For&lt;&gt;()</c>
    /// or drop the <c>Related()</c> and load the navigation with <c>Include()</c> in the query builder.
    /// Startup validation warns about the dual registration because it cannot see your DTO shape.
    /// </para>
    /// </remarks>
    public EntityServiceBuilder<TContext, TEntity, TKey> Related<TRelated, TRelatedKey>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, TRelatedKey>>? configure = null)
        where TRelated : class, IEntity<TRelatedKey>
    {
        EntityRegistrationLog.TrackRelated(Services, typeof(TContext), typeof(TEntity), typeof(TRelated));
        Services.AddPrepper(p =>
        {
            if (configure != null)
            {
                var relatedBuilder = new RelatedEntityBuilder<TContext, TRelated, TRelatedKey>();
                configure(relatedBuilder);
                var nestedPreppers = relatedBuilder.PrepperFactories.Select(prepperFactory => prepperFactory(p));
                return new RelatedCollectionPrepper<TContext, TEntity, TRelated, TKey, TRelatedKey>(p.GetRequiredService<TContext>(), navigationExpression, nestedPreppers);
            }
            else
            {
                return new RelatedCollectionPrepper<TContext, TEntity, TRelated, TKey, TRelatedKey>(p.GetRequiredService<TContext>(), navigationExpression);
            }
        });
        if (prepareFunc != null)
        {
            Prepare(prepareFunc);
        }

        return this;
    }
}