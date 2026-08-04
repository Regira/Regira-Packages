using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;

public class EntityServiceCollectionBase(EntityServiceCollectionOptions options) : ServiceCollectionWrapper(options.Services)
{
    protected internal EntityServiceCollectionOptions Options => options;


    public EntityServiceCollectionBase For<TEntity>(Action<EntityServiceBuilderBase<TEntity, int>>? configure = null)
        where TEntity : class, IEntity<int>
    {
        var builder = new EntityServiceBuilderBase<TEntity, int>(Options);
        configure?.Invoke(builder);
        return this;
    }

    public EntityServiceCollectionBase For<TEntity, TKey>(Action<EntityServiceBuilderBase<TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>
    {
        var builder = new EntityServiceBuilderBase<TEntity, TKey>(Options);
        configure?.Invoke(builder);
        return this;
    }

    public EntityServiceCollectionBase For<TEntity, TKey, TSearchObject>(Action<EntityServiceBuilderBase<TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
    {
        var builder = new EntityServiceBuilderBase<TEntity, TKey>(Options);
        configure?.Invoke(builder);
        return this;
    }

    public EntityServiceCollectionBase For<TEntity, TSearchObject, TSortBy, TIncludes>(Action<EntityServiceBuilderBase<TEntity, int>>? configure = null)
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        var builder = new EntityServiceBuilderBase<TEntity, int>(Options);
        configure?.Invoke(builder);
        return this;
    }

    public EntityServiceCollectionBase For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(Action<EntityServiceBuilderBase<TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        var builder = new EntityServiceBuilderBase<TEntity, TKey>(Options);
        configure?.Invoke(builder);
        return this;
    }


    public EntityServiceCollectionBase AddTransient<TService>()
        where TService : class
        => AddTransient<TService, TService>();
    public EntityServiceCollectionBase AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.AddTransient<TService, TImplementation>();
        return this;
    }
    public EntityServiceCollectionBase AddTransient<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        Services.AddTransient(factory);
        return this;
    }
}

