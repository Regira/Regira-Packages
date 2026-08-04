using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Warns for entities where a <c>?q=</c> search will be silently ignored: the entity does not implement
/// <see cref="IHasNormalizedContent"/> (so the built-in Q filter never applies) and no custom
/// <c>IFilteredQueryBuilder</c> is registered that could consume the search object.
/// </summary>
internal sealed class QSearchValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var unsearchable = context.Registrations.Entities
            .Where(r => !typeof(IHasNormalizedContent).IsAssignableFrom(r.EntityType))
            .Where(r => !HasCustomFilter(context, r.EntityType))
            .Select(r => r.EntityType.Name)
            .Distinct()
            .ToArray();

        if (unsearchable.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"?q= text search is silently ignored for: {string.Join(", ", unsearchable)}. " +
                "These entities do not implement IHasNormalizedContent and have no custom IFilteredQueryBuilder — " +
                "a q parameter returns unfiltered results. Implement IHasNormalizedContent (with a [Normalized] source) or register a custom filter.");
        }
    }

    private static bool HasCustomFilter(EntityValidationContext context, Type entityType)
        => context.Services.Any(d =>
            d.ServiceType.IsGenericType
            && d.ServiceType.GetGenericTypeDefinition() == typeof(IFilteredQueryBuilder<,,>)
            && d.ServiceType.GetGenericArguments()[0] == entityType);
}
