#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Web.Controllers.Abstractions;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// The DTO shapes the entity controllers bind — <c>TEntity</c>, <c>TDto</c> and <c>TInputDto</c> of every
/// <c>EntityControllerBase&lt;...&gt;</c> — for the startup checks that judge DTOs. Declaring the DTOs on the controller
/// alone is the documented default, and without this those checks saw no DTO for such an entity: removing the
/// concurrency token from an input DTO went unreported while every PUT became last-write-wins.
/// </summary>
internal sealed class ControllerDtoShapeSource(ApplicationPartManager? partManager = null) : IEntityDtoShapeSource
{
    public IEnumerable<EntityMappingRegistration> GetDtoShapes()
    {
        if (partManager == null)
        {
            yield break; // MVC is not configured — no controllers bind anything
        }

        var feature = new ControllerFeature();
        partManager.PopulateFeature(feature);
        foreach (var controller in feature.Controllers)
        {
            if (Shape(controller.AsType()) is { } shape)
            {
                yield return shape;
            }
        }
    }

    internal static EntityMappingRegistration? Shape(Type controllerType)
    {
        for (var type = controllerType; type != null && type != typeof(object); type = type.BaseType)
        {
            if (!type.IsGenericType)
            {
                continue;
            }
            var definition = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();
            if (definition == typeof(EntityControllerBase<,,,,>))
            {
                // TEntity, TKey, TSearchObject, TDto, TInputDto
                return new EntityMappingRegistration(args[0], args[3], args[4]);
            }
            if (definition == typeof(EntityControllerBase<,,,,,,>))
            {
                // TEntity, TKey, TSo, TSortBy, TIncludes, TDto, TInputDto
                return new EntityMappingRegistration(args[0], args[5], args[6]);
            }
        }
        return null;
    }
}
#endif
