using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.Reactors;

/// <summary>An <see cref="IEntityBatchReactor{TEntity}"/> built from delegates — what the builder's <c>ReactMany(...)</c> registers.</summary>
/// <param name="reactMany">Reacts to the committed changes together.</param>
/// <param name="canReact">Selects the changes to react to; <c>null</c> reacts to every change.</param>
public class EntityBatchReactor<TEntity>(Func<IReadOnlyList<IEntityChange<TEntity>>, CancellationToken, Task> reactMany, Func<IEntityChange<TEntity>, bool>? canReact = null)
    : EntityBatchReactorBase<TEntity>
    where TEntity : class
{
    public override bool CanReact(IEntityChange<TEntity> change) => canReact?.Invoke(change) ?? true;
    public override Task ReactMany(IReadOnlyList<IEntityChange<TEntity>> changes, CancellationToken token = default) => reactMany(changes, token);
}
