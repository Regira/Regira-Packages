using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;

namespace Regira.Media.Drawing.Services;

public class LabelImageCreator(IImageService service) : ImageCreatorBase<LabelImageOptions>
{
    public override async Task<IImageFile?> Create(LabelImageOptions input, CancellationToken cancellationToken = default)
        => await service.CreateTextImage(input, cancellationToken);
}
