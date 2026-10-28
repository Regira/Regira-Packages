using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mapping.Abstractions;

namespace Regira.Entities.Mapping.Mapster;

public class EntityMapConfigurator : IEntityMapConfigurator
{
    private readonly TypeAdapterConfig _config;

    // the pairs go into the shared config, not into the service collection
    public EntityMapConfigurator(IServiceCollection services, TypeAdapterConfig config)
        => _config = config;

    // ForType, not NewConfig: the pair becomes an explicit rule, and a rule the app set for it stays, whichever comes first
    public void Configure<TSource, TTarget>()
        => _config.ForType<TSource, TTarget>();

    public void Configure(Type sourceType, Type targetType)
        => _config.ForType(sourceType, targetType);
}
