using Regira.IO.Abstractions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordMerger
{
    Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default);
}