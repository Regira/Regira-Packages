namespace Regira.Entities.Reactors.Abstractions;

/// <summary>
/// Base class for an <see cref="IEntityBatchReactor{TEntity}"/>: override <see cref="ReactMany"/>, and
/// <see cref="EntityReactorBase{TEntity}.CanReact"/> to receive some changes only (by default every change of
/// <typeparamref name="TEntity"/>).
/// </summary>
public abstract class EntityBatchReactorBase<TEntity> : EntityReactorBase<TEntity>, IEntityBatchReactor<TEntity>
    where TEntity : class
{
    public abstract Task ReactMany(IReadOnlyList<IEntityChange<TEntity>> changes, CancellationToken token = default);

    /// <summary>
    /// Reacts to one change as a batch of one. Sealed: a batch reactor is called through <see cref="ReactMany"/>, so
    /// an override here would never run.
    /// </summary>
    public sealed override Task React(IEntityChange<TEntity> change, CancellationToken token = default) => ReactMany([change], token);

    Task IEntityBatchReactor.ReactMany(IReadOnlyList<IEntityChange> changes, CancellationToken token)
        => ReactMany(changes.Cast<IEntityChange<TEntity>>().ToArray(), token);
}
