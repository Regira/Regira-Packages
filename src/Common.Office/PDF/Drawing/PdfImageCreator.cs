using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Drawing;

public class PdfImageCreator(IPdfToImageService service, IPdfSplitter splitter) : ImageCreatorBase<PdfToImageLayerOptions>
{
    public override async Task<IImageFile?> Create(PdfToImageLayerOptions input, CancellationToken cancellationToken = default)
    {
        var page = input.Page ?? 1;
        var pageCount = await splitter.GetPageCount(input.File, cancellationToken);
        var singlePagePdf = pageCount > 1
            ? (await splitter.Split(input.File, [new PdfSplitRange { Start = page, End = page }], cancellationToken)).Single()
            : input.File;
        return (await service.ToImages(singlePagePdf, input.ToPdfToImageOptions(), cancellationToken)).SingleOrDefault();
    }
}
