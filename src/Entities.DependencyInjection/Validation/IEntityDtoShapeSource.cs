using Regira.Entities.DependencyInjection.Mapping;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Declares the DTO shapes an app binds without a <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration, so the
/// startup checks that judge DTOs (concurrency tokens, the attachments collection) see them. Regira.Entities.Web
/// contributes one that reads the entity controllers' generic arguments — the documented default, where the DTOs
/// are declared on the controller alone. Internal plumbing between the two packages (<c>InternalsVisibleTo</c>),
/// not an extension point.
/// </summary>
internal interface IEntityDtoShapeSource
{
    IEnumerable<EntityMappingRegistration> GetDtoShapes();
}
