namespace Regira.Entities.DependencyInjection.Mapping;

/// <summary>
/// Introspectable record of a <c>UseMapping&lt;TDto, TInputDto&gt;()</c> call, registered as an instance
/// singleton so endpoint scanners (e.g. the FastEndpoints auto-endpoints) can resolve the wire shape
/// configured for an entity.
/// </summary>
public sealed record EntityMappingRegistration(Type EntityType, Type DtoType, Type InputDtoType);
