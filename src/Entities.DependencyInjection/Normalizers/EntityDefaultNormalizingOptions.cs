using Regira.Normalizing.Models;

namespace Regira.Entities.DependencyInjection.Normalizers;

public class EntityDefaultNormalizingOptions
{
    protected internal Action<NormalizeOptions>? ConfigureNormalizingFunc { get; set; }
    public void ConfigureNormalizing(Action<NormalizeOptions> configure)
        => ConfigureNormalizingFunc = configure;
}