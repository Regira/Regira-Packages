using Regira.Entities.DependencyInjection.ServiceCollections;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Warns for entities that kept the default <c>EntityRepository</c> although <c>UseRepository()</c> replaced it: the
/// app's repository types are matched to a <c>For&lt;&gt;()</c> registration by their number of type parameters, and
/// none has the number these registrations need.
/// </summary>
internal sealed class RepositoryShapeValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        if (context.Services.FirstOrDefault(d => d.ServiceType == typeof(EntityRepositoryRegistry))?.ImplementationInstance is not EntityRepositoryRegistry registry)
        {
            yield break;
        }

        foreach (var shape in registry.EntitiesOnDefault.Distinct().GroupBy(x => x.Arity).OrderBy(g => g.Key))
        {
            var entities = shape.Select(x => x.EntityType.Name).Distinct().ToArray();
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"The default EntityRepository still serves: {string.Join(", ", entities)}. UseRepository() has no repository type with " +
                $"{shape.Key} type parameter{(shape.Key == 1 ? "" : "s")}, which their For<>() registration needs. " +
                $"Add one deriving from the EntityRepository with {shape.Key} type parameter{(shape.Key == 1 ? "" : "s")}, " +
                "or give an entity that should keep the default e.HasRepository<EntityRepository<...>>() to make it explicit.");
        }
    }
}
