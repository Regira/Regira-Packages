using System.Collections;

namespace Regira.Entities.Mapping.Abstractions;

public interface IEntityMapper
{
    TTarget Map<TTarget>(object source);
    TTarget Map<TSource, TTarget>(TSource source, TTarget target);
}

public abstract class EntityMapperBase(IEnumerable<IEntityAfterMapper>? afterMappers = null) : IEntityMapper
{
    public virtual TTarget Map<TTarget>(object source)
    {
        var target = MapEntity<TTarget>(source);
        ApplyAfterMappings(source, target);

        return target;
    }
    public virtual TTarget Map<TSource, TTarget>(TSource source, TTarget target)
    {
        MapEntity(source, target);
        ApplyAfterMappings(source, target);

        return target;
    }

    public virtual void ApplyAfterMappings<TSource, TTarget>(TSource source, TTarget target)
    {
        if (source == null || target == null)
        {
            return;
        }

        var sourceCollection = (source as IEnumerable)?.Cast<object>().ToArray() ?? [source];
        var targetCollection = (target as IEnumerable)?.Cast<object>().ToArray() ?? [target];

        var firstSourceItem = sourceCollection.FirstOrDefault();
        if (firstSourceItem == null)
        {
            return;
        }

        var targetAfterMappers = afterMappers?
            .Where(m => m.CanMap(firstSourceItem))
            .ToArray() ?? [];

        for (var i = 0; i < sourceCollection.Length; i++)
        {
            var sourceItem = sourceCollection[i];
            var targetItem = targetCollection[i];
            foreach (var afterMapper in targetAfterMappers)
            {
                afterMapper.AfterMap(sourceItem, targetItem);
            }
        }
    }

    public abstract TTarget MapEntity<TTarget>(object source);
    public abstract void MapEntity<TSource, TTarget>(TSource source, TTarget target);
}