using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.QueryBuilders;
using Regira.Entities.QueryBuilders.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports registered global filters that apply to none of the registered entities, so they never run.
/// <para>
/// For a custom filter this is silent and security-relevant: the usual cause is scoping it to a type no
/// registered entity satisfies, and the symptom is rows that were meant to be hidden being returned in
/// full. Nothing else catches it — it compiles, starts clean, and DI validation is satisfied because the
/// filter really is constructed; it is simply never selected for any entity.
/// </para>
/// <para>
/// Regira's own filters are not checked: <c>UseDefaults()</c> registers the whole set whether or not the app has
/// an <c>IArchivable</c> (or timestamped, or normalized-content) entity, so an inert one carries no signal.
/// </para>
/// </summary>
internal sealed class GlobalFilterScopeValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var entityTypes = context.Registrations.Entities.Select(e => e.EntityType).Distinct().ToArray();
        if (entityTypes.Length == 0)
        {
            yield break;
        }

        var inert = context.Provider.GetServices<IGlobalFilteredQueryBuilder>()
            .Select(filter => filter.GetType())
            .Distinct()
            .Where(filterType => !IsRegiras(filterType))
            .Where(filterType => !entityTypes.Any(entityType => GlobalFilterScope.AppliesTo(filterType, entityType)));
        foreach (var filterType in inert)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Global filter '{filterType.Name}' applies to none of the registered entities, so it never runs. " +
                "A filter applies to an entity when the type it scopes — the TEntity of " +
                "GlobalFilteredQueryBuilderBase<TEntity> — is that entity, one of its base types, or an interface it " +
                "implements. ACTION: check that scope. The usual causes are scoping an interface the entity does not " +
                "actually implement, and registering the filter for an entity that has no For<>() registration. " +
                "If this filter guards row-level access, the rows it was meant to hide are currently being returned. " +
                "See entities.instructions → Security & Authorization.");
        }
    }

    /// <summary>A filter one of Regira's packages declares, closed over any type argument.</summary>
    private static bool IsRegiras(Type filterType)
        => (filterType.IsGenericType ? filterType.GetGenericTypeDefinition() : filterType).Assembly.GetName().Name?.StartsWith("Regira.", StringComparison.Ordinal) == true;
}
