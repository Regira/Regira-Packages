using Regira.Normalizing.Abstractions;

namespace Regira.Normalizing.Models;

public class NormalizingOptions : NormalizeOptions
{
    public INormalizer? DefaultNormalizer { get; set; }
    public IObjectNormalizer? DefaultObjectNormalizer { get; set; }
    public bool DefaultRecursive { get; set; } = true;

    /// <summary>
    /// Required when <see cref="Activator"/>.CreateInstance does not work
    /// </summary>
    public IServiceProvider? ServiceProvider { get; set; }
}