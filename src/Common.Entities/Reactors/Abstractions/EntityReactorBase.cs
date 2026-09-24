namespace Regira.Entities.Reactors.Abstractions;

/// <summary>
/// Base class for an <see cref="IEntityReactor{TEntity}"/>: override <see cref="React"/>, and <see cref="CanReact"/>
/// to react to some changes only (by default every change of <typeparamref name="TEntity"/>).
/// </summary>
public abstract class EntityReactorBase<TEntity> : IEntityReactor<TEntity>
    where TEntity : class
{
    public virtual bool CanReact(IEntityChange<TEntity> change) => true;
    public abstract Task React(IEntityChange<TEntity> change, CancellationToken token = default);

    bool IEntityReactor.CanReact(IEntityChange change) => change is IEntityChange<TEntity> typed && CanReact(typed);
    Task IEntityReactor.React(IEntityChange change, CancellationToken token) => React((IEntityChange<TEntity>)change, token);
}
