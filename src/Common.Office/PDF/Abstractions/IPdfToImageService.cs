using Regira.IO.Abstractions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfToImageService
{
    Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default);
}
public interface IPdfToImageAsyncService
{
    IAsyncEnumerable<IImageFile> ToImagesAsync(IMemoryFile pdf, PdfToImagesOptions? options = null);
}
