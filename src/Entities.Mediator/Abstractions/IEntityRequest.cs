namespace Regira.Entities.Mediator.Abstractions;

/// <summary>
/// What any entity request tells about itself without its type arguments: the entity it is about and the operation.
/// A dispatcher that sees requests untyped — a mediator library's behaviour, around the app's own message — reads
/// these to tell a read from a write.
/// </summary>
public interface IEntityRequest
{
    Type EntityType { get; }
    EntityOperation Operation { get; }
}

/// <summary>
/// An entity request answered with <typeparamref name="TResponse"/>, sent through <see cref="IEntitySender"/> and
/// answered by the <see cref="IEntityRequestHandler{TRequest,TResponse}"/> for its type.
/// </summary>
public interface IEntityRequest<TResponse> : IEntityRequest;
