using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// <c>e.Endpoints(o => …)</c> on a <c>For&lt;&gt;()</c> registration: how <c>MapEntityEndpoints()</c> maps the entity
/// (<see cref="EntityEndpointOptions"/>). Inert for an app that maps no endpoints. It returns the builder it was called
/// on, of its own type, so the registration chains on.
/// </summary>
public static class EntityEndpointBuilderExtensions
{
    public static TBuilder Endpoints<TBuilder>(this TBuilder builder, Action<EntityEndpointOptions> configure)
        where TBuilder : IEntityServiceBuilder
    {
        builder.Services.ConfigureEntityEndpoints(builder.EntityType, configure);
        return builder;
    }

    // one registration per entity: a second Endpoints() call adds to the options of the first
    private static void ConfigureEntityEndpoints(this IServiceCollection services, Type entityType, Action<EntityEndpointOptions> configure)
    {
        var registration = services
            .Where(d => d.ServiceType == typeof(EntityEndpointRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityEndpointRegistration>()
            .FirstOrDefault(r => r.EntityType == entityType);
        if (registration == null)
        {
            registration = new EntityEndpointRegistration(entityType, new EntityEndpointOptions());
            services.AddSingleton(registration);
        }
        configure(registration.Options);
    }
}
