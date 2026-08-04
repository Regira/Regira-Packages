using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Models;

public record ImageLayer<T> : IImageLayer<T>
    where T : class
{
    public T Source { get; set; } = null!;
    object IImageLayer.Source
    {
        get => Source;
        set => Source = (T)value;
    }

    public ImageLayerOptions? Options { get; set; } = new();
}

public record ImageLayer : ImageLayer<IImageFile>;