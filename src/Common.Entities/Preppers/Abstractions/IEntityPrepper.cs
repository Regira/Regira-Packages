namespace Regira.Entities.Preppers.Abstractions;

public interface IEntityPrepper
{
    Task Prepare(object modified, object? original, CancellationToken token = default);
}
public interface IEntityPrepper<in TEntity> : IEntityPrepper
{
    Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default);
}
