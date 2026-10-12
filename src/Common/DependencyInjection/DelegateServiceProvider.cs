namespace Regira.DependencyInjection;

/// <summary>
/// Provides a mechanism to create typed service providers for a specific context or scope.
/// </summary>
/// <typeparam name="T">
/// The type that defines the context or scope for the service provider.
/// </typeparam>
[Obsolete("Nothing in the Regira packages uses the typed service providers. Will be removed in v7.")]
public class DelegateServiceProvider<T>
{
    public static ITypedServiceProvider<T, TService> Create<TService>(IServiceProvider p, Func<IServiceProvider, TService> func)
        => new TypedServiceProvider<T, TService>(() => func(p));
}