using Mapster;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mapping.Abstractions;

namespace Regira.Entities.Mapping.Mapster;

public class EntityMapConfigurator(IServiceCollection services, TypeAdapterConfig config) : IEntityMapConfigurator
{
    public void Configure<TSource, TTarget>()
        => services.AddTransient(_ => config.NewConfig<TSource, TTarget>());

    public void Configure(Type sourceType, Type targetType)
        => services.AddTransient(_ => config.NewConfig(sourceType, targetType));
}