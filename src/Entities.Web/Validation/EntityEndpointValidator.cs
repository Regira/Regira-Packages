#if NETCOREAPP3_1_OR_GREATER
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Web.Endpoints;

namespace Regira.Entities.Web.Validation;

/// <summary>
/// The startup check of the mapped entity endpoints (<c>MapEntityEndpoints()</c>): mapped attachment downloads whose
/// DTOs would carry a null <c>Uri</c> because <c>UseAttachmentUris()</c> was not called. Registered with the controller
/// checks.
/// </summary>
internal sealed class EntityEndpointValidator(EntityEndpointRegistry registry) : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var mapped = registry.Entities;
        if (mapped.Count == 0)
        {
            yield break;
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
