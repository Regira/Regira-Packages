using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Mapping.Abstractions;

namespace Regira.Entities.DependencyInjection.Mapping;

public static class ServiceCollectionMappingExtensions
{
    public class MappingConfigurator(EntityServiceCollectionOptions options)
    {
        public void SetMapConfigurator(Func<IServiceCollection, IEntityMapConfigurator> factory)
        {
            options.EntityMapConfiguratorFactory = factory;
        }
        public void SetMapper(Func<IServiceProvider, IEntityMapper> factory)
        {
            options.Services.AddTransient(factory);
        }
        public void SetMapper<TEntityMapper>()
            where TEntityMapper : class, IEntityMapper
        {
            options.Services.AddTransient<IEntityMapper, TEntityMapper>();
        }
    }

    public static EntityServiceCollectionOptions AddMapping(this EntityServiceCollectionOptions options, Action<MappingConfigurator> configure)
    {
        configure.Invoke(new MappingConfigurator(options));

        return options;
    }
    public static EntityServiceCollectionOptions AddMapping<TEntityMapper>(this EntityServiceCollectionOptions options,
        Func<IServiceCollection, IEntityMapConfigurator> configFactory)
        where TEntityMapper : class, IEntityMapper
        => AddMapping(options, c =>
        {
            c.SetMapConfigurator(configFactory);
            c.SetMapper<TEntityMapper>();
        });


    // AfterMappers
    public static EntityServiceCollectionOptions AddAfterMapper<TAfterMapper>(this EntityServiceCollectionOptions options)
        where TAfterMapper : class, IEntityAfterMapper
    {
        options.Services.AddTransient<IEntityAfterMapper, TAfterMapper>();
        return options;
    }
    public static EntityServiceCollectionOptions AfterMap<TSource, TTarget>(this EntityServiceCollectionOptions options, Action<TSource, TTarget> afterMapAction)
    {
        options.Services.AddTransient<IEntityAfterMapper>(p => new EntityAfterMapper<TSource, TTarget>(afterMapAction));
        return options;
    }
    public static EntityServiceCollectionOptions AfterMap<TSource, TTarget>(this EntityServiceCollectionOptions options, Func<IServiceProvider, Action<TSource, TTarget>> afterMapAction)
    {
        options.Services.AddTransient<IEntityAfterMapper>(p => new EntityAfterMapper<TSource, TTarget>(afterMapAction(p)));
        return options;
    }
}