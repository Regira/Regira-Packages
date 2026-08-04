using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;

namespace Regira.Entities.Normalizing.Abstractions;

public class EntityNormalizingOptions : NormalizingOptions
{
    public bool IsExclusive { get; set; }
}
public abstract class EntityNormalizerBase<T>(INormalizer? normalizer = null) : IEntityNormalizer<T>
    where T : class
{
    public virtual bool IsExclusive => false;

    protected virtual INormalizer DefaultPropertyNormalizer => normalizer ?? NormalizingDefaults.DefaultPropertyNormalizer ?? new DefaultNormalizer();

    public virtual Task HandleNormalize(T item, CancellationToken token = default)
        => Task.CompletedTask;

    public virtual async Task HandleNormalizeMany(IEnumerable<T> items, CancellationToken token = default)
    {
        foreach (var item in items)
        {
            await HandleNormalize(item, token);
        }
    }

    Task IEntityNormalizer.HandleNormalize(object item, CancellationToken token)
        => HandleNormalize((T)item, token);
    Task IEntityNormalizer.HandleNormalizeMany(IEnumerable<object> items, CancellationToken token)
        => HandleNormalizeMany(items.Cast<T>(), token);
}
