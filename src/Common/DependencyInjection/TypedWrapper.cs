namespace Regira.DependencyInjection;

/// <summary>
/// Represents a wrapper for a specific service type, providing access to the wrapped service instance.
/// </summary>
/// <typeparam name="TService">The type of the service being wrapped.</typeparam>
public interface ITypedWrapper<out TService>
{
    TService Value { get; }
}

/// <summary>
/// Represents a strongly-typed wrapper for a service instance, allowing for dependency injection scenarios
/// where a specific definer type is associated with the service.
/// </summary>
/// <typeparam name="TDefiner">
/// The type that defines or categorizes the context of the wrapped service. This is typically used to 
/// distinguish between multiple instances of the same service type.
/// </typeparam>
/// <typeparam name="TService">
/// The type of the service being wrapped. This is the actual service instance that is provided 
/// through the wrapper.
/// </typeparam>
public class TypedWrapper<TDefiner, TService>(TService instance) : ITypedWrapper<TService>
{
    public TService Value => instance;
}