using Regira.Utilities;

namespace Regira.Office.Excel.Internal;

/// <summary>
/// Prepares the rows of an untyped sheet for writing.
/// </summary>
/// <remarks>
/// Published Excel backends call this through <c>InternalsVisibleTo</c>: changing a signature breaks them at run time
/// (<see cref="MissingMethodException"/>), so such a change ships every Excel backend on the same version.
/// </remarks>
internal static class SheetRows
{
    private static readonly DictionaryOptions RowOptions = new() { Recursive = false, IgnoreCase = false };

    /// <summary>
    /// Each item as a new dictionary whose keys compare case-insensitively: a dictionary's entries, an object's properties.
    /// A property holding an object stays that object, so its cell shows the object's <see cref="object.ToString"/>.
    /// </summary>
    /// <exception cref="ArgumentException">An item holds two keys that differ only in case: a column is one key, whatever its case</exception>
    public static List<IDictionary<string, object?>> ToDictionaries(IEnumerable<object>? data)
        => (data ?? []).Select(item =>
        {
            // merged, not copied: a copy into a case-insensitive dictionary throws on "Id" beside "ID"
            var row = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var (key, value) in DictionaryUtility.ToDictionary(item, RowOptions))
            {
                if (!row.TryAdd(key, value))
                {
                    var other = row.Keys.First(existing => string.Equals(existing, key, StringComparison.InvariantCultureIgnoreCase));
                    throw new ArgumentException($"A row holds the keys '{other}' and '{key}', which differ only in case; a column is one key, whatever its case.", nameof(data));
                }
            }
            return (IDictionary<string, object?>)row;
        }).ToList();

    /// <summary>
    /// Every key the rows use, compared case-insensitively, as it is first spelled and in the order it first appears.
    /// </summary>
    public static List<string> Keys(IEnumerable<IDictionary<string, object?>> rows)
        => rows
            .SelectMany(row => row.Keys)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .ToList();
}
