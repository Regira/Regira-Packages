using Regira.IO.Storage.SSH;

namespace IO.Testing.SSH;

// How a configured host key fingerprint is compared to the one the server presents; no server is needed.
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SftpHostKeyTests
{
    // the form SSH.NET reports: unpadded base64, without the "SHA256:" prefix
    private const string Presented = "ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og";

    [TestCase("SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og")]
    [TestCase("sha256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og")]
    [TestCase("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og")]
    [TestCase("ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og=")]
    [TestCase("  SHA256:ohD8VZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og ")]
    public void Trusts_The_Configured_Fingerprint(string configured)
        => Assert.That(Probe.Matches(configured, Presented), Is.True);

    [TestCase("SHA256:OHD8VZEXGWO6EZ8GSEJQ9WPAFGLFSOFLOTGGQCQO6OG")]
    [TestCase("SHA256:AAAAVZEXGWo6Ez8GSEJQ9WpafgLFsOfLOtGGQCQo6Og")]
    [TestCase("MD5:97:70:33:82:fd:29:3a:73:39:af:6a:07:ad:f8:80:49")]
    public void Refuses_Any_Other_Key(string configured)
        => Assert.That(Probe.Matches(configured, Presented), Is.False);

    private sealed class Probe(SftpConfig config) : SftpCommunicator(config)
    {
        public static bool Matches(string expected, string presented) => MatchesFingerprint(expected, presented);
    }
}
