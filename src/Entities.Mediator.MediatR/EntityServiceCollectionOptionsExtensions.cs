using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.DependencyInjection;

namespace Regira.Entities.Mediator.MediatR;

public static class EntityServiceCollectionOptionsExtensions
{
    /// <summary>
    /// Dispatches every entity request through MediatR (<see cref="MediatREntitySender"/>), so the app's pipeline
    /// behaviours wrap the generated endpoints and every other caller of <see cref="IEntitySender"/>. Handler overrides,
    /// the <see cref="IEntityPipelineBehavior{TRequest,TResponse}"/>s and <c>Duration</c> stay as they are: the request is
    /// executed by the same <see cref="IEntityRequestExecutor"/>, from inside the MediatR pipeline.
    /// <para>
    /// Call it inside <c>UseEntities()</c>, or anywhere after: it replaces the in-house sender whatever the order. The app
    /// registers MediatR itself — <c>services.AddMediatR(...)</c>, with its licence key from MediatR 13 on.
    /// </para>
    /// </summary>
    public static EntityServiceCollectionOptions UseMediatR(this EntityServiceCollectionOptions options)
    {
        options.Services.AddEntityMediator();
        options.Services.Replace(ServiceDescriptor.Scoped<IEntitySender, MediatREntitySender>());
        options.Services.TryAddTransient<IRequestHandler<EntityRequestMessage, object?>, EntityRequestMessageHandler>();
        return options;
    }
}
