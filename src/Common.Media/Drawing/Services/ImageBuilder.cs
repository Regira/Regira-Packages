using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Media.Drawing.Utilities;

namespace Regira.Media.Drawing.Services;

public class ImageBuilder(IImageService service, IEnumerable<IImageCreator> imageCreators)
{
    private readonly IImageCreator[] _imageCreators = CollectCreators(service, imageCreators);
    private static IImageCreator[] CollectCreators(IImageService service, IEnumerable<IImageCreator> imageCreators)
    {
        var creators = imageCreators.ToArray();
        return [.. creators, AggregateImageCreator.Create(service, creators)];
    }
    private readonly List<IImageLayer> _items = [];
    private IImageLayer? _target;
    
    /// <summary>
    /// Use an existing image as target to draw on
    /// </summary>
    /// <param name="target"></param>
    /// <returns></returns>
    /// 
    public ImageBuilder SetBaseLayer(IImageFile target)
    {
        _target = new ImageLayer { Source = target };
        return this;
    }
    /// <summary>
    /// Sets the base layer for the image builder using the specified <see cref="IImageLayer"/>.
    /// </summary>
    /// <param name="target">
    /// The <see cref="IImageLayer"/> to be used as the base layer for drawing operations.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="ImageBuilder"/> to allow method chaining.
    /// </returns>
    public ImageBuilder SetBaseLayer(IImageLayer target)
    {
        _target = target;
        return this;
    }
    /// <summary>
    /// Sets the base layer for the image builder using the specified <see cref="CanvasImageOptions"/>.
    /// </summary>
    /// <param name="target">
    /// The <see cref="CanvasImageOptions"/> instance that defines the size, background color, and image format
    /// to be used as the base layer for drawing operations.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="ImageBuilder"/> to allow method chaining.
    /// </returns>
    public ImageBuilder SetBaseLayer(CanvasImageOptions target)
    {
        _target = new ImageLayer<CanvasImageOptions> { Source = target };
        return this;
    }

    /// <summary>
    /// Adds the specified image layers to the image builder.
    /// </summary>
    /// <param name="items">
    /// An array of <see cref="IImageLayer"/> objects to be added to the image.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="ImageBuilder"/> to allow method chaining.
    /// </returns>
    public ImageBuilder Add(params IImageLayer[] items)
    {
        _items.AddRange(items);
        return this;
    }
    /// <summary>
    /// Adds the specified collection of image layers to the image builder.
    /// </summary>
    /// <param name="items">
    /// A collection of <see cref="IImageLayer"/> objects to be added to the image.
    /// </param>
    /// <returns>
    /// The current instance of <see cref="ImageBuilder"/> to allow method chaining.
    /// </returns>
    public ImageBuilder Add(IEnumerable<IImageLayer> items)
    {
        _items.AddRange(items);
        return this;
    }

    /// <summary>
    /// Builds and returns the resulting <see cref="IImageFile"/> by combining the base layer and additional image layers.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// The constructed <see cref="IImageFile"/> containing the combined image layers.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown when the base layer cannot be converted into an <see cref="IImageFile"/>.
    /// </exception>
    public async Task<IImageFile> Build(CancellationToken cancellationToken = default)
    {
        var target = _target != null
            ? await _imageCreators.GetImageFile(_target, cancellationToken) ?? throw new Exception($"Could not create IImageFile from {_target.GetType()}")
            : null;

        IImageFile? result = target;
        foreach (var item in _items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageLayer = await ToImageLayer(item, cancellationToken);
            result = await service.Draw([imageLayer], result, cancellationToken);
        }

        return result!;
    }

    /// <summary>
    /// Ensures that the source of the layer is converted into an <see cref="IImageFile"/> using the available <see cref="IImageCreator"/> implementations.
    /// </summary>
    /// <param name="item">
    /// The <see cref="IImageLayer"/> instance to be converted.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the operation.</param>
    /// <returns>
    /// An <see cref="ImageLayer"/> instance containing the source and options from the provided <paramref name="item"/>.
    /// </returns>
    /// <exception cref="Exception">
    /// Thrown when the source object of the provided <paramref name="item"/> cannot be converted into an <see cref="IImageFile"/>.
    /// </exception>
    protected async Task<ImageLayer> ToImageLayer(IImageLayer item, CancellationToken cancellationToken = default)
    {
        if (item is ImageLayer imageLayer)
        {
            return imageLayer;
        }

        var source = await _imageCreators.GetImageFile(item, cancellationToken)
                     ?? throw new Exception($"Could not create IImageFile from {item.Source.GetType()}");

        return new ImageLayer
        {
            Source = source,
            Options = item.Options
        };
    }
}
