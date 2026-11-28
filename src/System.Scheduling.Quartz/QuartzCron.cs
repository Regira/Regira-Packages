using System.Text.RegularExpressions;
using Regira.System.Scheduling.Abstractions;

namespace Regira.System.Scheduling.Quartz;

/// <summary>Writes a <see cref="CronSchedule"/> in the cron dialect of Quartz.</summary>
public static class QuartzCron
{
    private static readonly string[] MonthNames = ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    /// <summary>
    /// The Quartz expression that fires on the same occurrences: a leading <c>0</c> for seconds when the schedule has five fields,
    /// <c>?</c> in the day field that is not restricted, the months as numbers, and the days of the week as Quartz numbers them (Sunday is 1).
    /// </summary>
    public static string Translate(CronSchedule schedule)
    {
        var fields = schedule.Expression.Split(' ');
        if (!schedule.HasSeconds)
        {
            fields = ["0", .. fields];
        }

        // seconds, minutes, hours, day of month, month, day of week
        // Quartz reads a month name with a step (MAR/2, JAN-MAR/2) as the name alone; the numbers it reads as written
        fields[4] = Regex.Replace(fields[4], "[A-Za-z]+", name => (Array.FindIndex(MonthNames, m => m.Equals(name.Value, StringComparison.OrdinalIgnoreCase)) + 1).ToString());
        if (fields[5] == "*")
        {
            fields[5] = "?";
        }
        else
        {
            fields[3] = "?";
            fields[5] = string.Join(",", schedule.GetDaysOfWeek().Select(day => (int)day + 1));
        }

        return string.Join(" ", fields);
    }
}
