using Microsoft.Extensions.DependencyInjection;
using Regira.DependencyInjection;

namespace Regira.System.Hosting.DependencyInjection;

public static class TypedServiceProviderExtensions
{
    public static IServiceCollection AddTypedProvider<T, TService, TImplementation>(this IServiceCollection services)
        where TImplementation : class, TService
        => services
        .AddTransient<TImplementation>()
        .AddTransient(p => DelegateServiceProvider<T>.Create<TService>(p, p => p.GetService<TImplementation>()!));
    public static IServiceCollection AddTypedProvider<T, TService>(this IServiceCollection services, Func<IServiceProvider, TService> func)
        => services.AddTransient(p => DelegateServiceProvider<T>.Create(p, func));
    public static TService? GetTypedImplementation<T, TService>(this IServiceProvider sp)
    {
        var provider = sp.GetService<ITypedServiceProvider<T, TService>>();
        return provider != null ? provider.Provide() : default;
    }
    public static IEnumerable<TService> GetTypedImplementations<T, TService>(this IServiceProvider sp)
    {
        var providers = sp.GetServices<ITypedServiceProvider<T, TService>>();
        return providers.Select(p => p.Provide());
    }
}