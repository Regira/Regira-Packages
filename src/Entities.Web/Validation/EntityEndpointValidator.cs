#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Web.Endpoints;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// The startup checks of the mapped entity endpoints (<c>MapEntityEndpoints()</c>, <c>MapEntity&lt;TEntity&gt;()</c>): an
/// entity or attachment link a controller serves too, and mapped attachment downloads whose DTOs would carry a null
/// <c>Uri</c> because <c>UseAttachmentUris()</c> was not called. Registered with the controller checks.
/// </summary>
internal sealed class EntityEndpointValidator(EntityEndpointRegistry registry, ApplicationPartManager? partManager = null) : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var mapped = registry.Entities;
        if (mapped.Count == 0)
        {
            yield break;
        }

        if (partManager != null)
        {
            var feature = new ControllerFeature();
            partManager.PopulateFeature(feature);
            var controllers = feature.Controllers.Select(c => c.AsType()).ToList();
            var controllerEntities = controllers.Select(c => ControllerDtoShapeSource.Shape(c)?.EntityType).OfType<Type>().ToHashSet();
            var controllerAttachments = controllers.SelectMany(ControllerRegistrationValidator.GetAttachmentEntities).ToHashSet();
            var servedTwice = mapped.Select(m => m.EntityType).Where(controllerEntities.Contains)
                .Concat(mapped.Select(m => m.Attachments?.AttachmentType).OfType<Type>().Where(controllerAttachments.Contains))
                .Select(t => t.Name)
                .ToArray();
            if (servedTwice.Length > 0)
            {
                yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                    $"Served by a controller and by mapped endpoints: {string.Join(", ", servedTwice)}. On one route a request matches both " +
                    "and fails as ambiguous. MapEntityEndpoints() leaves a controller's entity alone; MapEntity<TEntity>() maps it regardless, " +
                    "so give the mapped set a route of its own or drop one of the two.");
            }
        }

        // Mapping an owner's attachment downloads is the statement that clients download these files over HTTP, so a null
        // Uri resolver is a wiring slip, as it is for an attachment controller
        var withoutUris = mapped
            .Select(m => m.Attachments?.AttachmentType)
            .OfType<Type>()
            .Distinct()
            .Where(attachment => ControllerRegistrationValidator.IsNullUriResolver(context, attachment))
            .Select(attachment => attachment.Name)
            .ToArray();
        if (withoutUris.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Attachment DTOs will have a null Uri for: {string.Join(", ", withoutUris)}. Their download endpoints are mapped but " +
                "UseAttachmentUris() was not called on the same UseEntities options instance, so the null resolver is in place. " +
                "Add options.UseAttachmentUris() (plus AddHttpContextAccessor()), or have clients build download links from the " +
                "{objectId}/files/{fileName} route themselves.");
        }
    }
}

/// <summary>
/// The DTO pairs the mapped entity endpoints bind, for the startup checks that judge DTOs — the counterpart of
/// <see cref="ControllerDtoShapeSource"/> for an app without controllers.
/// </summary>
internal sealed class EntityEndpointDtoShapeSource(EntityEndpointRegistry registry) : IEntityDtoShapeSource
{
    public IEnumerable<EntityMappingRegistration> GetDtoShapes()
        => registry.Entities.Select(m => new EntityMappingRegistration(m.EntityType, m.DtoType, m.InputDtoType));
}
#endif
