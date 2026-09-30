using System.Collections.Concurrent;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Validators;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.Validation.FluentValidation;

/// <summary>
/// Runs the registered FluentValidation validators in the entity write pipeline. Scoped to <see cref="IEntity"/>, so it
/// checks every entity, and it follows the same scope rule as an <see cref="IEntityValidator{TScope}"/>: an
/// <c>AbstractValidator&lt;T&gt;</c> runs when <c>T</c> is the entity's type, one of its base types or an interface it
/// implements — <c>AbstractValidator&lt;IHasTenantId&gt;</c> checks every tenant-owned entity.
/// <para>
/// Each write runs the rules outside any rule set plus <see cref="EntityRuleSets.Add"/> or <see cref="EntityRuleSets.Modify"/>,
/// and a delete runs <see cref="EntityRuleSets.Remove"/> alone. Rules are run with <c>ValidateAsync</c>, so <c>MustAsync</c>
/// rules can query the database, and read the write through <see cref="ValidationContextExtensions.GetOriginal{T}"/> and
/// <see cref="ValidationContextExtensions.GetOperation{T}"/>. Only failures of <see cref="Severity.Error"/> reject the write.
/// </para>
/// </summary>
public class FluentEntityValidator(IServiceProvider services) : EntityValidatorBase<IEntity>, ISelectiveEntityValidator
{
    private delegate Task Dispatch(IServiceProvider services, IEntityValidatorContext context, CancellationToken token);

    private static readonly MethodInfo ValidateAsMethod =
        typeof(FluentEntityValidator).GetMethod(nameof(ValidateAs), BindingFlags.NonPublic | BindingFlags.Static)!;
    private static readonly ConcurrentDictionary<Type, Dispatch> Dispatchers = new();

    public override async Task Validate(IEntityValidatorContext<IEntity> context, CancellationToken token = default)
    {
        var scopes = services.GetRequiredService<FluentValidatorLookup>().ScopesWithValidators(context.Item.GetType());
        foreach (var scope in scopes)
        {
            var dispatch = Dispatchers.GetOrAdd(scope, static type => ValidateAsMethod.MakeGenericMethod(type).CreateDelegate<Dispatch>());
            await dispatch(services, context, token);
        }
    }

    // only an entity an AbstractValidator applies to: the pipeline runs this validator, and so scans the tracker for a
    // refusal's undo, only for those, and the startup wiring check reports only those
    bool ISelectiveEntityValidator.Covers(Type entityType)
        => services.GetRequiredService<FluentValidatorLookup>().ScopesWithValidators(entityType).Count > 0;

    /// <summary>Runs every <see cref="IValidator{T}"/> of one scope type, each with a <see cref="ValidationContext{T}"/> of its own.</summary>
    private static async Task ValidateAs<TScope>(IServiceProvider services, IEntityValidatorContext context, CancellationToken token)
    {
        var instance = (TScope)context.Item;
        foreach (var validator in services.GetServices<IValidator<TScope>>())
        {
            var validationContext = ValidationContext<TScope>.CreateWithOptions(instance, options =>
            {
                switch (context.Operation)
                {
                    case EntityWriteOperation.Add:
                        options.IncludeRuleSets(EntityRuleSets.Add).IncludeRulesNotInRuleSet();
                        break;
                    case EntityWriteOperation.Modify:
                        options.IncludeRuleSets(EntityRuleSets.Modify).IncludeRulesNotInRuleSet();
                        break;
                    case EntityWriteOperation.Remove:
                        options.IncludeRuleSets(EntityRuleSets.Remove);
                        break;
                }
            });
            // RootContextData is shared with child validators, so a ChildRules/SetValidator rule reads it too
            validationContext.RootContextData[ValidationContextExtensions.OriginalKey] = context.Original;
            validationContext.RootContextData[ValidationContextExtensions.OperationKey] = context.Operation;

            var result = await validator.ValidateAsync(validationContext, token);
            // warnings and info have no place in the 400 body, and must not block the save
            foreach (var failure in result.Errors.Where(f => f.Severity == Severity.Error))
            {
                context.AddError(failure.PropertyName ?? string.Empty, failure.ErrorMessage);
            }
        }
    }
}

/// <summary>
/// Which of an entity type's scope types have a FluentValidation validator registered — looked up once per entity type,
/// so an entity without one costs a cached lookup and nothing else. A singleton: registrations cannot change once the
/// container is built.
/// </summary>
internal sealed class FluentValidatorLookup(IServiceProviderIsService isService)
{
    private readonly ConcurrentDictionary<Type, Type[]> _scopes = new();

    public IReadOnlyList<Type> ScopesWithValidators(Type entityType)
        => _scopes.GetOrAdd(entityType, type => EntityScopeTypes.Of(type)
            .Where(scope => isService.IsService(typeof(IValidator<>).MakeGenericType(scope)))
            .ToArray());
}
