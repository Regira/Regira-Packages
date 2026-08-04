using Regira.IO.Abstractions;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfMerger
{
    Task<IMemoryFile?> Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default);
}