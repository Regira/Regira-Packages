using Regira.Utilities;

namespace Common.Testing.Utilities;

// mutates the process-wide policy — keep out of the parallel test queue
[TestFixture]
[NonParallelizable]
public class DateTimeDefaultsTests
{
    [TearDown]
    public void TearDown()
    {
        DateTimeDefaults.UseUtc = true;
    }

    [Test]
    public void Defaults_To_Utc()
    {
        Assert.That(DateTimeDefaults.UseUtc, Is.True);
    }

    [Test]
    public void Can_Be_Disabled()
    {
        DateTimeDefaults.UseUtc = false;

        Assert.That(DateTimeDefaults.UseUtc, Is.False);
    }
}
