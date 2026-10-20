using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// The DTO shapes the write path binds, per entity, as the DTO checks judge them: the entity's last
/// <c>UseMapping&lt;TDto, TInputDto&gt;()</c> registration when it has one (UseMapping appends a registration per call
/// and DI resolves the last), otherwise every distinct shape an <see cref="IEntityDtoShapeSource"/> declares — the
/// entity controllers' generic arguments. An entity with neither is its own input DTO: an empty list. A source that
/// throws fails the check reading the shapes, which the validation run reports.
/// </summary>
internal sealed class EntityDtoShapes
{
    private readonly EntityMappingRegistration[] _mappings;
    private readonly EntityMappingRegistration[] _declared;

    public EntityDtoShapes(EntityValidationContext context)
    {
        _mappings = context.Services
            .Where(d => d.ServiceType == typeof(EntityMappingRegistration))
            .Select(d => d.ImplementationInstance)
            .OfType<EntityMappingRegistration>()
            .ToArray();
        _declared = context.Provider.GetServices<IEntityDtoShapeSource>()
            .SelectMany(source => source.GetDtoShapes())
            .ToArray();
    }

    public IReadOnlyList<EntityMappingRegistration> For(Type entityType)
    {
        var mapping = _mappings.LastOrDefault(m => m.EntityType == entityType);
        return mapping != null
            ? [mapping]
            : _declared.Where(s => s.EntityType == entityType).Distinct().ToArray();
    }
}
