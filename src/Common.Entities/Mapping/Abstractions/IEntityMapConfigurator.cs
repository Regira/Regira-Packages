namespace Regira.Entities.Mapping.Abstractions;

public interface IEntityMapConfigurator
{
    void Configure<TSource, TTarget>();
    void Configure(Type sourceType, Type targetType);
}