using Regira.Utilities;

namespace Regira.Entities.Mapping.Abstractions;

public abstract class EntityAfterMapperBase<TSource, TTarget> : IEntityAfterMapper<TSource, TTarget>
{
    public abstract void AfterMap(TSource source, TTarget target);
    public bool CanMap(object source)
        => TypeUtility.ImplementsType<TSource>(source.GetType());
    void IEntityAfterMapper.AfterMap(object source, object target)
        => AfterMap((TSource)source, (TTarget)target);
}
