namespace Regira.Media.Drawing.Models.Abstractions;

public interface IImageLayer
{
    public object Source { get; set; }
    public ImageLayerOptions? Options { get; set; }
}
public interface IImageLayer<T> : IImageLayer
{
    public new T Source { get; set; }
}