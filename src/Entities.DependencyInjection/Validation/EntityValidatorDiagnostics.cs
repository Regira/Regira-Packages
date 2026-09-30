using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Validators;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

internal static class EntityValidatorDiagnostics
{
    private sealed record Resolution(IEntityValidator[] Validators, EntityValidationIssue? Issue);

    // one resolution per startup run, shared by the scope and the wiring check: resolving builds every validator and
    // its dependencies (a DbContext among them)
    private static readonly ConditionalWeakTable<EntityValidationContext, Resolution> Resolutions = new();
    private static readonly ConcurrentDictionary<Type, Type[]> Families = new();

    /// <summary>
    /// One instance of every registered validator type. Resolving user-supplied services can throw, and a startup check
    /// must never break startup with its own diagnostics, so a failure becomes one informational issue.
    /// </summary>
    public static bool TryResolve(EntityValidationContext context, out IEntityValidator[] validators, out EntityValidationIssue? issue)
    {
        var resolution = Resolutions.GetValue(context, Resolve);
        validators = resolution.Validators;
        issue = resolution.Issue;
        return issue == null;
    }

    private static Resolution Resolve(EntityValidationContext context)
    {
        try
        {
            var validators = context.Provider.GetServices<IEntityValidator>().DistinctBy(v => v.GetType()).ToArray();
            return new Resolution(validators, null);
        }
        catch (Exception ex)
        {
            return new Resolution([], new EntityValidationIssue(EntityValidationSeverity.Info,
                $"Could not resolve the entity validators, so their scope and wiring were not checked: {ex.Message}"));
        }
    }

    /// <summary>
    /// Whether <paramref name="validatorType"/> can run for an item written through the service of
    /// <paramref name="entityType"/>. The runtime decides by the item's runtime type, so a validator scoped to a derived
    /// type (<c>Person</c>, saved through the <c>Party</c> service) runs too: every type in <paramref name="entityType"/>'s
    /// assembly that derives from it counts.
    /// </summary>
    public static bool MayApplyTo(Type validatorType, Type entityType)
        => FamilyOf(entityType).Any(type => EntityValidatorScope.AppliesTo(validatorType, type));

    /// <summary>
    /// <see cref="MayApplyTo"/>, narrowed by an <see cref="ISelectiveEntityValidator"/> to the types it has anything
    /// to check for.
    /// </summary>
    public static bool Checks(IEntityValidator validator, Type entityType)
        => FamilyOf(entityType).Any(type => EntityValidatorScope.RunsFor(validator, type));

    /// <summary><paramref name="entityType"/> and every type in its assembly deriving from it.</summary>
    private static Type[] FamilyOf(Type entityType)
        => Families.GetOrAdd(entityType, static type => new[] { type }
            .Concat(LoadableTypes(type.Assembly).Where(t => t != type && type.IsAssignableFrom(t)))
            .ToArray());

    private static IEnumerable<Type> LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.OfType<Type>();
        }
    }

    /// <summary>The type as written: <c>EntityValidator&lt;OrderLine&gt;</c> rather than <c>EntityValidator`1</c>.</summary>
    public static string Describe(Type type)
    {
        if (!type.IsGenericType)
        {
            return type.Name;
        }
        var name = type.Name;
        var arity = name.IndexOf('`');
        return (arity < 0 ? name : name[..arity]) + EntityServiceDiagnostics.FormatTypeArgs(type);
    }
}
