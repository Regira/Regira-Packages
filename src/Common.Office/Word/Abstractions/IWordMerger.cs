using Regira.IO.Abstractions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordMerger
{
    /// <summary>
    /// Creates every input and joins the results into one document, each starting on a new page. An input keeps its
    /// own section breaks.
    /// </summary>
    Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default);
}