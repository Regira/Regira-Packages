using Regira.Entities.Processing.Abstractions;

namespace Regira.Entities.EFcore.Processing;

public class EntityProcessor<TEntity, TIncludes>(Func<IList<TEntity>, TIncludes?, Task>? process = null)
    : IEntityProcessor<TEntity, TIncludes>
        where TIncludes : struct, Enum
{
    //Task IEntityProcessor.Process<T, TIncludes>(IList<T> items, TIncludes? includes)
    //    => Process(items.OfType<TEntity>().ToList(), includes);

    public virtual Task Process(IList<TEntity> items, TIncludes? includes, CancellationToken token = default)
        // Note: inline process delegates do not receive the token; use a class-based IEntityProcessor for cancellation support
        => process?.Invoke(items, includes) ?? Task.CompletedTask;
}
