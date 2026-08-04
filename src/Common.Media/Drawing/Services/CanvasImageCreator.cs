using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;

namespace Regira.Media.Drawing.Services;

public class CanvasImageCreator(IImageService service) : ImageCreatorBase<CanvasImageOptions>
{
    public override async Task<IImageFile?> Create(CanvasImageOptions input, CancellationToken cancellationToken = default)
        => await service.Create(input.Size, input.BackgroundColor, input.ImageFormat, cancellationToken);
}
