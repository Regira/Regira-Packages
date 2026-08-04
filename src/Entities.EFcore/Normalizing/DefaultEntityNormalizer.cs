using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;

namespace Regira.Entities.EFcore.Normalizing;

public class DefaultEntityNormalizer(IObjectNormalizer? objectNormalizer = null)
    : DefaultEntityNormalizer<IEntity>(objectNormalizer);

public class DefaultEntityNormalizer<T>(IObjectNormalizer? objectNormalizer = null)
    : EntityNormalizerBase<T>(objectNormalizer?.DefaultNormalizer)
    where T : class
{
    protected override INormalizer DefaultPropertyNormalizer => objectNormalizer?.DefaultNormalizer ?? base.DefaultPropertyNormalizer;
    protected internal readonly IObjectNormalizer DefaultObjectNormalizer = objectNormalizer
                                                                            ?? NormalizingDefaults.DefaultObjectNormalizer
                                                                            ?? new ObjectNormalizer();

    public DefaultEntityNormalizer(INormalizer? normalizer)
        : this(new ObjectNormalizer(new NormalizingOptions { DefaultNormalizer = normalizer }))
    { }

    public override Task HandleNormalize(T item, CancellationToken token = default)
    {
        DefaultObjectNormalizer.HandleNormalize(item);
        return Task.CompletedTask;
    }
}