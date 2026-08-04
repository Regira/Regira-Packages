namespace Regira.Normalizing.Abstractions;

public interface IObjectNormalizer
{
    bool IsExclusive { get; }
    INormalizer DefaultNormalizer { get; }
    Task HandleNormalizeMany(IEnumerable<object?> instances, bool recursive = false);
    void HandleNormalize(object? item, bool recursive = false);
}

//public interface IObjectNormalizer<in T> : IObjectNormalizer
//{
//    Task HandleNormalizeMany(IEnumerable<T?> instances, bool recursive = false);
//    void HandleNormalize(T? item, bool recursive = false);
//}