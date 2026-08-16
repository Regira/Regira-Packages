using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Mapping;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports an attachments owner whose registered input DTO cannot carry the <c>Attachments</c> collection.
/// <para>
/// The attachments sync on the parent write path diffs the <em>incoming</em> collection against the stored
/// one and treats <c>null</c> as "collection not sent" — the correct contract for a client that omits it.
/// But when the owner's input DTO has no <c>Attachments</c> property at all, the convention map drops the
/// collection on every request, so "sent" is impossible: attachment adds, removes and reorders in the entity
/// payload are ignored with a 200 OK, no error and no log. The <c>/{id}/attachments</c> sub-routes still
/// work, which masks the gap until a user notices a removed file resurrecting after save.
/// </para>
/// <para>
/// Detected statically: the entity implements <see cref="IHasAttachments"/> (or a typed variant) and a
/// <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration exists whose input DTO lacks a public
/// <c>Attachments</c> collection property. An unmapped owner writes through the entity itself, where the
/// collection is always present, so it is not reported.
/// </para>
/// </summary>
internal sealed class AttachmentsInputDtoValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var mappings = context.Services
            .Where(d => d.ServiceType == typeof(EntityMappingRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityMappingRegistration>()
            .ToArray();

        foreach (var entityType in context.Registrations.Entities.Select(e => e.EntityType).Distinct().OrderBy(t => t.Name))
        {
            if (!IsAttachmentsOwner(entityType))
            {
                continue;
            }

            // Last-wins: UseMapping appends a registration per call and DI resolves the last one, so the
            // effective mapping is the last match. Reading the first would validate a superseded DTO —
            // warning about one no longer in use, or passing while the live one silently drops attachments.
            var mapping = mappings.LastOrDefault(m => m.EntityType == entityType);
            if (mapping == null || mapping.InputDtoType == entityType || HasAttachmentsCollection(mapping.InputDtoType))
            {
                continue;
            }

            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"{entityType.Name} implements IHasAttachments but its input DTO {mapping.InputDtoType.Name} has no Attachments collection the convention map can materialize. " +
                "Every PUT/PATCH through the entity controller maps the collection to null, and the attachments sync treats null as 'collection not sent': " +
                "attachment adds, removes and reorders in the entity payload are silently ignored — 200 OK, no error, no log. " +
                "The /{id}/attachments sub-routes still work, which masks it. " +
                $"ACTION: add `public ICollection<EntityAttachmentInputDto>? Attachments {{ get; set; }}` (or your derived attachment input DTO) to {mapping.InputDtoType.Name}. " +
                "See entities.instructions → Attachments.");
        }
    }

    /// <summary>The typed interfaces do not extend the non-generic marker, so both shapes are probed.</summary>
    private static bool IsAttachmentsOwner(Type entityType)
        => typeof(IHasAttachments).IsAssignableFrom(entityType)
           || entityType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IHasAttachments<,,,,>));

    private static bool HasAttachmentsCollection(Type inputDtoType)
        => inputDtoType.GetProperties().Any(p =>
            p.Name.Equals(nameof(IHasAttachments.Attachments), StringComparison.OrdinalIgnoreCase)
            && IsMaterializableAttachmentCollection(p.PropertyType));

    /// <summary>
    /// The property must be a generic enumerable whose element type the convention map can materialize as an
    /// attachment input: a non-primitive class (<c>EntityAttachmentInputDto</c> or a derived DTO). A name-only
    /// probe let <c>ICollection&lt;int&gt;? Attachments</c> pass while the map still dropped the collection on
    /// every request — the exact silent failure this check exists to catch.
    /// </summary>
    private static bool IsMaterializableAttachmentCollection(Type propertyType)
    {
        var elementType = new[] { propertyType }.Concat(propertyType.GetInterfaces())
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
        return elementType is { IsClass: true } && elementType != typeof(string);
    }
}
