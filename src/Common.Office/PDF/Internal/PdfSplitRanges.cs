using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Internal;

/// <summary>
/// The pages each <see cref="PdfSplitRange"/> covers, checked alike by every backend before it splits anything.
/// </summary>
internal static class PdfSplitRanges
{
    /// <summary>
    /// The first and last page of each range, counted from 1; a range without an <see cref="PdfSplitRange.End"/> ends
    /// at the document's last page.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A range starts before page 1, ends after the document's last page, or starts after it ends.
    /// </exception>
    public static IReadOnlyList<(int Start, int End)> Resolve(IEnumerable<PdfSplitRange> ranges, int pageCount)
    {
        var resolved = new List<(int Start, int End)>();
        foreach (var range in ranges)
        {
            var end = range.End ?? pageCount;
            if (range.Start < 1 || end > pageCount || range.Start > end)
            {
                throw new ArgumentOutOfRangeException(nameof(ranges), range,
                    $"Pages {range.Start} to {end} are not a range of this document's pages 1 to {pageCount}.");
            }
            resolved.Add((range.Start, end));
        }
        return resolved;
    }
}
