namespace Regira.Entities.Preppers.Abstractions;

public abstract class EntityPrepperBase<TEntity> : IEntityPrepper<TEntity>
    where TEntity : class
{
    public abstract Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default);
    Task IEntityPrepper.Prepare(object modified, object? original, CancellationToken token)
        => Prepare((TEntity)modified, original as TEntity, token);
}
