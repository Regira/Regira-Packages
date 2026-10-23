#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Endpoints;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// The startup check of the handler overrides: a closed <c>IEntityRequestHandler&lt;TRequest, TResponse&gt;</c> for an
/// entity request whose type list neither the entity's controller nor its mapped endpoints send. The sender resolves the
/// exact closed type a surface sends, so such a handler is never reached by them and the default answers instead — an
/// override with a rule in it would be skipped without a word. A sender of the app's own may still send that type list,
/// so this warns rather than fails. Registered with the controller checks.
/// </summary>
internal sealed class EntityRequestHandlerValidator(EntityEndpointRegistry registry, ApplicationPartManager? partManager = null) : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var overrides = context.Services
            .Where(d => d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == typeof(IEntityRequestHandler<,>))
            .Select(d => (Request: d.ServiceType.GetGenericArguments()[0], Handler: d.ImplementationType))
            .Where(o => o.Request.IsGenericType && EntityRequestTypes.Definitions.Contains(o.Request.GetGenericTypeDefinition()))
            .ToList();
        if (overrides.Count == 0)
        {
            yield break;
        }

        var sent = SentRequests().ToLookup(x => x.EntityType, x => (x.Request, x.Surface));
        foreach (var (request, handler) in overrides)
        {
            var entityType = request.GetGenericArguments()[0];
            var surfaces = sent[entityType].ToList();
            // an entity no surface serves has nothing to compare with: its first send names what For<>() registered
            if (surfaces.Count == 0 || surfaces.Any(s => s.Request == request))
            {
                continue;
            }

            var definition = request.GetGenericTypeDefinition();
            var alike = surfaces.Where(s => s.Request.GetGenericTypeDefinition() == definition)
                .Select(s => $"{s.Surface} {Describe(s.Request)}")
                .Distinct()
                .ToList();
            var what = alike.Count > 0
                ? string.Join("; ", alike)
                : $"no surface of {entityType.Name} sends a {Describe(definition)} of this shape";
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{handler?.Name ?? "A registered handler"} answers {Describe(request)}, but {what}. " +
                "It is reached only by a sender of the app's own: register it for the type list the endpoints send.");
        }
    }

    private IEnumerable<(Type EntityType, Type Request, string Surface)> SentRequests()
    {
        foreach (var mapped in registry.Entities)
        {
            foreach (var request in mapped.Requests)
            {
                yield return (mapped.EntityType, request, "the mapped endpoints send");
            }
        }

        if (partManager == null)
        {
            yield break;
        }
        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);
        foreach (var controller in feature.Controllers)
        {
            if (EntityRequestTypes.OfController(controller.AsType()) is { } controllerRequests)
            {
                foreach (var request in controllerRequests)
                {
                    yield return (request.GetGenericArguments()[0], request, $"{controller.Name} sends");
                }
            }
        }
    }

    private static string Describe(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }
        var name = type.Name[..type.Name.IndexOf('`')];
        return type.IsGenericTypeDefinition
            ? name
            : $"{name}<{string.Join(", ", type.GetGenericArguments().Select(Describe))}>";
    }
}

/// <summary>The closed entity requests a surface sends for one entity, built from the type arguments it serves it with.</summary>
internal static class EntityRequestTypes
{
    public static readonly IReadOnlySet<Type> Definitions = new HashSet<Type>
    {
        typeof(DetailsQuery<,,>), typeof(ListQuery<,,,>), typeof(ListQuery<,,,,,>), typeof(SearchQuery<,,,>), typeof(SearchQuery<,,,,,>),
        typeof(SaveCommand<,,,>), typeof(PatchCommand<,,,>), typeof(DeleteCommand<,,>)
    };

    public static Type[] Simple(Type entity, Type key, Type searchObject, Type dto, Type inputDto) =>
    [
        typeof(DetailsQuery<,,>).MakeGenericType(entity, key, dto),
        typeof(ListQuery<,,,>).MakeGenericType(entity, key, searchObject, dto),
        typeof(SearchQuery<,,,>).MakeGenericType(entity, key, searchObject, dto),
        .. Writes(entity, key, dto, inputDto)
    ];

    public static Type[] Complex(Type entity, Type key, Type searchObject, Type sortBy, Type includes, Type dto, Type inputDto) =>
    [
        typeof(DetailsQuery<,,>).MakeGenericType(entity, key, dto),
        typeof(ListQuery<,,,,,>).MakeGenericType(entity, key, searchObject, sortBy, includes, dto),
        typeof(SearchQuery<,,,,,>).MakeGenericType(entity, key, searchObject, sortBy, includes, dto),
        .. Writes(entity, key, dto, inputDto)
    ];

    /// <summary>What an <c>EntityControllerBase&lt;...&gt;</c> subclass sends; <c>null</c> for any other controller.</summary>
    public static Type[]? OfController(Type controllerType)
    {
        for (var type = controllerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (!type.IsGenericType)
            {
                continue;
            }
            var definition = type.GetGenericTypeDefinition();
            var a = type.GetGenericArguments();
            if (definition == typeof(EntityControllerBase<,,,,>))
            {
                // TEntity, TKey, TSearchObject, TDto, TInputDto
                return Simple(a[0], a[1], a[2], a[3], a[4]);
            }
            if (definition == typeof(EntityControllerBase<,,,,,,>))
            {
                // TEntity, TKey, TSo, TSortBy, TIncludes, TDto, TInputDto
                return Complex(a[0], a[1], a[2], a[3], a[4], a[5], a[6]);
            }
        }
        return null;
    }

    private static Type[] Writes(Type entity, Type key, Type dto, Type inputDto) =>
    [
        typeof(SaveCommand<,,,>).MakeGenericType(entity, key, dto, inputDto),
        typeof(PatchCommand<,,,>).MakeGenericType(entity, key, dto, inputDto),
        typeof(DeleteCommand<,,>).MakeGenericType(entity, key, dto)
    ];
}
#endif
