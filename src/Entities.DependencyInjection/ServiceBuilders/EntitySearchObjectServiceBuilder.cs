using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(EntityServiceCollectionOptions options)
    : EntityServiceBuilder<TContext, TEntity, TKey>(options),
        IEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject>
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
    where TSearchObject : class, ISearchObject<TKey>, new()
{
    public new bool HasEntityService() => HasService<IEntityService<TEntity, TKey, TSearchObject>>();

    // Build
    public override void Build()
    {
        base.Build();

        // Query Builder
        if (!HasService<IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>>())
        {
            AddDefaultQueryBuilder();
        }
        // Read Service
        if (!HasService<IEntityReadService<TEntity, TKey, TSearchObject>>())
        {
            UseReadService<EntityReadService<TContext, TEntity, TKey, TSearchObject>>();
        }
        // Write Service
        if (!HasService<IEntityWriteService<TEntity, TKey>>())
        {
            UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }
        // Entity Repository
        if (!HasService<IEntityRepository<TEntity, TKey, TSearchObject>>())
        {
            HasRepositoryInner<EntityRepository<TEntity, TKey, TSearchObject>>();
        }
        // Entity Service
        if (!HasService<IEntityService<TEntity, TKey, TSearchObject>>())
        {
            UseEntityService<EntityRepository<TEntity, TKey, TSearchObject>>();
        }
    }



    // Entity services
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> AddDefaultService()
        => UseEntityService<EntityRepository<TEntity, TKey, TSearchObject>>();
    /// <summary> 
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey, TSearchObject}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity, TKey, TSearchObject>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityService<TEntity, TKey, TSearchObject>, TService>();
        return this;
    }
    /// <summary>
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity, TKey, TSearchObject}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseEntityService<TService>(Func<IServiceProvider, TService> factory)
        where TService : class, IEntityService<TEntity, TKey, TSearchObject>
    {
        Services.AddTransient<IEntityService<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityService<TEntity, TKey, TSearchObject>>(factory);
        Services.AddTransient(factory);
        return this;
    }

    // Read Service
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseReadService<TService>()
        where TService : class, IEntityReadService<TEntity, TKey, TSearchObject>
    {
        Services.AddTransient<IEntityReadService<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityReadService<TEntity, TKey, TSearchObject>, TService>();
        return this;
    }
    // Write Service
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseWriteService<TService>()
        where TService : class, IEntityWriteService<TEntity, TKey>
    {
        Services.AddTransient<IEntityWriteService<TEntity, TKey>, TService>();
        return this;
    }

    // Entity Repository
    protected internal new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, TSearchObject>
    {
        Services.AddTransient<IEntityRepository<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject>, TService>();
        return this;
    }
    protected internal new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey, TSearchObject>
    {
        Services.AddTransient(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityRepository<TEntity, TKey, TSearchObject>>(factory);
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
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TKey, TSearchObject>
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
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TKey>, IEntityRepository<TEntity, TKey, TSearchObject>
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
    ///     <item><see cref="IEntityManager{TEntity, TKey, TSearchObject}"/></item>
    /// </list>
    /// </summary>
    /// <typeparam name="TService"></typeparam>
    /// <returns></returns>
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TKey, TSearchObject>
    {
        UseEntityService<TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey>, TService>();
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject>, TService>();
        return this;
    }
    /// <summary>
    /// Adds implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityManager{TEntity}"/></item>
    ///     <item><see cref="IEntityManager{TEntity, TKey, TSearchObject}"/></item>
    /// </list>
    /// </summary>
    /// <param name="factory"></param>
    /// <returns></returns>
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TKey, TSearchObject>
    {
        UseEntityService(factory);
        Services.AddTransient(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey>>(factory);
        Services.AddTransient<IEntityManager<TEntity, TKey, TSearchObject>>(factory);
        return this;
    }

    // Query Builders
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TKey, TSearchObject>();
        Services.UseQueryBuilder<TEntity, TKey, TSearchObject, QueryBuilder<TEntity, TKey, TSearchObject>>();
        return this;
    }
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, TSearchObject, TImplementation>();
        return this;
    }
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> UseQueryBuilder<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TKey, TSearchObject, TImplementation>(factory);
        return this;
    }

    // Query Filters
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> AddFilter<TImplementation>()
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    {
        Services.AddFilter<TEntity, TKey, TSearchObject, TImplementation>();
        return this;
    }
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> AddFilter<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IFilteredQueryBuilder<TEntity, TKey, TSearchObject>
    {
        Services.AddFilter<TEntity, TKey, TSearchObject, TImplementation>(factory);
        return this;
    }
    public EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> Filter(Func<IQueryable<TEntity>, TSearchObject?, IQueryable<TEntity>> filterFunc)
    {
        AddFilter(_ => new EntityQueryFilter<TEntity, TKey, TSearchObject>(filterFunc));
        return this;
    }

    // Server-owned fields
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.ServerOwned{TProp}" />
    public new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> ServerOwned<TProp>(Expression<Func<TEntity, TProp>> selector, Func<TEntity, TProp>? mintOnCreate = null)
    {
        base.ServerOwned(selector, mintOnCreate);
        return this;
    }

    // Related
    /// <summary>
    /// Single-type-argument shortcut for <see cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated,TRelatedKey}" />
    /// for related entities with an <see cref="int"/> key. The related key is independent of the
    /// parent <typeparamref name="TKey"/>, so this shortcut is available regardless of the parent key type.
    /// </summary>
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated,TRelatedKey}" />
    public EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, int>>? configure = null)
        where TRelated : class, IEntity<int>
    {
        Related<TRelated, int>(navigationExpression, prepareFunc, configure);

        return this;
    }
}