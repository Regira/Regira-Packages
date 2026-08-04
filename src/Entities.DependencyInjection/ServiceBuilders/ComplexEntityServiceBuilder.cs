using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Processors;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Processing;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
    EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> builder)
    : EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(builder.Options)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    // Build
    public override void Build()
    {
        base.Build();

        // Query Builder
        if (!HasService<IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            AddDefaultQueryBuilder();
        }

        // Read Service
        if (!HasService<IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            UseReadService<EntityReadService<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }
        // Write Service
        if (!HasService<IEntityWriteService<TEntity, TKey>>())
        {
            UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }

        // Entity Repository
        if (!HasService<IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            HasRepositoryInner<EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }

        // Entity Service
        if (!HasEntityService())
        {
            UseEntityService<EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }
    }


    // Entity service
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddDefaultService()
        => UseEntityService<EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();

    /// <summary>
    /// Adds <typeparamref name="TService"/> implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey, TSearchObject, TSortBy, TIncludes}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        base.UseEntityService<TService>();
        Services.AddTransient<IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }
    /// <summary>
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey, TSearchObject, TSortBy, TIncludes}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> UseEntityService<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        base.UseEntityService(factory);
        Services.AddTransient(factory);
        return this;
    }

    // Entity Repository
    protected internal new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity, TKey>
    {
        Services.AddTransient<IEntityRepository<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }
    protected internal new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity, TKey>
    {
        Services.AddTransient(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>(factory);
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity, TKey>
    {
        UseEntityService<TService>();
        HasRepositoryInner<TService>();
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity, TKey>
    {
        UseEntityService(factory);
        HasRepositoryInner(factory);
        return this;
    }

    // Entity Manager
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        UseEntityService<TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        UseEntityService(factory);
        Services.AddTransient(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>(factory);
        return this;
    }

    // EntityNormalizers
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddNormalizer<TNormalizer>()
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        base.AddNormalizer<TNormalizer>();
        return this;
    }

    // Read Service
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        Services.AddTransient<IEntityReadService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityReadService<TEntity, TKey, TSearchObject>, TService>();
        Services.AddTransient<IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }

    /* Reading */

    // Query Builders
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>();
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TImplementation>();
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> UseQueryBuilder<TQueryBuilder>(Func<IServiceProvider, TQueryBuilder> factory)
        where TQueryBuilder : class, IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes, TQueryBuilder>(factory);
        return this;
    }

    // Query Filters
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    {
        Services.AddFilter<TEntity, TKey, TSearchObject, TImplementation>();
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddFilter<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    {
        Services.AddFilter<TEntity, TKey, TSearchObject, TImplementation>(factory);
        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> Filter(Func<IQueryable<TEntity>, TSearchObject?, IQueryable<TEntity>> filterFunc)
    {
        AddFilter(_ => new EntityQueryFilter<TEntity, TKey, TSearchObject>(filterFunc));
        return this;
    }

    // SortBy
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddSortBy<TImplementation>()
        where TImplementation : class, ISortedQueryBuilder<TEntity, TKey, TSortBy>
    {
        Services.AddTransient<ISortedQueryBuilder<TEntity, TKey, TSortBy>, TImplementation>();
        return this;
    }
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddSortBy<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, ISortedQueryBuilder<TEntity, TKey, TSortBy>
    {
        Services.AddTransient<ISortedQueryBuilder<TEntity, TKey, TSortBy>>(factory);
        return this;
    }
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> SortBy(Func<IQueryable<TEntity>, TSortBy?, IQueryable<TEntity>> sortByFunc)
    {
        Services.AddTransient<ISortedQueryBuilder<TEntity, TKey, TSortBy>>(_ => new SortedQueryBuilder<TEntity, TKey, TSortBy>(sortByFunc));
        return this;
    }

    // Includes
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddIncludes<TImplementation>()
        where TImplementation : class, IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    {
        Services.AddTransient<IIncludableQueryBuilder<TEntity, TKey, TIncludes>, TImplementation>();
        return this;
    }
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddIncludes<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IIncludableQueryBuilder<TEntity, TKey, TIncludes>
    {
        Services.AddTransient<IIncludableQueryBuilder<TEntity, TKey, TIncludes>>(factory);
        return this;
    }
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> Includes(Func<IQueryable<TEntity>, TIncludes?, IQueryable<TEntity>> addIncludes)
    {
        Services.AddTransient<IIncludableQueryBuilder<TEntity, TKey, TIncludes>>(_ => new IncludableQueryBuilder<TEntity, TKey, TIncludes>(addIncludes));
        return this;
    }

    // Entity Processor
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> Process(Func<IList<TEntity>, TIncludes?, Task> process)
    {
        Services.AddProcessor<TEntity, TIncludes, EntityProcessor<TEntity, TIncludes>>(_ => new EntityProcessor<TEntity, TIncludes>(process));
        return this;
    }
    public ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> Process(Action<TEntity, TIncludes?> process)
    {
        Services.AddProcessor<TEntity, TIncludes, EntityProcessor<TEntity, TIncludes>>(_ => new EntityProcessor<TEntity, TIncludes>((items, includes) =>
        {
            foreach (var item in items)
            {
                process(item, includes);
            }
            return Task.CompletedTask;
        }));

        return this;
    }
    public new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes> AddProcessor<TImplementation>()
        where TImplementation : class, IEntityProcessor<TEntity, TIncludes>
    {
        Services.AddTransient<IEntityProcessor<TEntity, TIncludes>, TImplementation>();
        return this;
    }
}
