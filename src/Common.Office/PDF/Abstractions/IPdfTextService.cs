using Regira.IO.Abstractions;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfTextService : IPdfTextExtractor
{
    Task<IList<string>> GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default);
    Task<IMemoryFile?> RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default);
}