using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator.DependencyInjection;

public static class EntityMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-house <see cref="IEntitySender"/>. <c>UseEntities()</c> calls it, so an app needs no explicit
    /// call; the registration is a <c>TryAdd</c>, so a sender the app registered first stays.
    /// </summary>
    public static IServiceCollection AddEntityMediator(this IServiceCollection services)
    {
        services.TryAddScoped<IEntitySender, EntitySender>();
        return services;
    }
}
