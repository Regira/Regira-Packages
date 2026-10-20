namespace Regira.Entities.Mediator.Abstractions;

/// <summary>
/// What the controllers and every other caller — a domain action, a job, a seeder, a consumer's own endpoint — send
/// entity requests through. The in-house <see cref="EntitySender"/> is the default, registered by <c>UseEntities()</c>;
/// an app that dispatches through a mediator library replaces it with its own, which ends in an <see cref="EntitySender"/>.
/// </summary>
public interface IEntitySender
{
    /// <inheritdoc cref="IEntityRequestHandler{TRequest,TResponse}.Handle"/>
    Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default);
}
