using Regira.Entities.Normalizing.Abstractions;
using Regira.Normalizing.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Persons;

public class PersonNormalizer(INormalizer? normalizer) : EntityNormalizerBase<Person>(normalizer)
{
    public override Task HandleNormalizeMany(IEnumerable<Person> items, CancellationToken token = default)
    {
        foreach (var item in items)
        {
            item.NormalizedContent = $"PERSON {item.NormalizedContent}";
        }

        return Task.CompletedTask;
    }
}