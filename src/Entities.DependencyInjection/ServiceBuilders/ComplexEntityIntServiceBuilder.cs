using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.Processing.Abstractions;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Reactors.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;
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
        RegisterAs(typeof(TService), null, EntityServiceFamily);
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
        RegisterAs(typeof(TImplementation), p => factory(p), EntityServiceFamily);
        return this;
    }

    // Entity Repository
    protected internal new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TService>()
        where TService : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        RegisterAs(typeof(TService), null, RepositoryFamily);
        return this;
    }
    protected internal new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepositoryInner<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), RepositoryFamily);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepository<TService>()
        where TService : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        RegisterAs(typeof(TService), null, RepositoryFamily, EntityServiceFamily);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasRepository<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityRepository<TEntity>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), RepositoryFamily, EntityServiceFamily);
        return this;
    }

    // Entity Manager
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasManager<TService>()
        where TService : class, IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityManager<TEntity>
    {
        RegisterAs(typeof(TService), null, ManagerFamily, EntityServiceFamily);
        return this;
    }
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> HasManager<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityManager<TEntity, TSearchObject, TSortBy, TIncludes>, IEntityManager<TEntity>
    {
        RegisterAs(typeof(TImplementation), p => factory(p), ManagerFamily, EntityServiceFamily);
        return this;
    }

    // Query Builders
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddDefaultQueryBuilder()
    {
        Services.AddDefaultQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>();
        Services.UseQueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes, QueryBuilder<TEntity, TSearchObject, TSortBy, TIncludes>>();
        return this;
    }

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

    // Server-owned fields
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.ServerOwned{TProp}" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> ServerOwned<TProp>(Expression<Func<TEntity, TProp>> selector, Func<TEntity, TProp>? mintOnCreate = null)
    {
        base.ServerOwned(selector, mintOnCreate);
        return this;
    }

    // Validators
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddValidator{TValidator}" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddValidator<TValidator>()
        where TValidator : class, IEntityValidator<TEntity>
    {
        base.AddValidator<TValidator>();
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Action{IEntityValidatorContext{TEntity}})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Validate(Action<IEntityValidatorContext<TEntity>> validate)
    {
        base.Validate(validate);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Func{IEntityValidatorContext{TEntity},Task})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Validate(Func<IEntityValidatorContext<TEntity>, Task> validate)
    {
        base.Validate(validate);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.Validate(Func{IEntityValidatorContext{TEntity},TContext,CancellationToken,Task})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Validate(Func<IEntityValidatorContext<TEntity>, TContext, CancellationToken, Task> validate)
    {
        base.Validate(validate);
        return this;
    }

    // Reactors
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddReactor{TReactor}" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> AddReactor<TReactor>()
        where TReactor : class, IEntityReactor<TEntity>
    {
        base.AddReactor<TReactor>();
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React(Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> React(Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(reactFunc);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React(Func{IEntityChange{TEntity},bool},Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> React(Func<IEntityChange<TEntity>, bool> canReact,
        Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(canReact, reactFunc);
        return this;
    }
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.React{TProp}(Expression{Func{TEntity,TProp}},TProp,Func{IEntityChange{TEntity},IServiceProvider,CancellationToken,Task})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> React<TProp>(Expression<Func<TEntity, TProp>> property, TProp value,
        Func<IEntityChange<TEntity>, IServiceProvider, CancellationToken, Task> reactFunc)
    {
        base.React(property, value, reactFunc);
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
    /// <inheritdoc cref="EntitySearchObjectServiceBuilder{TContext,TEntity,TKey,TSearchObject}.Related{TRelated}(Expression{Func{TEntity,ICollection{TRelated}}},Action{RelatedEntityBuilder{TContext,TRelated,int}})" />
    public new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes> Related<TRelated>(
        Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TRelated, int>> configure)
        where TRelated : class, IEntity<int>
    {
        Related<TRelated, int>(navigationExpression, null, configure);

        return this;
    }
}