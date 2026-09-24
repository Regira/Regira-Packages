namespace Regira.Entities.Reactors.Abstractions;

/// <summary>
/// Reacts to a row after the save that changed it has been <b>committed</b> — the place for side effects that must
/// not happen for a write that fails or rolls back: sending mail, calling another system, enqueueing a background
/// job, starting a follow-up workflow.
/// <para>
/// Reactors run in registration order, once per changed row, in a DI scope of their own with a fresh
/// <c>DbContext</c>: a reactor that writes saves its own unit of work, and that save can trigger reactors in turn.
/// A reactor that throws is logged and skipped — the data is committed, so the save still succeeds and the other
/// reactors still run.
/// </para>
/// </summary>
public interface IEntityReactor
{
    /// <summary>Whether to react to <paramref name="change"/> — evaluated for every change of a matching entity type.</summary>
    bool CanReact(IEntityChange change);
    /// <summary>Reacts to a committed change.</summary>
    Task React(IEntityChange change, CancellationToken token = default);
}

/// <inheritdoc cref="IEntityReactor"/>
/// <typeparam name="TEntity">
/// The entity type to react to, or a type it derives from or implements — a reactor on <c>IHasTimestamps</c>
/// reacts to every entity implementing it.
/// </typeparam>
public interface IEntityReactor<in TEntity> : IEntityReactor
{
    /// <inheritdoc cref="IEntityReactor.CanReact"/>
    bool CanReact(IEntityChange<TEntity> change);
    /// <inheritdoc cref="IEntityReactor.React"/>
    Task React(IEntityChange<TEntity> change, CancellationToken token = default);
}
