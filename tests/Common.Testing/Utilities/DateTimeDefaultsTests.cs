using Regira.Utilities;

namespace Common.Testing.Utilities;

// Nothing in this assembly sets the process-wide policy, so this reads the default the property starts with. Turning
// it off is covered where it is read: the Entities UTC tests.
[TestFixture]
public class DateTimeDefaultsTests
{
    [Test]
    public void Defaults_To_Utc()
    {
        Assert.That(DateTimeDefaults.UseUtc, Is.True);
    }
}
