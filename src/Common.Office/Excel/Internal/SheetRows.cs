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
    /// Of keys that differ only in case, the first spelling and its value are kept, as across rows.
    /// A property holding an object stays that object, so its cell shows the object's <see cref="object.ToString"/>.
    /// </summary>
    public static List<IDictionary<string, object?>> ToDictionaries(IEnumerable<object>? data)
        => (data ?? []).Select(item =>
        {
            // merged, not copied: a copy into a case-insensitive dictionary throws on "Id" beside "ID"
            var row = new Dictionary<string, object?>(StringComparer.InvariantCultureIgnoreCase);
            foreach (var (key, value) in DictionaryUtility.ToDictionary(item, RowOptions))
            {
                row.TryAdd(key, value);
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
