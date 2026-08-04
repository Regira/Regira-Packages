using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;

namespace Regira.Entities.Mapping.Mapster;

public static class ServiceCollectionExtensions
{
    // Holds the single shared TypeAdapterConfig so repeated UseMapsterMapping() calls (e.g. one per
    // UseEntities<TContext> stack) contribute to the SAME config instead of the previous approach where
    // each call registered its own config with AddSingleton (last-wins) — which silently dropped the
    // earlier stack's entity->DTO mappings unless it happened to be registered last. That ordering
    // dependency was the "register X before Y" fragility in multi-context apps.
    private sealed class MapsterConfigHolder(TypeAdapterConfig config)
    {
        public TypeAdapterConfig Config => config;
    }

    public static EntityServiceCollectionOptions UseMapsterMapping(this EntityServiceCollectionOptions options, Action<TypeAdapterConfig>? configure = null)
    {
        var holder = (MapsterConfigHolder?)options.Services
            .FirstOrDefault(d => d.ServiceType == typeof(MapsterConfigHolder))?.ImplementationInstance;

        TypeAdapterConfig config;
        if (holder != null)
        {
            // reuse the shared config from the first call; apply this call's configure to it
            config = holder.Config;
            configure?.Invoke(config);
        }
        else
        {
            config = new TypeAdapterConfig();
            // important to prevent stackoverflow!!
            config.Default.PreserveReference(true);
            // eager, same as every later call — deferring into the singleton factory would run this
            // delegate at first resolution (i.e. LAST), inverting the documented call order
            configure?.Invoke(config);

            options.Services.AddSingleton(new MapsterConfigHolder(config));
            options.Services
                .AddSingleton(config)
                .AddMapster();
        }

        options.AddMapping<EntityMapper>(services => new EntityMapConfigurator(services, config));

        return options;
    }
}