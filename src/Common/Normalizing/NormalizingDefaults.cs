using Regira.Normalizing.Abstractions;

namespace Regira.Normalizing;

public static class NormalizingDefaults
{
    public static IObjectNormalizer? DefaultObjectNormalizer { get; set; }
    public static INormalizer? DefaultPropertyNormalizer { get; set; }
}