using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator;

/// <summary>
/// The in-house dispatch: every request goes straight to the <see cref="IEntityRequestExecutor"/>. Registered by
/// <c>UseEntities()</c>; an adapter package replaces it with a third-party mediator.
/// </summary>
public class EntitySender(IEntityRequestExecutor executor) : IEntitySender
{
    public virtual Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
        => executor.Execute(request, token);
}
