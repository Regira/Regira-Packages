using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;

namespace Regira.Media.Drawing.Services;

public class AggregateImageCreator : ImageCreatorBase<AggregateImageOptions>
{
    private readonly IImageService _service;
    private readonly IEnumerable<IImageCreator> _imageCreators;
    // private constructor to prevent circular dependencies
    private AggregateImageCreator(IImageService service, IEnumerable<IImageCreator> imageCreators)
    {
        _service = service;
        _imageCreators = imageCreators;
    }

    public static AggregateImageCreator Create(IImageService service, IEnumerable<IImageCreator> imageCreators)
        => new(service, imageCreators);

    public override async Task<IImageFile?> Create(AggregateImageOptions input, CancellationToken cancellationToken = default)
    {
        var builder = new ImageBuilder(_service, _imageCreators);

        if (input.Target != null)
        {
            builder.SetBaseLayer(input.Target);
        }

        builder.Add(input.ImageLayers.ToArray());
        return await builder.Build(cancellationToken);
    }
}
