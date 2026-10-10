using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator.MediatR;

/// <summary>
/// Dispatches every entity request through MediatR: wrapped in an <see cref="EntityRequestMessage"/> and sent with the
/// app's <see cref="ISender"/>, so the app's pipeline behaviours run around it. Registered by <c>UseMediatR()</c>.
/// </summary>
public class MediatREntitySender(IServiceProvider services) : IEntitySender
{
    public virtual async Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
    {
        var sender = services.GetService<ISender>()
            ?? throw new InvalidOperationException(
                "UseMediatR() sends entity requests through MediatR, but MediatR is not registered. Call services.AddMediatR(...).");
        var response = await sender.Send(new EntityRequestMessage(request), token);
        return response switch
        {
            null => default,
            TResponse typed => typed,
            // a pipeline behaviour replaced the response: answering null would read as "not found"
            _ => throw new InvalidOperationException(
                $"MediatR answered a {request.GetType().Name} with a {response.GetType().Name} where a {typeof(TResponse).Name} was expected. " +
                "A pipeline behaviour returns what next() returned for an EntityRequestMessage.")
        };
    }
}
