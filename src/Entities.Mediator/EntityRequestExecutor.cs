using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Handlers;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Web.Models.Abstractions;

namespace Regira.Entities.Mediator;

/// <summary>
/// <inheritdoc cref="IEntityRequestExecutor"/>
/// <para>
/// A built-in request needs no registration: its default handler is built from the request's own type arguments
/// (<c>DetailsQuery&lt;TEntity, TKey, TDto&gt;</c> is answered by <c>DetailsHandler&lt;TEntity, TKey, TDto&gt;</c>), since the
/// container cannot map one onto the other as an open generic. <c>Duration</c> is the elapsed time of the handler and
/// every behaviour, in milliseconds.
/// </para>
/// </summary>
public class EntityRequestExecutor(IServiceProvider services) : IEntityRequestExecutor
{
    private static readonly Dictionary<Type, Type> DefaultHandlers = new()
    {
        [typeof(DetailsQuery<,,>)] = typeof(DetailsHandler<,,>),
        [typeof(ListQuery<,,,>)] = typeof(ListHandler<,,,>),
        [typeof(ListQuery<,,,,,>)] = typeof(ListHandler<,,,,,>),
        [typeof(SearchQuery<,,,>)] = typeof(SearchHandler<,,,>),
        [typeof(SearchQuery<,,,,,>)] = typeof(SearchHandler<,,,,,>),
        [typeof(SaveCommand<,,,>)] = typeof(SaveHandler<,,,>),
        [typeof(PatchCommand<,,,>)] = typeof(PatchHandler<,,,>),
        [typeof(DeleteCommand<,,>)] = typeof(DeleteHandler<,,>),
    };

    private static readonly MethodInfo RunMethod =
        typeof(EntityRequestExecutor).GetMethod(nameof(Run), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly MethodInfo RunUntypedMethod =
        typeof(EntityRequestExecutor).GetMethod(nameof(RunUntyped), BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly ConcurrentDictionary<(Type Request, Type Response), Delegate> TypedRuns = new();
    private static readonly ConcurrentDictionary<Type, Func<IServiceProvider, IEntityRequest, CancellationToken, Task<object?>>> UntypedRuns = new();
    private static readonly ConcurrentDictionary<Type, ObjectFactory?> DefaultHandlerFactories = new();

    public virtual Task<TResponse?> Execute<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = (Func<IServiceProvider, IEntityRequest<TResponse>, CancellationToken, Task<TResponse?>>)TypedRuns.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => RunMethod.MakeGenericMethod(key.Request, key.Response)
                .CreateDelegate<Func<IServiceProvider, IEntityRequest<TResponse>, CancellationToken, Task<TResponse?>>>());
        return run(services, request, token);
    }

    public virtual Task<object?> Execute(IEntityRequest request, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var run = UntypedRuns.GetOrAdd(request.GetType(), static requestType =>
            RunUntypedMethod.MakeGenericMethod(requestType, ResponseTypeOf(requestType))
                .CreateDelegate<Func<IServiceProvider, IEntityRequest, CancellationToken, Task<object?>>>());
        return run(services, request, token);
    }

    private static async Task<TResponse?> Run<TRequest, TResponse>(IServiceProvider services, IEntityRequest<TResponse> request, CancellationToken token)
        where TRequest : IEntityRequest<TResponse>
    {
        var stopwatch = Stopwatch.StartNew();
        var typed = (TRequest)request;
        var handler = ResolveHandler<TRequest, TResponse>(services);

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

    private static async Task<object?> RunUntyped<TRequest, TResponse>(IServiceProvider services, IEntityRequest request, CancellationToken token)
        where TRequest : IEntityRequest<TResponse>
        => await Run<TRequest, TResponse>(services, (IEntityRequest<TResponse>)request, token);

    private static IEntityRequestHandler<TRequest, TResponse> ResolveHandler<TRequest, TResponse>(IServiceProvider services)
        where TRequest : IEntityRequest<TResponse>
    {
        var registered = services.GetService<IEntityRequestHandler<TRequest, TResponse>>();
        if (registered != null)
        {
            return registered;
        }

        var factory = DefaultHandlerFactories.GetOrAdd(typeof(TRequest), static requestType =>
            DefaultHandlerOf(requestType) is { } handlerType ? ActivatorUtilities.CreateFactory(handlerType, Type.EmptyTypes) : null);
        return factory != null
            ? (IEntityRequestHandler<TRequest, TResponse>)factory(services, null)
            : throw new InvalidOperationException(
                $"Nothing answers {Describe(typeof(TRequest))}: it has no default handler. " +
                $"Register an IEntityRequestHandler<{Describe(typeof(TRequest))}, {Describe(typeof(TResponse))}>.");
    }

    private static Type? DefaultHandlerOf(Type requestType)
        => requestType.IsGenericType && DefaultHandlers.TryGetValue(requestType.GetGenericTypeDefinition(), out var handler)
            ? handler.MakeGenericType(requestType.GetGenericArguments())
            : null;

    private static Type ResponseTypeOf(Type requestType)
    {
        var responses = requestType.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityRequest<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray();
        return responses.Length == 1
            ? responses[0]
            : throw new ArgumentException(
                $"{Describe(requestType)} implements IEntityRequest<TResponse> for {(responses.Length == 0 ? "no" : "more than one")} response type, so it cannot be executed untyped.",
                "request");
    }

    private static string Describe(Type type)
        => type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;
}
