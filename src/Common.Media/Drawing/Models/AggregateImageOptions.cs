using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Models;

public class AggregateImageOptions
{
    public IImageLayer? Target { get; set; }
    public ICollection<IImageLayer> ImageLayers { get; set; } = null!;
}