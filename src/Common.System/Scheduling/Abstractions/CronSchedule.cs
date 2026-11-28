namespace Regira.System.Scheduling.Abstractions;

/// <summary>
/// A cron expression in the subset every scheduling backend reads the same way:
/// five fields (minute, hour, day of month, month, day of week) with an optional leading seconds field.
/// <list type="bullet">
/// <item>Values, <c>*</c>, lists (<c>,</c>), ranges (<c>-</c>), steps (<c>/</c>), and month and weekday names.</item>
/// <item>Day of week is 0 to 6 with Sunday as 0; 7 is also Sunday.</item>
/// <item>A value with a step runs to the end of its field, which for day of week is 7: <c>1/2</c> is Monday, Wednesday, Friday and Sunday.</item>
/// <item>At most one of day-of-month and day-of-week is restricted.</item>
/// <item>Not in the subset: <c>?</c>, <c>L</c>, <c>W</c>, <c>#</c>, year fields, and macros such as <c>@daily</c>.</item>
/// </list>
/// </summary>
public readonly struct CronSchedule : IEquatable<CronSchedule>
{
    private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];
    private static readonly string[] DayNames = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

    private sealed record Field(string Name, int Min, int Max, string[]? Names = null, int NamesStart = 0);
    private static readonly Field Second = new("second", 0, 59);
    private static readonly Field Minute = new("minute", 0, 59);
    private static readonly Field Hour = new("hour", 0, 23);
    private static readonly Field DayOfMonth = new("day of month", 1, 31);
    private static readonly Field Month = new("month", 1, 12, MonthNames, 1);
    private static readonly Field WeekDay = new("day of week", 0, 7, DayNames);

    private readonly string? _expression;

    private CronSchedule(string expression, bool hasSeconds)
    {
        _expression = expression;
        HasSeconds = hasSeconds;
    }

    /// <summary>The expression, its fields separated by one space.</summary>
    public string Expression => _expression ?? throw new InvalidOperationException("This cron schedule is empty: create one with CronSchedule.Parse.");
    /// <summary><c>true</c> when the expression has six fields, the first being seconds.</summary>
    public bool HasSeconds { get; }

    /// <summary>The days of the week the expression fires on: all seven when its day-of-week field is <c>*</c>.</summary>
    public IReadOnlyList<DayOfWeek> GetDaysOfWeek()
    {
        var dayField = Expression.Split(' ')[^1];
        return ReadValues(dayField, WeekDay, Expression)
            .Select(d => (DayOfWeek)(d % 7))
            .Distinct()
            .OrderBy(d => d)
            .ToArray();
    }

    /// <summary>Reads an expression of the portable subset.</summary>
    /// <exception cref="FormatException">The expression is outside the subset.</exception>
    public static CronSchedule Parse(string expression)
    {
        if (expression == null)
        {
            throw new ArgumentNullException(nameof(expression));
        }

        var parts = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var normalized = string.Join(" ", parts);
        if (normalized.StartsWith('@'))
        {
            throw Refuse(normalized, "macros such as @daily are not supported; write the five fields");
        }
        if (parts.Length is not (5 or 6))
        {
            throw Refuse(normalized, $"expected 5 fields (minute, hour, day of month, month, day of week) or 6 with leading seconds, found {parts.Length}");
        }

        var hasSeconds = parts.Length == 6;
        Field[] fields = hasSeconds
            ? [Second, Minute, Hour, DayOfMonth, Month, WeekDay]
            : [Minute, Hour, DayOfMonth, Month, WeekDay];
        for (var i = 0; i < parts.Length; i++)
        {
            ReadValues(parts[i], fields[i], normalized);
        }

        if (parts[^3] != "*" && parts[^1] != "*")
        {
            throw Refuse(normalized, "day of month and day of week are both restricted; leave one of them as *");
        }

        return new CronSchedule(normalized, hasSeconds);
    }

    public static bool TryParse(string? expression, out CronSchedule schedule)
    {
        schedule = default;
        if (expression == null)
        {
            return false;
        }

        try
        {
            schedule = Parse(expression);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Validates a field and returns the values it names.
    private static SortedSet<int> ReadValues(string text, Field field, string expression)
    {
        var values = new SortedSet<int>();
        foreach (var item in text.Split(','))
        {
            if (item.Length == 0)
            {
                throw Refuse(expression, $"the {field.Name} field '{text}' has an empty list item");
            }
            if (item.IndexOfAny(['?', 'L', 'W', '#']) >= 0 && !IsName(item, field))
            {
                throw Refuse(expression, $"the {field.Name} field '{text}' uses ?, L, W or #, which are not supported");
            }

            var stepParts = item.Split('/');
            if (stepParts.Length > 2)
            {
                throw Refuse(expression, $"the {field.Name} field '{text}' has more than one step");
            }

            int from, to;
            var rangeParts = stepParts[0].Split('-');
            if (stepParts[0] == "*")
            {
                from = field.Min;
                // 7 is a second name for Sunday, not an eighth day
                to = field == WeekDay ? 6 : field.Max;
            }
            else if (rangeParts.Length == 1)
            {
                from = ReadValue(rangeParts[0], field, text, expression);
                // a value with a step runs to the end of the field: for a day of the week that is 7, the second Sunday
                to = stepParts.Length == 2 ? field.Max : from;
            }
            else if (rangeParts.Length == 2)
            {
                from = ReadValue(rangeParts[0], field, text, expression);
                to = ReadValue(rangeParts[1], field, text, expression);
                if (from > to)
                {
                    throw Refuse(expression, $"the {field.Name} range '{stepParts[0]}' runs backwards; write it as a list of two ranges");
                }
            }
            else
            {
                throw Refuse(expression, $"the {field.Name} field '{text}' is not a value, a range or *");
            }

            var step = 1;
            if (stepParts.Length == 2 && (!IsNumber(stepParts[1]) || !int.TryParse(stepParts[1], out step) || step < 1 || step > field.Max))
            {
                throw Refuse(expression, $"the {field.Name} step '{stepParts[1]}' is not a number from 1 to {field.Max}");
            }

            for (var value = from; value <= to; value += step)
            {
                values.Add(value);
            }
        }

        return values;
    }
    private static int ReadValue(string text, Field field, string fieldText, string expression)
    {
        var nameIndex = field.Names == null ? -1 : Array.FindIndex(field.Names, n => n.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (nameIndex >= 0)
        {
            return nameIndex + field.NamesStart;
        }
        if (IsNumber(text) && int.TryParse(text, out var value) && value >= field.Min && value <= field.Max)
        {
            return value;
        }

        throw Refuse(expression, $"'{text}' in the {field.Name} field '{fieldText}' is not a value from {field.Min} to {field.Max}"
                                 + (field.Names == null ? "" : $" or a name ({field.Names[0]}-{field.Names[^1]})"));
    }
    // one or two digits, without a sign: what every backend reads as a number
    private static bool IsNumber(string text)
        => text.Length is 1 or 2 && text.All(c => c is >= '0' and <= '9');
    private static bool IsName(string item, Field field)
        => field.Names != null && item.Split('-', '/').All(p => p.All(char.IsLetter) ? field.Names.Contains(p, StringComparer.OrdinalIgnoreCase) : p.All(char.IsDigit));
    private static FormatException Refuse(string expression, string reason)
        => new($"Cron expression '{expression}' is not supported: {reason}.");

    public static implicit operator CronSchedule(string expression) => Parse(expression);

    public bool Equals(CronSchedule other) => string.Equals(_expression, other._expression, StringComparison.OrdinalIgnoreCase);
    public override bool Equals(object? obj) => obj is CronSchedule other && Equals(other);
    public override int GetHashCode() => _expression == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(_expression);
    public static bool operator ==(CronSchedule left, CronSchedule right) => left.Equals(right);
    public static bool operator !=(CronSchedule left, CronSchedule right) => !left.Equals(right);
    public override string ToString() => _expression ?? string.Empty;
}
