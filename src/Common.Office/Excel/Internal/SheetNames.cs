namespace Regira.Office.Excel.Internal;

/// <summary>
/// Resolves the names of the sheets to write: an explicit name is validated against Excel's rules,
/// a missing one is generated from a backend's pattern (<c>Sheet-{n}</c>), skipping the names already taken.
/// </summary>
/// <remarks>
/// The exceptions name <c>sheets</c>, the parameter of the public <c>Create</c> methods.
/// <br />Published Excel backends call this through <c>InternalsVisibleTo</c>: changing a signature breaks them at run
/// time (<see cref="MissingMethodException"/>), so such a change ships every Excel backend on the same version.
/// </remarks>
internal static class SheetNames
{
    public const int MaxLength = 31;
    private const string SheetsParameter = "sheets";
    private static readonly char[] InvalidChars = [':', '\\', '/', '?', '*', '[', ']'];

    public static string[] Resolve(IReadOnlyList<string?> names, string generatedName = "Sheet-{0}")
    {
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in names.Where(n => n != null))
        {
            Validate(name!);
            if (!taken.Add(name!))
            {
                throw new ArgumentException($"Sheet name '{name}' is used more than once. Excel compares sheet names case-insensitively.", SheetsParameter);
            }
        }

        var index = 0;
        return names
            .Select(name =>
            {
                if (name != null)
                {
                    return name;
                }
                string generated;
                do
                {
                    generated = string.Format(generatedName, ++index);
                } while (!taken.Add(generated));
                return generated;
            })
            .ToArray();
    }

    private static void Validate(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A sheet name cannot be empty. Leave Name null to have one generated.", SheetsParameter);
        }
        if (name.Length > MaxLength)
        {
            throw new ArgumentException($"Sheet name '{name}' is {name.Length} characters long; Excel allows at most {MaxLength}.", SheetsParameter);
        }
        if (name.IndexOfAny(InvalidChars) >= 0)
        {
            throw new ArgumentException($"Sheet name '{name}' contains a character Excel does not allow: {string.Join(" ", InvalidChars)}", SheetsParameter);
        }
        if (name.StartsWith('\'') || name.EndsWith('\''))
        {
            throw new ArgumentException($"Sheet name '{name}' cannot start or end with an apostrophe.", SheetsParameter);
        }
        if (name.Equals("History", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Sheet name 'History' is reserved by Excel.", SheetsParameter);
        }
    }
}
