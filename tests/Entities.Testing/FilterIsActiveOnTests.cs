using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing;

/// <summary><c>FilterIsActiveOn</c> keeps the periods holding a date, and, as every other filter, filters nothing without one.</summary>
[TestFixture]
public class FilterIsActiveOnTests
{
    private sealed class Period : IHasStartEndDate
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    private static readonly Period OpenEnded = new();
    private static readonly Period Starting2026 = new() { StartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    private static readonly Period Ending2026 = new() { EndDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) };
    private static readonly Period Year2025 = new()
    {
        StartDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)
    };
    private static readonly Period[] Periods = [OpenEnded, Starting2026, Ending2026, Year2025];

    [Test]
    public void Without_A_Date_Nothing_Is_Filtered()
        => Assert.That(Periods.AsQueryable().FilterIsActiveOn(null), Is.EqualTo(Periods));

    [Test]
    public void A_Date_Keeps_The_Periods_Holding_It()
        => Assert.That(Periods.AsQueryable().FilterIsActiveOn(new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            Is.EqualTo(new[] { OpenEnded, Ending2026, Year2025 }));
}
