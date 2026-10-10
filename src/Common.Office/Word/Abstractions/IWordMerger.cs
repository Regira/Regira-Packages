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

    /// <summary>
    /// Creates every input and joins the results into one document, laid out as <paramref name="options"/> asks;
    /// <c>null</c> merges as <see cref="Merge(IEnumerable{WordTemplateInput}, CancellationToken)"/> does.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The merger cannot honour <paramref name="options"/>. This default implementation honours <c>null</c> and the
    /// defaults of <see cref="MergeOptions"/> only.
    /// </exception>
    Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, MergeOptions? options, CancellationToken cancellationToken = default)
        => options?.FollowOn == true
            ? Task.FromException<IMemoryFile>(new NotSupportedException(
                $"{GetType().Name} cannot run merged documents on from the previous one's last page ({nameof(MergeOptions)}.{nameof(MergeOptions.FollowOn)})."))
            : Merge(inputs, cancellationToken);
}