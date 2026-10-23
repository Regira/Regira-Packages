using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Handlers;
using Regira.Entities.Mediator.Requests;

namespace Regira.Entities.Mediator;

/// <summary>
/// Picks the handler of a request: a registered closed <see cref="IEntityRequestHandler{TRequest,TResponse}"/>, otherwise
/// the default one, built from the request's own type arguments (<c>DetailsQuery&lt;TEntity, TKey, TDto&gt;</c> is
/// answered by <c>DetailsHandler&lt;TEntity, TKey, TDto&gt;</c>), since the container cannot map one onto the other as an
/// open generic. The sender resolves through it, and so does a <c>PATCH</c> for the <c>SaveCommand</c> it ends in.
/// </summary>
internal static class EntityRequestHandlers
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
        [typeof(UploadAttachmentCommand<,,>)] = typeof(UploadAttachmentHandler<,,>),
        [typeof(UpdateAttachmentCommand<,,>)] = typeof(UpdateAttachmentHandler<,,>),
        [typeof(ReplaceAttachmentFileCommand<,>)] = typeof(ReplaceAttachmentFileHandler<,>),
        [typeof(AttachmentFileQuery<>)] = typeof(AttachmentFileHandler<>),
    };

    private static readonly ConcurrentDictionary<Type, ObjectFactory?> DefaultHandlerFactories = new();

    public static IEntityRequestHandler<TRequest, TResponse> Resolve<TRequest, TResponse>(IServiceProvider services)
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

    private static string Describe(Type type)
        => type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>"
            : type.Name;

    private static Type? DefaultHandlerOf(Type requestType)
        => requestType.IsGenericType && DefaultHandlers.TryGetValue(requestType.GetGenericTypeDefinition(), out var handler)
            ? handler.MakeGenericType(requestType.GetGenericArguments())
            : null;
}
