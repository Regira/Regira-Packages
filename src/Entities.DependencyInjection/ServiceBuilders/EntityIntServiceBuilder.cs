using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Reactors.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class EntityIntServiceBuilder<TContext, TEntity>(EntityServiceCollectionOptions options)
    : EntityServiceBuilder<TContext, TEntity, int>(options),
        IEntityServiceBuilder<TContext, TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity<int>
{
    public new bool HasEntityService() => HasService<IEntityService<TEntity>>();

    // Entity service
    public new EntityIntServiceBuilder<TContext, TEntity> AddDefaultService()
        => UseEntityService<EntityRepository<TEntity>>();
    public new EntityIntServiceBuilder<TContext, TEntity> UseEntityService<TService>()
        where TService : class, IEntityService<TEntity>, IEntityService<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TService), null, EntityServiceFamily);
        return this;
    }
    /// <summary>
    /// Adds an implementation for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntity}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public new EntityIntServiceBuilder<TContext, TEntity> UseEntityService<TService>(Func<IServiceProvider, TService> factory)
        where TService : class, IEntityService<TEntity>, IEntityService<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TService), p => factory(p), EntityServiceFamily);
        return this;
    }

    // Entity Repository
    protected internal new EntityIntServiceBuilder<TContext, TEntity> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity>, IEntityRepository<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TService), null, RepositoryFamily);
        return this;
    }
    protected internal new EntityIntServiceBuilder<TContext, TEntity> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity>, IEntityRepository<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), RepositoryFamily);
        return this;
    }
    public new EntityIntServiceBuilder<TContext, TEntity> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity>, IEntityRepository<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TService), null, RepositoryFamily, EntityServiceFamily);
        return this;
    }
    public new EntityIntServiceBuilder<TContext, TEntity> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity>, IEntityRepository<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), RepositoryFamily, EntityServiceFamily);
        return this;
    }

    // Entity Manager
    public new EntityIntServiceBuilder<TContext, TEntity> HasManager<TService>()
        where TService : class, IEntityManager<TEntity>, IEntityManager<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TService), null, ManagerFamily, EntityServiceFamily);
        return this;
    }
    public new EntityIntServiceBuilder<TContext, TEntity> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity>, IEntityManager<TEntity, int, SearchObject<int>>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), ManagerFamily, EntityServiceFamily);
        return this;
    }

    // Query Builders
    public new EntityIntServiceBuilder<TContext, TEntity> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity>();
        Services.UseQueryBuilder<TEntity, QueryBuilder<TEntity>>();
        return this;
    }
    public new EntityIntServiceBuilder<TContext, TEntity> UseQueryBuilder<TImplementation>()
        where TImplementation : class, IQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TImplementation>();
        return this;
    }
    public new EntityIntServiceBuilder<TContext, TEntity> UseQueryBuilder<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>
    {
        Services.UseQueryBuilder<TEntity, TImplementation>(factory);
        return this;
    }

    // EntityNormalizers
    public new EntityIntServiceBuilder<TContext, TEntity> AddNormalizer<TNormalizer>()
        where TNormalizer : class, IEntityNormalizer<TEntity>
    {
        base.AddNormalizer<TNormalizer>();
        return this;
    }

    // Preppers
    public new EntityIntServiceBuilder<TContext, TEntity> Prepare(Func<TEntity, TContext, Task> prepareFunc)
    {
        Services.AddTransient<IEntityPrepper>(p => new EntityPrepper<TContext, TEntity>(p.GetRequiredService<TContext>(), prepareFunc));

        return this;
    }
    // Validators
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddValidator{TValidator}" />
    public new EntityIntServiceBuilder<TContext, TEntity> AddValidator<TValidator>()
        where TValidator : class, IEntityValidator<TEntity>
    {
        base.AddValidator<TValidator>();
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Action{IEntityValidatorContext{TEntity}})" />
    public new EntityIntServiceBuilder<TContext, TEntity> Validate(Action<IEntityValidatorContext<TEntity>> validate)
    {
        base.Validate(validate);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Func{IEntityValidatorContext{TEntity},Task})" />
    public new EntityIntServiceBuilder<TContext, TEntity> Validate(Func<IEntityValidatorContext<TEntity>, Task> validate)
    {
        base.Validate(validate);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Func{IEntityValidatorContext{TEntity},TContext,CancellationToken,Task})" />
    public new EntityIntServiceBuilder<TContext, TEntity> Validate(Func<IEntityValidatorContext<TEntity>, TContext, CancellationToken, Task> validate)
    {
        base.Validate(validate);
        return this;
    }
    // Reactors
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddReactor{TReactor}" />
    public new EntityIntServiceBuilder<TContext, TEntity> AddReactor<TReactor>()
        where TReactor : class, IEntityReactor<TEntity>
    {
        base.AddReactor<TReactor>();
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React(Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new EntityIntServiceBuilder<TContext, TEntity> React(Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(reactFunc);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React(Func{IEntityChange{TEntity},bool},Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new EntityIntServiceBuilder<TContext, TEntity> React(Func<IEntityChange<TEntity>, bool> canReact,
        Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(canReact, reactFunc);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React{TProp}(Expression{Func{TEntity,TProp}},TProp,Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new EntityIntServiceBuilder<TContext, TEntity> React<TProp>(Expression<Func<TEntity, TProp>> property, TProp value,
        Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(property, value, reactFunc);
        return this;
    }
    // Server-owned fields
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.ServerOwned{TProp}" />
    public new EntityIntServiceBuilder<TContext, TEntity> ServerOwned<TProp>(Expression<Func<TEntity, TProp>> selector, Func<TEntity, TProp>? mintOnCreate = null)
    {
        base.ServerOwned(selector, mintOnCreate);
        return this;
    }

    // Related
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated,TRelatedKey}" />
    public EntityIntServiceBuilder<TContext, TEntity> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression)
        where TRelated : class, IEntity<int>
    {
        EntityRegistrationLog.TrackRelated(Services, typeof(TContext), typeof(TEntity), typeof(TRelated));
        Services.AddPrepper(p => new RelatedCollectionPrepper<TContext, TEntity, TRelated, int, int>(p.GetRequiredService<TContext>(), navigationExpression, NestedPreppers.WithServerOwned<TRelated>()));

        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated}(Expression{Func{TEntity,ICollection{TRelated}}},Action{TEntity},Action{RelatedEntityBuilder{TContext,TRelated,int}})" />
    public new EntityIntServiceBuilder<TContext, TEntity> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<TEntity>? prepareFunc = null,
        Action<RelatedEntityBuilder<TContext, TRelated, int>>? configure = null)
        where TRelated : class, IEntity<int>
    {
        Related<TRelated, int>(navigationExpression, prepareFunc, configure);

        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Related{TRelated}(Expression{Func{TEntity,ICollection{TRelated}}},Action{RelatedEntityBuilder{TContext,TRelated,int}})" />
    public new EntityIntServiceBuilder<TContext, TEntity> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TRelated, int>> configure)
        where TRelated : class, IEntity<int>
    {
        Related<TRelated, int>(navigationExpression, null, configure);

        return this;
    }
}