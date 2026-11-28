using Regira.Entities.Mapping.Abstractions;

namespace Entities.DependencyInjection.Testing.Infrastructure;

/// <summary>A mapping engine that configures nothing: lets a test call <c>UseMapping()</c> without a mapper package.</summary>
public class NullMapConfigurator : IEntityMapConfigurator
{
    public void Configure<TSource, TTarget>() { }
    public void Configure(Type sourceType, Type targetType) { }
}

public record CourseDto;
public record CourseInputDto;
