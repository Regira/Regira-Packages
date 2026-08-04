using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>(
    EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject> services)
    : ComplexEntityServiceBuilder<TContext, TEntity, int, TSearchObject, TSortBy, TIncludes>(services)
    where TContext : DbContext
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
    where TSortBy : struct, Enum
    where TIncludes : struct, Enum
{
    // Build
    public override void Build()
    {
        base.Build();

        // Query Builder
        if (!HasService<IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>>())
        {
            AddDefaultQueryBuilder();
        }

        // Read Service
        if (!HasService<IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes>>())
        {
            UseReadService<EntityReadService<TContext, TEntity, int, TSearchObject, TSortBy, TIncludes>>();
        }
        // Write Service
        if (!HasService<IEntityWriteService<TEntity, int>>())
        {
            UseWriteService<EntityWriteService<TContext, TEntity, int>>();
        }

        // Entity Repository
        if (!HasService<IEntityRepository<TEntity, int, TSearchObject, TSortBy, TIncludes>>())
        {
            HasRepositoryInner<EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
        }

        // Entity Service
        if (!HasEntityService())
        {
            UseEntityService<EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
        }
    }


    // Entity service
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddDefaultService()
        => UseEntityService<EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();

    /// <summary>
    /// Adds <typeparamref name="TService"/> implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey, TSearchObject, TSortBy, TIncludes}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
    {
        base.UseEntityService<TService>();
        Services.AddTransient<IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>, TService>();
        Services.AddTransient<IEntityService<TEntity>, TService>();
        return this;
    }
    /// <summary>
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TSearchObject, TSortBy, TIncludes}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> UseEntityService<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>
    {
        base.UseEntityService(factory);
        Services.AddTransient<IEntityService<TEntity>>(factory);
        Services.AddTransient<IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>>(factory);
        return this;
    }

    // Entity Repository
    protected internal new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        base.HasRepositoryInner<TService>();
        Services.AddTransient<IEntityRepository<TEntity>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }
    protected internal new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        base.HasRepository(factory);
        Services.AddTransient<IEntityRepository<TEntity>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>(factory);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        UseEntityService<TService>();
        HasRepositoryInner<TService>();
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        UseEntityService(factory);
        HasRepositoryInner(factory);
        return this;
    }

    // Entity Manager
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityManager<TEntity>
    {
        base.HasManager<TService>();
        UseEntityService<TService>();
        Services.AddTransient<IEntityManager<TEntity>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>, TService>();
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityManager<TEntity>
    {
        base.HasManager(factory);
        UseEntityService(factory);
        Services.AddTransient<IEntityManager<TEntity>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>>(factory);
        return this;
    }

    // EntityNormalizers
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddNormalizer<TNormalizer>()
    //    where TNormalizer : class, IEntityNormalizer<TEntity>
    //{
    //    base.AddNormalizer<TNormalizer>();
    //    return this;
    //}

    // Read Service
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> UseReadService<TService>()
    //    where TService : class, IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes>
    //{
    //    base.UseReadService<TService>();
    //    return this;
    //}

    // Query Builders
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>();
        Services.UseQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes, QueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>>();
        return this;
    }
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> UseQueryBuilder<TImplementation>()
    //    where TImplementation : class, IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>
    //{
    //    base.UseQueryBuilder<TImplementation>();
    //    return this;
    //}
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> UseQueryBuilder<TQueryBuilder>(Func<IServiceProvider, TQueryBuilder> factory)
    //    where TQueryBuilder : class, IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>
    //{
    //    base.UseQueryBuilder(factory);
    //    return this;
    //}

    // Query Filters
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddFilter<TImplementation>()
    //    where TImplementation : class, IFilteredQueryBuilder<TEntity, int, TSearchObject>
    //{
    //    base.AddFilter<TImplementation>();
    //    return this;
    //}
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddFilter<TImplementation>(Func<IServiceProvider, TImplementation> factory)
    //    where TImplementation : class, IFilteredQueryBuilder<TEntity, int, TSearchObject>
    //{
    //    base.AddFilter(factory);
    //    return this;
    //}
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Filter(Func<IQueryable<TEntity>, TSearchObject?, IQueryable<TEntity>> filterFunc)
    //{
    //    AddFilter(_ => new EntityQueryFilter<TEntity, int, TSearchObject>(filterFunc));
    //    return this;
    //}

    // SortBy
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddSortBy<TImplementation>()
        where TImplementation : class, ISortedQueryBuilder<TEntity, int, TSortBy>
    {
        base.AddSortBy<TImplementation>();
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddSortBy<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, ISortedQueryBuilder<TEntity, int, TSortBy>
    {
        base.AddSortBy(factory);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> SortBy(Func<IQueryable<TEntity>, TSortBy?, IQueryable<TEntity>> sortByFunc)
    {
        base.SortBy(sortByFunc);
        return this;
    }

    // Includes
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddIncludes<TImplementation>()
    //    where TImplementation : class, IIncludableQueryBuilder<TEntity, int, TIncludes>
    //{
    //    base.AddIncludes<TImplementation>();
    //    return this;
    //}
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddIncludes<TImplementation>(Func<IServiceProvider, TImplementation> factory)
    //    where TImplementation : class, IIncludableQueryBuilder<TEntity, int, TIncludes>
    //{
    //    base.AddIncludes<TImplementation>(factory);
    //    return this;
    //}
    //public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Includes(Func<IQueryable<TEntity>, TIncludes?, IQueryable<TEntity>> addIncludes)
    //{
    //    base.Includes(addIncludes);
    //    return this;
    //}

    // Entity Processor
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Process(Func<IList<TEntity>, TIncludes?, Task> process)
    {
        base.Process(process);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Process(Action<TEntity, TIncludes?> process)
    {
        base.Process(process);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddProcessor<TImplementation>()
        where TImplementation : class, IEntityProcessor<TEntity, TIncludes>
    {
        base.AddProcessor<TImplementation>();
        return this;
    }

    // Related
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated,TRelatedKey}" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, int>>? configure = null)
        where TRelated : class, IEntity<int>
    {
        Related<TRelated, int>(navigationExpression, prepareFunc, configure);

        return this;
    }
}