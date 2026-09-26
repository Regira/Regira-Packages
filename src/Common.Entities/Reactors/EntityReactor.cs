using Regira.Entities.Reactors.Abstractions;

namespace Regira.Entities.Reactors;

/// <summary>An <see cref="IEntityReactor{TEntity}"/> built from delegates — what the builder's <c>React(...)</c> registers.</summary>
/// <param name="react">Reacts to a committed change.</param>
/// <param name="canReact">Selects the changes to react to; <c>null</c> reacts to every change.</param>
public class EntityReactor<TEntity>(Func<IEntityChange<TEntity>, CancellationToken, Task> react, Func<IEntityChange<TEntity>, bool>? canReact = null)
    : EntityReactorBase<TEntity>
    where TEntity : class
{
    public override bool CanReact(IEntityChange<TEntity> change) => canReact?.Invoke(change) ?? true;
    public override Task React(IEntityChange<TEntity> change, CancellationToken token = default) => react(change, token);
}
