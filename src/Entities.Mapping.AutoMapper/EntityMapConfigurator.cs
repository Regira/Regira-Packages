using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mapping.Abstractions;

namespace Regira.Entities.Mapping.AutoMapper;

public class EntityMapConfigurator(IServiceCollection services) : IEntityMapConfigurator
{
    public void Configure<TSource, TTarget>() 
        => services.AddAutoMapper(cfg => cfg.CreateMap<TSource, TTarget>());

    public void Configure(Type sourceType, Type targetType) 
        => services.AddAutoMapper(cfg => cfg.CreateMap(sourceType, targetType));
}