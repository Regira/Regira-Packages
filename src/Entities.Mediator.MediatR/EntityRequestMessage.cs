using MediatR;
using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator.MediatR;

/// <summary>
/// The one MediatR request every entity request travels in. It is a single closed type, so its handler is one closed
/// registration that competes with none of the app's own. A pipeline behaviour reads <see cref="IEntityRequest.EntityType"/>
/// and <see cref="IEntityRequest.Operation"/> on <see cref="Request"/> to tell a read from a write.
/// </summary>
public sealed record EntityRequestMessage(IEntityRequest Request) : IRequest<object?>;

/// <summary>Hands the entity request to the <see cref="IEntityRequestExecutor"/>, inside the app's MediatR pipeline.</summary>
public class EntityRequestMessageHandler(IEntityRequestExecutor executor) : IRequestHandler<EntityRequestMessage, object?>
{
    public virtual Task<object?> Handle(EntityRequestMessage message, CancellationToken cancellationToken)
        => executor.Execute(message.Request, cancellationToken);
}
