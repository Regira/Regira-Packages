using Regira.IO.Abstractions;
using Regira.Office.OCR.Models.DTO;

namespace Regira.Office.OCR.Abstractions;

public interface IOcrService
{
    Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default);
}
