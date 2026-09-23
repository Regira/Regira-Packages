using Regira.Entities.Attachments.Abstractions;

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
/// Detected statically: the entity implements <see cref="IHasAttachments"/> (or a typed variant) and an input DTO
/// it is bound through — its last <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration, or else the
/// <c>TInputDto</c> of an entity controller (<see cref="EntityDtoShapes"/>) — lacks a public <c>Attachments</c>
/// collection property. An owner written through the entity itself, where the collection is always present, is
/// not reported.
/// </para>
/// </summary>
internal sealed class AttachmentsInputDtoValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var shapes = new EntityDtoShapes(context);
        if (shapes.FailureIssue("attachments input-DTO") is { } failure)
        {
            yield return failure;
        }

        foreach (var entityType in context.Registrations.Entities.Select(e => e.EntityType).Distinct().OrderBy(t => t.Name))
        {
            if (!IsAttachmentsOwner(entityType))
            {
                continue;
            }

            foreach (var mapping in shapes.For(entityType))
            {
                if (mapping.InputDtoType == entityType || HasAttachmentsCollection(mapping.InputDtoType))
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
