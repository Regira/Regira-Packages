using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Processing.Abstractions;

namespace Regira.Entities.DependencyInjection.Processors;

public static class ServiceCollectionProcessorExtensions
{
    public static IServiceCollection AddProcessor<TEntity, TEntityIncludes, TImplementation>(this IServiceCollection services)
        where TEntityIncludes : struct, Enum
        where TImplementation : class, IEntityProcessor<TEntity, TEntityIncludes>
    {
        return services.AddTransient<IEntityProcessor<TEntity, TEntityIncludes>, TImplementation>();
    }

    public static IServiceCollection AddProcessor<TEntity, TEntityIncludes, TImplementation>(this IServiceCollection services, Func<IServiceProvider, TImplementation> factory)
        where TEntityIncludes : struct, Enum
        where TImplementation : class, IEntityProcessor<TEntity, TEntityIncludes>
    {
        return services.AddTransient<IEntityProcessor<TEntity, TEntityIncludes>>(factory);
    }
}