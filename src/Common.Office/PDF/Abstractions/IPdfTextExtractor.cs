using Regira.IO.Abstractions;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfTextExtractor
{
    Task<string> GetText(IMemoryFile pdf, CancellationToken cancellationToken = default);
}