using MapsterMapper;
using Regira.Entities.Mapping.Abstractions;

namespace Regira.Entities.Mapping.Mapster;

public class EntityMapper(IMapper mapper, IEnumerable<IEntityAfterMapper>? afterMappers = null) : EntityMapperBase(afterMappers)
{
    public override TTarget MapEntity<TTarget>(object source)
        => mapper.Map<TTarget>(source);

    public override void MapEntity<TSource, TTarget>(TSource source, TTarget target)
        => mapper.Map(source, target);
}