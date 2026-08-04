using Regira.Utilities;

namespace Common.Testing.Utilities;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DateTimeUtilityTests
{
    [Test]
    public void AsUtc_Keeps_Utc_Value_Unchanged()
    {
        var value = new DateTime(2026, 7, 17, 10, 30, 0, DateTimeKind.Utc);

        var result = value.AsUtc();

        Assert.That(result, Is.EqualTo(value));
        Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void AsUtc_Converts_Local_To_Universal_Time()
    {
        var value = new DateTime(2026, 7, 17, 10, 30, 0, DateTimeKind.Local);

        var result = value.AsUtc();

        Assert.That(result, Is.EqualTo(value.ToUniversalTime()));
        Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void AsUtc_Assumes_Unspecified_Is_Utc()
    {
        var value = new DateTime(2026, 7, 17, 10, 30, 0, DateTimeKind.Unspecified);

        var result = value.AsUtc();

        // same ticks, only the kind changes
        Assert.That(result.Ticks, Is.EqualTo(value.Ticks));
        Assert.That(result.Kind, Is.EqualTo(DateTimeKind.Utc));
    }

    [Test]
    public void AsUtc_Nullable_Passes_Null_Through()
    {
        DateTime? value = null;

        Assert.That(value.AsUtc(), Is.Null);
    }

    [Test]
    public void AsUtc_Nullable_Normalizes_Value()
    {
        DateTime? value = new DateTime(2026, 7, 17, 10, 30, 0, DateTimeKind.Unspecified);

        var result = value.AsUtc();

        Assert.That(result!.Value.Ticks, Is.EqualTo(value.Value.Ticks));
        Assert.That(result.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
    }
}
