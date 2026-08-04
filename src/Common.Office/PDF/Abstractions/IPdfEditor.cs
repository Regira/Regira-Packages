using Regira.IO.Abstractions;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfEditor : IPdfMerger, IPdfSplitter
{
    Task<IMemoryFile?> RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default);
}