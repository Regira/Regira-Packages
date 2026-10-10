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
    /// Gives each header cell a unique key: the header text, <c>Column{n}</c> for a blank header, and
    /// <c>{header}_{i}</c> for a repeated one. A key never takes the text of another column's header.
    /// </summary>
    /// <param name="headerCells">The values of the header row, one per column, blank cells included</param>
    public static string[] Keys(IReadOnlyList<object?> headerCells)
    {
        var texts = headerCells.Select(HeaderText).ToList();
        // case-insensitive, as the writers compare keys, so a sheet that is read can be written back
        var literals = new HashSet<string>(texts.Where(x => x != null)!, StringComparer.InvariantCultureIgnoreCase);
        var used = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

        return texts
            .Select((text, i) => text != null && used.Add(text)
                ? text
                : Generate(text ?? $"Column{i + 1}", text == null))
            .ToArray();

        string Generate(string baseName, bool tryBaseName)
        {
            if (tryBaseName && !literals.Contains(baseName) && used.Add(baseName))
            {
                return baseName;
            }
            for (var i = 2; ; i++)
            {
                var candidate = $"{baseName}_{i}";
                if (!literals.Contains(candidate) && used.Add(candidate))
                {
                    return candidate;
                }
            }
        }
    }

    private static string? HeaderText(object? value)
    {
        var text = value?.ToString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}
