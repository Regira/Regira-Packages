using Regira.IO.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfSplitter
{
    Task<IEnumerable<IMemoryFile>> Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default);
    Task<int> GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default);
}