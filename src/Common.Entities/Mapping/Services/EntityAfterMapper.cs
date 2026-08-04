namespace Regira.Entities.Mapping.Abstractions;

public class EntityAfterMapper<TSource, TTarget>(Action<TSource, TTarget> afterMapAction) : EntityAfterMapperBase<TSource, TTarget>
{
    public override void AfterMap(TSource source, TTarget target)
        => afterMapAction(source, target);
}