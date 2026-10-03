using Regira.Entities.Validators;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports registered entity validators that apply to none of the registered entities, so they never run — the input
/// validation counterpart of <see cref="GlobalFilterScopeValidator"/>. The usual cause is a validator scoped to the wrong
/// type, or to a child row a parent's <c>Related()</c> sync writes: validators check the entity a write service saves,
/// and a child is checked through its parent's validators. It compiles, starts cleanly and checks nothing.
/// </summary>
internal sealed class EntityValidatorScopeValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var entityTypes = context.Registrations.Entities.Select(e => e.EntityType).Distinct().ToArray();
        if (entityTypes.Length == 0)
        {
            yield break;
        }

        if (!EntityValidatorDiagnostics.TryResolve(context, out var validators, out var issue))
        {
            yield return issue!;
            yield break;
        }

        var inert = validators
            .Select(validator => validator.GetType())
            .Where(validatorType => !entityTypes.Any(entityType => EntityValidatorDiagnostics.MayApplyTo(validatorType, entityType)));
        foreach (var validatorType in inert)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Entity validator '{EntityValidatorDiagnostics.Describe(validatorType)}' is scoped to " +
                $"{string.Join(", ", EntityValidatorScope.ScopesOf(validatorType).Select(EntityValidatorDiagnostics.Describe))}, " +
                "which none of the registered entities (or a type deriving from one) is, derives from or implements — so it " +
                "never runs. A validator applies to an item when the TScope of its IEntityValidator<TScope> is the item's type, " +
                "one of its base types or an interface it implements. ACTION: check that scope. The usual causes are scoping an interface the entity does not " +
                "implement, and scoping a child row a parent's Related() sync writes — validators check the entity a write " +
                "service saves, so validate a child from its parent's validator (Lines[0].Quantity). " +
                "See entities.instructions → Validators.");
        }
    }
}
