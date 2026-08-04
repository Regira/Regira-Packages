using Regira.Normalizing;
using Regira.Normalizing.Abstractions;
using Regira.Normalizing.Models;
using Regira.Utilities;
using Testing.Library.Contoso;

namespace Normalizing.Testing.Services;

public class PeopleNormalizer(INormalizer? normalizer) : IObjectNormalizer
{
    public bool IsExclusive => false;
    public INormalizer DefaultNormalizer { get; } = normalizer
                                                    ?? new DefaultNormalizer(new NormalizeOptions
                                                    {
                                                        RemoveDiacritics = true,
                                                        Transform = TextTransform.ToUpperCase
                                                    });

    public PeopleNormalizer() : this(null) { }

    public void HandleNormalize(object? instance, bool recursive = true)
    {
        if (instance is Person item)
        {
            item.NormalizedLastName = DefaultNormalizer.Normalize(item.LastName);
            item.NormalizedGivenName = DefaultNormalizer.Normalize(item.GivenName);
            item.Email = item.Email?.ToLowerInvariant();
            item.NormalizedPhone = !string.IsNullOrWhiteSpace(item.Phone)
                ? RegexUtility.ExtractPhoneNumbers(item.Phone).FirstOrDefault()
                : null;

            HandleNormalize(item.Supervisor);
        }
    }

    public Task HandleNormalizeMany(IEnumerable<object?> instances, bool recursive = true)
    {
        foreach (var item in instances)
        {
            HandleNormalize(item, recursive);
        }

        return Task.CompletedTask;
    }
}