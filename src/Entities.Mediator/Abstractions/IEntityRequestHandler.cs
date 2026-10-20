namespace Regira.Entities.Mediator.Abstractions;

/// <summary>
/// Answers one request type. Every built-in request has a default handler that needs no registration; registering a
/// closed handler for a request type replaces that default for it — one operation of one entity:
/// <code>services.AddTransient&lt;IEntityRequestHandler&lt;DetailsQuery&lt;Product, int, ProductDto&gt;, DetailsResult&lt;ProductDto&gt;&gt;, ProductDetailsHandler&gt;();</code>
/// </summary>
public interface IEntityRequestHandler<in TRequest, TResponse>
    where TRequest : IEntityRequest<TResponse>
{
    /// <summary>
    /// <c>null</c> when the entity does not exist. A refused write throws: <c>EntityInputException</c>,
    /// <c>EntityConstraintException</c> and <c>EntityConcurrencyException</c> reach the caller.
    /// </summary>
    Task<TResponse?> Handle(TRequest request, CancellationToken token = default);
}

/// <summary>The rest of the pipeline, as a pipeline behaviour sees it.</summary>
public delegate Task<TResponse?> EntityRequestDelegate<TResponse>();

/// <summary>
/// Runs around the handler of every request of its type: caching, auditing the operation itself, and other cross-cutting
/// work at the operation level. Registered as an open generic it runs for every request:
/// <code>services.AddTransient(typeof(IEntityPipelineBehavior&lt;,&gt;), typeof(AuditBehavior&lt;,&gt;));</code>
/// Behaviours run in registration order, the first registered outermost. The executor writes <c>Duration</c> on the
/// result a request returns, so a behaviour that serves a cached result returns a copy (<c>cached with { }</c>), not the
/// instance it keeps.
/// </summary>
public interface IEntityPipelineBehavior<in TRequest, TResponse>
    where TRequest : IEntityRequest<TResponse>
{
    Task<TResponse?> Handle(TRequest request, EntityRequestDelegate<TResponse> next, CancellationToken token = default);
}
