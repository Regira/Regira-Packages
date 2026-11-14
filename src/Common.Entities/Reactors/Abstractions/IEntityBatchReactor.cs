namespace Regira.Entities.Reactors.Abstractions;

/// <summary>
/// An <see cref="IEntityReactor"/> that reacts to the changes of a commit <b>together</b>: one call with every change
/// it covers and its <see cref="IEntityReactor.CanReact"/> selects, in the order they were saved — the place for one
/// query, one save, one message or one enqueued job where a reactor per row would make one for each.
/// <para>
/// The changes are those of one commit: a save that commits on its own, or every save of a transaction — a row saved
/// twice in it appears twice. A batch reactor that throws is logged and skipped as a whole. It is registered as any
/// reactor is: <c>AddReactor</c>, or the builder's <c>ReactMany(...)</c>.
/// </para>
/// </summary>
public interface IEntityBatchReactor : IEntityReactor
{
    /// <summary>Reacts to the committed changes this reactor selected: never empty, in the order they were saved.</summary>
    Task ReactMany(IReadOnlyList<IEntityChange> changes, CancellationToken token = default);
}

/// <inheritdoc cref="IEntityBatchReactor"/>
/// <typeparam name="TEntity">
/// The entity type to react to, or a type it derives from or implements — a batch reactor on <c>IHasTimestamps</c>
/// receives the changes of every entity implementing it in one list.
/// </typeparam>
public interface IEntityBatchReactor<in TEntity> : IEntityReactor<TEntity>, IEntityBatchReactor
{
    /// <inheritdoc cref="IEntityBatchReactor.ReactMany"/>
    Task ReactMany(IReadOnlyList<IEntityChange<TEntity>> changes, CancellationToken token = default);
}
