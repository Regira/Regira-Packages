using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Web.Models.Abstractions;

namespace Regira.Entities.Mediator;

/// <summary>
/// The in-house dispatch, registered by <c>UseEntities()</c>. It runs one request: resolves its handler — a registered
/// closed <see cref="IEntityRequestHandler{TRequest,TResponse}"/>, otherwise the default one — wraps it in the registered
/// <see cref="IEntityPipelineBehavior{TRequest,TResponse}"/>s and fills <c>Duration</c> on the result.
/// <para>
/// A built-in request needs no registration: its default handler is built from the request's own type arguments
/// (<c>DetailsQuery&lt;TEntity, TKey, TDto&gt;</c> is answered by <c>DetailsHandler&lt;TEntity, TKey, TDto&gt;</c>), since the
/// container cannot map one onto the other as an open generic. <c>Duration</c> is the elapsed time of the handler and
/// every behaviour, in milliseconds.
/// </para>
/// <para>
/// A sender that dispatches through a mediator library ends here too, by creating one of these from inside its library
/// (<c>ActivatorUtilities.CreateInstance&lt;EntitySender&gt;(services)</c>), so overrides, behaviours and <c>Duration</c>
/// stay the same whatever dispatches.
/// </para>
/// </summary>
public class EntitySender(IServiceProvider services) : IEntitySender
{
    private static readonly MethodInfo RunMethod =
        typeof(EntitySender).GetMethod(nameof(Run), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<(Type Request, Type Response), Delegate> Runs = new();

    public virtual Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = (Func<IServiceProvider, IEntityRequest<TResponse>, CancellationToken, Task<TResponse?>>)Runs.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => RunMethod.MakeGenericMethod(key.Request, key.Response)
                .CreateDelegate<Func<IServiceProvider, IEntityRequest<TResponse>, CancellationToken, Task<TResponse?>>>());
        return run(services, request, token);
    }

    private static async Task<TResponse?> Run<TRequest, TResponse>(IServiceProvider services, IEntityRequest<TResponse> request, CancellationToken token)
        where TRequest : IEntityRequest<TResponse>
    {
        var stopwatch = Stopwatch.StartNew();
        var typed = (TRequest)request;
        var handler = EntityRequestHandlers.Resolve<TRequest, TResponse>(services);

        EntityRequestDelegate<TResponse> next = () => handler.Handle(typed, token);
        // the first registered behaviour ends up outermost
        foreach (var behavior in services.GetServices<IEntityPipelineBehavior<TRequest, TResponse>>().Reverse())
        {
            var inner = next;
            next = () => behavior.Handle(typed, inner, token);
        }

        var response = await next();
        if (response is IEntityResult result)
        {
            result.Duration = stopwatch.ElapsedMilliseconds;
        }
        return response;
    }
}
