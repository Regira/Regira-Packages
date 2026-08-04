using AutoMapper;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using System.Reflection;

namespace Regira.Entities.Mapping.AutoMapper;

public static class ServiceCollectionExtensions
{
    public static EntityServiceCollectionOptions UseAutoMapper(this EntityServiceCollectionOptions options, Action<IServiceProvider, IMapperConfigurationExpression>? configure = null)
    {
        options.Services
            .AddAutoMapper((p, e) =>
            {
                e.AllowNullCollections = true;

                configure?.Invoke(p, e);

            }, Array.Empty<Assembly>());

        options.AddMapping<EntityMapper>(services => new EntityMapConfigurator(services));

        return options;
    }
}