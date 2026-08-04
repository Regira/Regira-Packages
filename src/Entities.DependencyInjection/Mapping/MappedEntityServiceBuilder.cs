using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Mapping;

public class MappedEntityServiceBuilder<TContext, TEntity, TKey>(EntityServiceCollectionOptions options)
    : EntityServiceBuilder<TContext, TEntity, TKey>(options)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    public MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TImplementation>()
        where TImplementation : class, IEntityAfterMapper
    {
        Services.AddTransient<IEntityAfterMapper, TImplementation>();
        return this;
    }
    public MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TImplementation>(Func<IServiceProvider, TImplementation> factory)
        where TImplementation : class, IEntityAfterMapper
    {
        Services.AddTransient<IEntityAfterMapper>(factory);
        return this;
    }
    public MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TSource, TTarget>(Action<TSource, TTarget> afterMapAction)
    {
        Services.AddTransient<IEntityAfterMapper>(_ => new EntityAfterMapper<TSource, TTarget>(afterMapAction));
        return this;
    }
    public MappedEntityServiceBuilder<TContext, TEntity, TKey> After<TSource, TTarget>(Func<IServiceProvider, Action<TSource, TTarget>> afterMapActionFactory)
    {
        Services.AddTransient<IEntityAfterMapper>(p => new EntityAfterMapper<TSource, TTarget>(afterMapActionFactory(p)));
        return this;
    }
}

public class MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto>(EntityServiceCollectionOptions options) : MappedEntityServiceBuilder<TContext, TEntity, TKey>(options)
    where TContext : DbContext
    where TEntity : class, IEntity<TKey>
{
    public MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto> After(Action<TEntity, TDto> afterMapAction)
    {
        After<TEntity, TDto>(afterMapAction);
        return this;
    }
    public MappedEntityServiceBuilder<TContext, TEntity, TKey, TDto, TInputDto> AfterInput(Action<TInputDto, TEntity> afterMapAction)
    {
        After(afterMapAction);
        return this;
    }
}