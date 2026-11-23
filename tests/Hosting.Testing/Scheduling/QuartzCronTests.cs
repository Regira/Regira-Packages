using Regira.System.Scheduling.Abstractions;
using Regira.System.Scheduling.Quartz;

namespace Hosting.Testing.Scheduling;

[TestFixture]
public class QuartzCronTests
{
    private static readonly TimeZoneInfo Brussels = TimeZoneInfo.FindSystemTimeZoneById("Europe/Brussels");

    [TestCase("*/15 * * * *", "0 */15 * * * ?")]
    [TestCase("0 7 * * MON-FRI", "0 0 7 ? * 2,3,4,5,6")]
    [TestCase("0 0 * * 0", "0 0 0 ? * 1")]
    [TestCase("0 0 * * 7", "0 0 0 ? * 1")]
    [TestCase("0 0 * * 5-7", "0 0 0 ? * 1,6,7")]
    [TestCase("0 0 1,15 * *", "0 0 0 1,15 * ?")]
    [TestCase("0 0 1-7 JAN,JUL *", "0 0 0 1-7 JAN,JUL ?")]
    [TestCase("*/10 * * * * *", "*/10 * * * * ?")]
    [TestCase("30 0 7 * * SAT", "30 0 7 ? * 7")]
    public void Translates_To_The_Quartz_Dialect(string expression, string expected)
    {
        Assert.That(QuartzCron.Translate(CronSchedule.Parse(expression)), Is.EqualTo(expected));
    }

    // Cronos, which the built-in backend and Hangfire read a schedule with, is the oracle: Quartz fires the translated
    // expression on the same occurrences, in UTC and in a zone with daylight saving.
    [TestCaseSource(typeof(CronScheduleTests), nameof(CronScheduleTests.Portable))]
    public void Quartz_Fires_On_The_Occurrences_Of_Cronos(string expression)
    {
        var schedule = CronSchedule.Parse(expression);
        var cronos = Cronos.CronExpression.Parse(expression, schedule.HasSeconds ? Cronos.CronFormat.IncludeSeconds : Cronos.CronFormat.Standard);
        DateTimeOffset[] starts =
        [
            new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero),
            // the days before summer time starts and ends in Brussels
            new(2026, 3, 27, 12, 0, 0, TimeSpan.Zero),
            new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero)
        ];

        foreach (var zone in new[] { TimeZoneInfo.Utc, Brussels })
        {
            var quartz = new Quartz.CronExpression(QuartzCron.Translate(schedule), zone);
            foreach (var start in starts)
            {
                DateTimeOffset? expected = start, actual = start;
                for (var i = 0; i < 40; i++)
                {
                    expected = cronos.GetNextOccurrence(expected!.Value, zone);
                    actual = quartz.GetNextValidTimeAfter(actual!.Value);

                    Assert.That(actual?.ToUniversalTime(), Is.EqualTo(expected?.ToUniversalTime()), $"occurrence {i + 1} of '{expression}' after {start:u} in {zone.Id}");
                }
            }
        }
    }
}
