using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using Testing.Library.Contoso;

namespace Normalizing.Testing.Services;

public class DummyPersonNormalizer(INormalizer? defaultNormalizer = null) : IObjectNormalizer
{
    public bool IsExclusive => false;
    public INormalizer DefaultNormalizer { get; } = defaultNormalizer ?? NormalizingDefaults.DefaultPropertyNormalizer ?? new DefaultNormalizer();


    public void HandleNormalize(Person item)
    {
        item.NormalizedGivenName = item.GivenName;
        item.NormalizedLastName = item.LastName;
    }
    public void HandleNormalize(object? instance, bool recursive = true)
    {
        if (instance is Person item)
        {
            HandleNormalize(item);
        }
    }

    public Task HandleNormalizeMany(IEnumerable<object?> instances, bool recursive = true)
    {
        foreach (var item in instances)
        {
            if (item is Person person)
            {
                HandleNormalize(person, recursive);
            }
        }

        return Task.CompletedTask;
    }
}