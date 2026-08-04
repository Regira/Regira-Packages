namespace Regira.Entities.Normalizing.Abstractions;

public interface IEntityNormalizer
{
    bool IsExclusive { get; }
    Task HandleNormalize(object item, CancellationToken token = default);
    Task HandleNormalizeMany(IEnumerable<object> items, CancellationToken token = default);
}
public interface IEntityNormalizer<in T> : IEntityNormalizer
{
    Task HandleNormalize(T item, CancellationToken token = default);
    Task HandleNormalizeMany(IEnumerable<T> items, CancellationToken token = default);
}
