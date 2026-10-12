namespace Regira.DependencyInjection;

/// <summary>
/// Represents a typed service provider that provides instances of a specific service type.
/// </summary>
/// <typeparam name="TDefiner">
/// The type that defines the context or scope for the service provider.
/// </typeparam>
/// <typeparam name="TService">
/// The type of the service to be provided.
/// </typeparam>
[Obsolete("Nothing in the Regira packages uses the typed service providers. Will be removed in v7.")]
public class TypedServiceProvider<TDefiner, TService>(Func<TService> func) : ITypedServiceProvider<TDefiner, TService>
{
    public TService Provide()
        => func();
}
