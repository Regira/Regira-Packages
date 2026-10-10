namespace Regira.Entities.Mediator.Abstractions;

/// <summary>
/// What the controllers and every other caller — a domain action, a job, a seeder, a consumer's own endpoint — send
/// entity requests through. The in-house <see cref="EntitySender"/> is the default, registered by <c>UseEntities()</c>;
/// an adapter package replaces it with a third-party mediator, which then dispatches every entity request.
/// </summary>
public interface IEntitySender
{
    /// <inheritdoc cref="IEntityRequestHandler{TRequest,TResponse}.Handle"/>
    Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default);
}

/// <summary>
/// Runs one request: resolves its handler — a registered closed <see cref="IEntityRequestHandler{TRequest,TResponse}"/>,
/// otherwise the default one — wraps it in the registered <see cref="IEntityPipelineBehavior{TRequest,TResponse}"/>s and
/// fills <c>Duration</c> on the result. Every sender ends here: the in-house one directly, an adapter's from inside its
/// library, so overrides, behaviours and <c>Duration</c> are the same whatever dispatches.
/// </summary>
public interface IEntityRequestExecutor
{
    /// <inheritdoc cref="IEntityRequestHandler{TRequest,TResponse}.Handle"/>
    Task<TResponse?> Execute<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default);
    /// <summary>
    /// The same, for a request whose response type the caller does not know — an adapter, whose library hands the
    /// request back as <see cref="IEntityRequest"/>.
    /// </summary>
    Task<object?> Execute(IEntityRequest request, CancellationToken token = default);
}
