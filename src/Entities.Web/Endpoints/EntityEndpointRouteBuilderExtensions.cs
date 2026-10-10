using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;

namespace Regira.Entities.Web.Endpoints;

public static class EntityEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps every entity registered through <c>For&lt;&gt;()</c> as minimal-API endpoints — the routes, request bodies and
    /// response envelopes of <c>EntityControllerBase</c>, and the attachment routes under an owner that <c>HasAttachments()</c>
    /// registered — so the app writes no controller. Each entity's route is the kebab-case plural of its name, under
    /// <see cref="EntityEndpointsOptions.Prefix"/>; <c>e.Endpoints(o => …)</c> on its registration changes the route, trims
    /// the set or leaves the entity out.
    /// <para>
    /// The endpoints answer a refused write as the controllers do (<see cref="EntityExceptionEndpointFilter"/>) and check a
    /// save's input DTO against its DataAnnotations. An entity needs its DTO pair declared — <c>UseMapping&lt;TDto, TInputDto&gt;()</c>
    /// or <c>e.Endpoints(o => o.UseDtos&lt;TDto, TInputDto&gt;())</c> — or mapping throws. An entity an <c>EntityControllerBase</c>
    /// subclass serves, and an attachment link an attachment controller serves, are left to the controller, so an app can
    /// move one entity at a time.
    /// </para>
    /// </summary>
    /// <returns>The group of every mapped entity, for what applies to all of them: <c>.RequireAuthorization()</c>.</returns>
    public static RouteGroupBuilder MapEntityEndpoints(this IEndpointRouteBuilder endpoints, Action<EntityEndpointsOptions>? configure = null)
    {
        var options = new EntityEndpointsOptions();
        configure?.Invoke(options);

        var mapper = EntityEndpointMapper.For(endpoints.ServiceProvider);
        var root = endpoints.MapGroup(options.Prefix);
        root.AddEndpointFilter<EntityExceptionEndpointFilter>();
        foreach (var registration in mapper.Registrations)
        {
            var entityType = registration.EntityType;
            if (mapper.OptionsFor(entityType).IsDisabled)
            {
                continue;
            }
            // a link entity is served under its owner's route, and the file store WithAttachments() registers (Attachment)
            // through the links — neither is a resource of its own
            if (typeof(IEntityAttachment).IsAssignableFrom(entityType) || typeof(IAttachment).IsAssignableFrom(entityType))
            {
                continue;
            }
            if (mapper.ControllerEntities.Contains(entityType))
            {
                mapper.Logger?.LogInformation("{Entity}: a controller serves it, so its endpoints are left to that controller.", entityType.Name);
                continue;
            }
            options.ApplyGroup(entityType, mapper.Map(root, registration));
        }
        return root;
    }

    /// <summary>
    /// Maps one entity registered through <c>For&lt;&gt;()</c>, as <see cref="MapEntityEndpoints"/> maps each — whether or not
    /// <c>e.Endpoints(o => o.Disable())</c> or a controller would keep it off that surface. Its route is
    /// <paramref name="route"/>, else its <c>e.Endpoints(o => o.Route = …)</c>, else the kebab-case plural of its name.
    /// </summary>
    public static RouteGroupBuilder MapEntity<TEntity>(this IEndpointRouteBuilder endpoints, string? route = null)
    {
        var mapper = EntityEndpointMapper.For(endpoints.ServiceProvider);
        var registration = mapper.Registrations.FirstOrDefault(r => r.EntityType == typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} has no For<>() registration, so there are no endpoints to map for it.");
        var group = mapper.Map(endpoints, registration, route);
        group.AddEndpointFilter<EntityExceptionEndpointFilter>();
        return group;
    }
}
