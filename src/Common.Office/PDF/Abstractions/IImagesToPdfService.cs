using Regira.IO.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Abstractions;

public interface IImagesToPdfService
{
    Task<IMemoryFile?> ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default);
}