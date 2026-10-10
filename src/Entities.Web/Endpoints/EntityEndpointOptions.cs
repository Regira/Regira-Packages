using Microsoft.AspNetCore.Routing;

namespace Regira.Entities.Web.Endpoints;

/// <summary>
/// How <c>MapEntityEndpoints()</c> maps one entity, set with <c>e.Endpoints(o => …)</c> on its <c>For&lt;&gt;()</c>
/// registration. Without it the entity maps every endpoint under the kebab-case plural of its name.
/// </summary>
public class EntityEndpointOptions
{
    private readonly HashSet<EntityEndpoint> _excluded = [];
    private readonly HashSet<EntityEndpoint> _anonymous = [];

    /// <summary>
    /// The entity's route, relative to the prefix <c>MapEntityEndpoints()</c> takes. <c>null</c> = the kebab-case plural of the
    /// entity's name (<c>Product</c> → <c>products</c>, <c>InterventionType</c> → <c>intervention-types</c>); an irregular
    /// plural (<c>Person</c>) needs it.
    /// </summary>
    public string? Route { get; set; }
    /// <summary>Whether <c>MapEntityEndpoints()</c> leaves the entity out (<see cref="Disable"/>).</summary>
    public bool IsDisabled { get; private set; }
    public IReadOnlySet<EntityEndpoint> Excluded => _excluded;
    public IReadOnlySet<EntityEndpoint> Anonymous => _anonymous;
    /// <summary>The read DTO the endpoints answer with; <c>null</c> = the one <c>UseMapping&lt;TDto, TInputDto&gt;()</c> declared.</summary>
    public Type? DtoType { get; private set; }
    /// <summary>The input DTO the endpoints bind; <c>null</c> = the one <c>UseMapping&lt;TDto, TInputDto&gt;()</c> declared.</summary>
    public Type? InputDtoType { get; private set; }

    /// <summary>Keeps the entity off the mapped surface — for one served by a controller of its own or by nothing at all.</summary>
    public EntityEndpointOptions Disable()
    {
        IsDisabled = true;
        return this;
    }

    /// <summary>Leaves these endpoints out of the entity's set.</summary>
    public EntityEndpointOptions Exclude(params EntityEndpoint[] endpoints)
    {
        _excluded.UnionWith(endpoints);
        return this;
    }

    /// <summary>
    /// Opens these endpoints to anonymous callers when a policy secures the rest — <see cref="EntityEndpoint.Download"/>
    /// for files an <c>&lt;img&gt;</c> loads.
    /// </summary>
    public EntityEndpointOptions AllowAnonymous(params EntityEndpoint[] endpoints)
    {
        _anonymous.UnionWith(endpoints);
        return this;
    }

    /// <summary>
    /// The DTO pair the endpoints answer with and bind, when the entity declares none with <c>UseMapping</c>.
    /// <c>UseDtos&lt;Product, Product&gt;()</c> serves the entity as itself.
    /// </summary>
    public EntityEndpointOptions UseDtos<TDto, TInputDto>()
        where TDto : class
        where TInputDto : class
    {
        DtoType = typeof(TDto);
        InputDtoType = typeof(TInputDto);
        return this;
    }

    internal bool Maps(EntityEndpoint endpoint) => !_excluded.Contains(endpoint);
}

/// <summary>The <see cref="EntityEndpointOptions"/> of one entity, as <c>e.Endpoints(…)</c> registers them.</summary>
public sealed record EntityEndpointRegistration(Type EntityType, EntityEndpointOptions Options);

/// <summary>What <c>MapEntityEndpoints()</c> applies to all of the entities it maps.</summary>
public class EntityEndpointsOptions
{
    private readonly Dictionary<Type, Action<RouteGroupBuilder>> _groups = [];

    /// <summary>A base path for every entity route — what a route-prefix convention is for the controllers. Empty by default.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>
    /// Configures one entity's route group — a policy, a rate limit or a filter that differs from the rest; what applies
    /// to all of them goes on the group <c>MapEntityEndpoints()</c> returns.
    /// </summary>
    public EntityEndpointsOptions ConfigureGroup<TEntity>(Action<RouteGroupBuilder> configure)
    {
        _groups[typeof(TEntity)] = _groups.TryGetValue(typeof(TEntity), out var existing) ? existing + configure : configure;
        return this;
    }

    internal void ApplyGroup(Type entityType, RouteGroupBuilder group)
    {
        if (_groups.TryGetValue(entityType, out var configure))
        {
            configure(group);
        }
    }
}
