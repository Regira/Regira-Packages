namespace Regira.Entities.Mapping.Abstractions;

public interface IEntityAfterMapper
{
    bool CanMap(object source);
    void AfterMap(object source, object target);
}
public interface IEntityAfterMapper<in TSource, in TTarget> : IEntityAfterMapper
{
    void AfterMap(TSource source, TTarget target);
}
