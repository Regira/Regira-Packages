namespace Regira.Office.Excel.Internal;

/// <summary>
/// Turns a sheet's first row into the keys of its data rows.
/// </summary>
/// <remarks>
/// A vendor's own header handling keeps only one of two equal headers (MiniExcel the last, a dictionary
/// the first) or drops a column without one, so each backend reads the first row as cells and keys it here.
/// <br />Published Excel backends call this through <c>InternalsVisibleTo</c>: changing a signature breaks them at run
/// time (<see cref="MissingMethodException"/>), so such a change ships every Excel backend on the same version.
/// </remarks>
internal static class SheetHeaders
{
    /// <summary>
    /// Gives each header cell a unique key, in one pass from the left: the header text, <c>Column{n}</c> for a blank
    /// header in column <c>n</c>, and for a key taken already the first free <c>{key}_{i}</c>, from 2. Keys compare
    /// case-insensitively, as the writers compare them, so a sheet that is read can be written back.
    /// </summary>
    /// <param name="headerCells">The values of the header row, one per column, blank cells included</param>
    public static string[] Keys(IReadOnlyList<object?> headerCells)
    {
        var used = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
        // the suffix each key's next repeat starts from: used only grows, so a suffix taken once stays taken, and a
        // workbook of one repeated header is keyed in linear time, where restarting from 2 made it quadratic
        var next = new Dictionary<string, int>(StringComparer.InvariantCultureIgnoreCase);
        return headerCells
            .Select((cell, i) =>
            {
                var key = HeaderText(cell) ?? $"Column{i + 1}";
                if (used.Add(key))
                {
                    return key;
                }
                for (var n = next.GetValueOrDefault(key, 2); ; n++)
                {
                    var candidate = $"{key}_{n}";
                    if (used.Add(candidate))
                    {
                        next[key] = n + 1;
                        return candidate;
                    }
                }
            })
            .ToArray();
    }

    private static string? HeaderText(object? value)
    {
        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
