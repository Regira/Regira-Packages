using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.Word.Abstractions;

namespace Regira.Office.Word.Drawing;

public class WordImageCreator(IWordToImagesService service) : ImageCreatorBase<WordToImageLayerOptions>
{
    public override async Task<IImageFile?> Create(WordToImageLayerOptions input, CancellationToken cancellationToken = default)
    {
        var images = await service.ToImages(input.ToWordTemplateInput());
        return images
            .Skip((input.Page ?? 1) - 1)
            .FirstOrDefault();
    }
}
