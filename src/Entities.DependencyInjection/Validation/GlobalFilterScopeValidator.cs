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
/// filter really is constructed; it is simply never selected for any entity. Those are reported per filter
/// as warnings.
/// </para>
/// <para>
/// Filters the defaults contributed are different: <c>UseDefaults()</c> registers the whole set whether or
/// not the app has an <c>IArchivable</c> (or timestamped, or normalized-content) entity, so an inert one
/// carries no signal. Those collapse into a single informational line — giving them the security wording
/// would teach readers to skim past the message that matters. Membership is recorded at registration, not
/// inferred from the type, so the same built-in registered <i>deliberately</i> still warns when it is inert.
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

        IGlobalFilteredQueryBuilder[] filters;
        string? resolutionError = null;
        try
        {
            // Resolving user-supplied services can throw; a validator must never break startup with
            // its own diagnostics.
            filters = context.Provider.GetServices<IGlobalFilteredQueryBuilder>().ToArray();
        }
        catch (Exception ex)
        {
            filters = [];
            resolutionError = ex.Message;
        }

        if (resolutionError != null)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Info,
                $"Could not resolve global filters, so their scope was not checked: {resolutionError}");
            yield break;
        }

        var contributedByDefaults = context.Registrations.DefaultGlobalFilters;
        var inert = filters
            .Select(filter => filter.GetType())
            .Distinct()
            .Where(filterType => !entityTypes.Any(entityType => GlobalFilterScope.AppliesTo(filterType, entityType)))
            .ToArray();

        // UseDefaults() registers the whole built-in set regardless of which capability interfaces the app's
        // entities actually implement, so an inert one it contributed is the normal case, not a finding.
        // Reporting each with the security wording below trains readers to skim past it — which is exactly the
        // habit that hides a genuinely mis-scoped filter. Membership comes from what the defaults actually
        // registered, so the same type registered deliberately still warns.
        var inertDefaults = inert.Where(contributedByDefaults.Contains).ToArray();
        if (inertDefaults.Length > 0)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Info,
                $"Built-in global filters that never run: {string.Join(", ", inertDefaults.Select(FriendlyName))}. " +
                "No registered entity implements the capability each one scopes (IArchivable, IHasCreated, " +
                "IHasLastModified, IHasNormalizedContent). UseDefaults() registers the full set regardless, so this " +
                "is expected and needs no action.");
        }

        foreach (var filterType in inert.Where(t => !contributedByDefaults.Contains(t)))
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

    /// <summary>Type name without the CLR arity suffix, so <c>FilterIdsQueryBuilder`1</c> reads as written.</summary>
    private static string FriendlyName(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`');
        return arity < 0 ? name : name[..arity];
    }
}
