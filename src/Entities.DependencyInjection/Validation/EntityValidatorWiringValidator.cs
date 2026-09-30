using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Validators.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Reports entities whose write path cannot run the validators in their scope: the service that writes them has no
/// constructor taking <c>IEnumerable&lt;IEntityValidator&gt;</c> and hands the write to nothing that has. That is a write
/// service built on <c>EntityWriteService</c>'s constructor without validators, or a custom <c>IEntityRepository</c> over
/// another store. Services registered through a factory are skipped, since their implementation type cannot be seen.
/// </summary>
internal sealed class EntityValidatorWiringValidator : IEntityRegistrationValidator
{
    private enum Reach { Runs, Blocked, Unknown }

    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        var registrations = context.Registrations.Entities
            .GroupBy(e => e.EntityType)
            .Select(g => g.First())
            .ToArray();
        if (registrations.Length == 0)
        {
            yield break;
        }

        if (!EntityValidatorDiagnostics.TryResolve(context, out var validators, out var issue))
        {
            yield return issue!;
            yield break;
        }

        foreach (var registration in registrations)
        {
            var inScope = validators
                .Where(v => EntityValidatorDiagnostics.Checks(v, registration.EntityType))
                .Select(v => v.GetType())
                .ToArray();
            if (inScope.Length == 0)
            {
                continue;
            }

            var serviceType = typeof(IEntityService<,>).MakeGenericType(registration.EntityType, registration.KeyType);
            var (reach, blockedBy) = Check(context.Services, serviceType, registration.EntityType, []);
            if (reach != Reach.Blocked)
            {
                continue;
            }

            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"The entity validators in scope of {registration.EntityType.Name} " +
                $"({string.Join(", ", inScope.Select(EntityValidatorDiagnostics.Describe))}) never run: its write path goes through " +
                $"'{EntityValidatorDiagnostics.Describe(blockedBy!)}', which has no constructor taking IEnumerable<IEntityValidator>. " +
                "ACTION: for a write service derived from EntityWriteService, add an IEnumerable<IEntityValidator> parameter and pass " +
                "it to the base constructor; for a service writing to another store, import IEnumerable<IEntityValidator> and call " +
                "validators.ValidateItem(item, original, operation) before each write. See entities.instructions → Validators.");
        }
    }

    /// <summary>
    /// Follows the registered implementation of <paramref name="serviceType"/> through the entity services its constructors
    /// take, until one takes the validators (<see cref="Reach.Runs"/>) or one writes without handing the write on
    /// (<see cref="Reach.Blocked"/>). A factory registration, a missing one or a cycle ends the walk as <see cref="Reach.Unknown"/>.
    /// </summary>
    private static (Reach Reach, Type? BlockedBy) Check(IServiceCollection services, Type serviceType, Type entityType, HashSet<Type> visited)
    {
        var implementation = services.LastOrDefault(d => d.ServiceType == serviceType)?.ImplementationType;
        if (implementation == null || !visited.Add(implementation))
        {
            return (Reach.Unknown, null);
        }

        var parameters = implementation.GetConstructors()
            .SelectMany(c => c.GetParameters())
            .Select(p => p.ParameterType)
            .Distinct()
            .ToArray();
        if (parameters.Contains(typeof(IEnumerable<IEntityValidator>)))
        {
            return (Reach.Runs, null);
        }

        var results = parameters
            .Where(p => WritesEntity(p, entityType))
            .Select(p => Check(services, p, entityType, visited))
            .ToArray();
        if (results.Length == 0)
        {
            return (Reach.Blocked, implementation);
        }
        if (results.Any(r => r.Reach == Reach.Runs))
        {
            return (Reach.Runs, null);
        }
        return results.Any(r => r.Reach == Reach.Unknown) ? (Reach.Unknown, null) : results.First();
    }

    /// <summary>Whether <paramref name="type"/> is a write service of <paramref name="entityType"/> — every entity service shape is.</summary>
    private static bool WritesEntity(Type type, Type entityType)
        => new[] { type }.Concat(type.GetInterfaces()).Any(i =>
            i.IsGenericType
            && i.GetGenericTypeDefinition() == typeof(IEntityWriteService<,>)
            && i.GetGenericArguments()[0] == entityType);
}
