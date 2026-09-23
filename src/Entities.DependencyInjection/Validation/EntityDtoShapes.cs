using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// The DTO shapes the write path binds, per entity, as the DTO checks judge them: the entity's last
/// <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration when it has one (UseMapping appends a registration per call
/// and DI resolves the last), otherwise every distinct shape an <see cref="IEntityDtoShapeSource"/> declares — the
/// entity controllers' generic arguments. An entity with neither is its own input DTO: an empty list.
/// </summary>
internal sealed class EntityDtoShapes
{
    private readonly EntityMappingRegistration[] _mappings;
    private readonly List<EntityMappingRegistration> _declared = [];

    /// <summary>
    /// Why a source could not enumerate, or null. The checks then run on the UseMapping registrations alone, and a
    /// validator reports that as Info rather than going quiet about the entities it can no longer see.
    /// </summary>
    public string? Failure { get; }

    public EntityDtoShapes(EntityValidationContext context)
    {
        _mappings = context.Services
            .Where(d => d.ServiceType == typeof(EntityMappingRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityMappingRegistration>()
            .ToArray();

        var failures = new List<string>();
        foreach (var source in context.Provider.GetServices<IEntityDtoShapeSource>())
        {
            try
            {
                _declared.AddRange(source.GetDtoShapes().ToArray());
            }
            catch (Exception ex)
            {
                failures.Add($"{source.GetType().Name}: {ex.Message}");
            }
        }
        Failure = failures.Count > 0 ? string.Join("; ", failures) : null;
    }

    public IReadOnlyList<EntityMappingRegistration> For(Type entityType)
    {
        var mapping = _mappings.LastOrDefault(m => m.EntityType == entityType);
        return mapping != null
            ? [mapping]
            : _declared.Where(s => s.EntityType == entityType).Distinct().ToArray();
    }

    /// <summary>The Info line a DTO check emits when <see cref="Failure"/> is set.</summary>
    public EntityValidationIssue? FailureIssue(string check)
        => Failure == null
            ? null
            : new EntityValidationIssue(EntityValidationSeverity.Info,
                $"Could not read the entity controllers' DTOs for the {check} check ({Failure}); it judged only UseMapping registrations.");
}
