namespace Regira.DependencyInjection;

/// <summary>
/// Defines a contract for a typed service provider that supplies instances of a specific service type
/// within a defined context or scope.
/// </summary>
/// <typeparam name="T">
/// The type that specifies the context or scope for the service provider.
/// </typeparam>
/// <typeparam name="TService">
/// The type of the service that this provider supplies.
/// </typeparam>
[Obsolete("Nothing in the Regira packages uses the typed service providers. Will be removed in v7.")]
public interface ITypedServiceProvider<T, out TService>
{
    TService Provide();
}