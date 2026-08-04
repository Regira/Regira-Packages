namespace Regira.DependencyInjection;

/// <summary>
/// Provides a mechanism to create typed service providers for a specific context or scope.
/// </summary>
/// <typeparam name="T">
/// The type that defines the context or scope for the service provider.
/// </typeparam>
public class DelegateServiceProvider<T>
{
    public static ITypedServiceProvider<T, TService> Create<TService>(IServiceProvider p, Func<IServiceProvider, TService> func)
        => new TypedServiceProvider<T, TService>(() => func(p));
}