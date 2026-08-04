namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Warns when an entity is synced through a parent's <c>Related()</c> <b>and</b> has its own <c>For&lt;&gt;()</c>
/// registration. This combination is legitimate — it is how an owned child gets its own read/PATCH endpoints —
/// but it is only safe while the parent's input DTO leaves the collection <c>null</c>, which makes the sync a
/// no-op. If the parent ever sends that collection, its save re-diffs it and silently overwrites whatever the
/// standalone service last wrote, with no exception, and invisible to tests that exercise only one path.
/// The validator cannot see DTO shapes, so it reports the pairing and leaves the judgement to the developer.
/// </summary>
internal sealed class WriteAuthorityValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var conflicts = context.Registrations.Related
            .Where(r => context.Registrations.Entities.Any(e => e.EntityType == r.RelatedType))
            .Select(r => $"{r.RelatedType.Name} (owned by {r.ParentType.Name}.Related)")
            .Distinct()
            .ToArray();

        if (conflicts.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Two write paths for: {string.Join(", ", conflicts)}. " +
                "Each is synced by a parent's Related() and also registered via For<>(). This combination is supported — it " +
                "is how an owned child gets its own endpoints — but it is UNSAFE unless the parent's input DTO leaves the " +
                "collection null. ACTION: open that input DTO and confirm the collection is absent. " +
                "If it is, the sync short-circuits and this warning is expected — suppress it. " +
                "If the parent does send the collection, its next save re-diffs and silently overwrites every row written " +
                "through the standalone service: matched rows get ALL their values rewritten from the payload (so fields " +
                "missing from the child input DTO reset to default), and an empty list deletes every row — only null is a " +
                "no-op. No exception is thrown, and tests exercising one path never see it. " +
                "To resolve: remove the collection from the parent's input DTO and give each field its own narrow endpoint, " +
                "or drop the For<>() registration (edit the child through the parent), or drop the Related() call and load " +
                "the navigation with Include() in the parent's query builder. " +
                "See entities.patterns → Single-field PATCH / state toggle, and → Owned children that are both " +
                "sortable and individually togglable.");
        }
    }
}
