using Cronos;
using Regira.System.Scheduling.Abstractions;

namespace Hosting.Testing.Scheduling;

[TestFixture]
public class CronScheduleTests
{
    // The portable subset: what every backend reads the same way.
    public static readonly string[] Portable =
    [
        "* * * * *",
        "*/15 * * * *",
        "0 7 * * MON-FRI",
        "0 7 * * mon-fri",
        "30 6 1 * *",
        "0 0 1,15 * *",
        "0 0 1-7 JAN,JUL *",
        "0 12 * 3-9/3 *",
        "5/20 * * * *",
        "0 9-17/2 * * 1-5",
        "0 0 * * 0",
        "0 0 * * 7",
        "0 0 * * 5-7",
        "0 0 * * SUN,WED,SAT",
        "0 0 * * */2",
        "0 0 * * 1/2",
        "0 0 * * MON/2",
        "0 0 * * 3/2",
        "0 0 * * 1/3",
        "0 0 * * 7/2",
        "0 0 * * 2-6/2",
        "0 0 1 MAR/2 *",
        "0 0 1 JAN-MAR/2 *",
        "0 0 1 feb-oct/4,DEC *",
        "05 07 * * *",
        "0 22 31 12 *",
        "* * * * * *",
        "*/10 * * * * *",
        "30 0 7 * * MON-FRI",
        "0 0 0 1 JAN *"
    ];

    [TestCaseSource(nameof(Portable))]
    public void Parses_The_Portable_Subset(string expression)
    {
        var schedule = CronSchedule.Parse(expression);

        Assert.That(schedule.Expression, Is.EqualTo(expression));
        Assert.That(schedule.HasSeconds, Is.EqualTo(expression.Split(' ').Length == 6));
        // the parser of the built-in backend, and of Hangfire, reads every expression of the subset
        Assert.DoesNotThrow(() => CronExpression.Parse(expression, schedule.HasSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard));
    }

    [TestCase("0 0 ? * MON", "?, L, W or #")]
    [TestCase("0 0 L * *", "?, L, W or #")]
    [TestCase("0 0 15W * *", "?, L, W or #")]
    [TestCase("0 0 * * 5#2", "?, L, W or #")]
    [TestCase("0 0 * * 5L", "?, L, W or #")]
    [TestCase("@daily", "macros")]
    [TestCase("0 0 * *", "found 4")]
    [TestCase("0 0 0 * * * 2030", "found 7")]
    [TestCase("", "found 0")]
    [TestCase("0 0 13 * FRI", "both restricted")]
    [TestCase("60 * * * *", "'60' in the minute field")]
    [TestCase("0 24 * * *", "'24' in the hour field")]
    [TestCase("0 0 0 * *", "'0' in the day of month field")]
    [TestCase("0 0 * 13 *", "'13' in the month field")]
    [TestCase("0 0 * * 8", "'8' in the day of week field")]
    [TestCase("0 0 * FOO *", "'FOO' in the month field")]
    [TestCase("0 0 * * FRI-MON", "runs backwards")]
    [TestCase("0 22-2 * * *", "runs backwards")]
    [TestCase("*/0 * * * *", "step '0'")]
    [TestCase("*/5/2 * * * *", "more than one step")]
    [TestCase("1,,2 * * * *", "empty list item")]
    [TestCase("-5 * * * *", "'' in the minute field")]
    [TestCase("*/+5 * * * *", "step '+5'")]
    [TestCase("+5 * * * *", "'+5' in the minute field")]
    [TestCase("007 * * * *", "'007' in the minute field")]
    [TestCase("*/005 * * * *", "step '005'")]
    public void Refuses_What_Is_Outside_The_Subset(string expression, string reason)
    {
        var ex = Assert.Throws<FormatException>(() => CronSchedule.Parse(expression));

        Assert.That(ex!.Message, Does.Contain(reason));
        Assert.That(ex.Message, Does.StartWith($"Cron expression '{expression}' is not supported: "));
        Assert.That(CronSchedule.TryParse(expression, out _), Is.False);
    }

    [Test]
    public void Normalizes_Whitespace()
    {
        var schedule = CronSchedule.Parse("  0   7 * *\tMON-FRI ");

        Assert.That(schedule.Expression, Is.EqualTo("0 7 * * MON-FRI"));
        Assert.That(schedule, Is.EqualTo(CronSchedule.Parse("0 7 * * mon-fri")));
    }

    [Test]
    public void A_String_Converts_To_A_Schedule()
    {
        CronSchedule schedule = "*/15 * * * *";

        Assert.That(schedule.ToString(), Is.EqualTo("*/15 * * * *"));
        Assert.Throws<FormatException>(() => { CronSchedule _ = "@hourly"; });
    }

    [Test]
    public void An_Empty_Schedule_Has_No_Expression()
    {
        Assert.Throws<InvalidOperationException>(() => _ = default(CronSchedule).Expression);
    }

    [TestCase("0 0 * * *", new[] { 0, 1, 2, 3, 4, 5, 6 })]
    [TestCase("0 0 * * MON-FRI", new[] { 1, 2, 3, 4, 5 })]
    [TestCase("0 0 * * 7", new[] { 0 })]
    [TestCase("0 0 * * 5-7", new[] { 0, 5, 6 })]
    [TestCase("0 0 * * */2", new[] { 0, 2, 4, 6 })]
    [TestCase("0 0 * * 1/2", new[] { 0, 1, 3, 5 })]
    [TestCase("0 0 * * 7/2", new[] { 0 })]
    [TestCase("0 0 * * 2-6/2", new[] { 2, 4, 6 })]
    [TestCase("0 0 0 * * SUN,WED", new[] { 0, 3 })]
    public void Names_Its_Days_Of_The_Week(string expression, int[] days)
    {
        var result = CronSchedule.Parse(expression).GetDaysOfWeek();

        Assert.That(result.Select(d => (int)d), Is.EqualTo(days));
    }

    // The days a schedule names are the days Cronos fires it on.
    [TestCaseSource(nameof(Portable))]
    public void Its_Days_Of_The_Week_Are_The_Days_It_Fires_On(string expression)
    {
        var schedule = CronSchedule.Parse(expression);
        var cron = CronExpression.Parse(expression, schedule.HasSeconds ? CronFormat.IncludeSeconds : CronFormat.Standard);
        var from = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // one occurrence per day is enough to see the day
        var fired = Enumerable.Range(0, 366 * 2)
            .Select(day => cron.GetNextOccurrence(from.AddDays(day), inclusive: true))
            .Where(o => o != null)
            .Select(o => o!.Value.DayOfWeek)
            .Distinct()
            .OrderBy(d => d);

        Assert.That(schedule.GetDaysOfWeek(), Is.SupersetOf(fired));
        if (expression.Split(' ')[^3] == "*" && expression.Split(' ')[^2] == "*")
        {
            Assert.That(fired, Is.EqualTo(schedule.GetDaysOfWeek()));
        }
    }
}
