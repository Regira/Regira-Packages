using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Web.Analytics.Config;
using Regira.Web.Analytics.Services;
using Web.Analytics.Testing.Infrastructure;

namespace Web.Analytics.Testing;

[TestFixture]
public class BotDetectorTests
{
    private const string ChromeUa =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0 Safari/537.36";

    private static BotDetector Create(BotDetectionConfig? config = null, ILogger<BotDetector>? logger = null)
        => new(new TestOptionsMonitor<BotDetectionConfig>(config ?? new BotDetectionConfig()),
            logger ?? NullLogger<BotDetector>.Instance);

    [Test]
    public void DefaultMarkers_AreActive_WithEmptyConfiguration()
    {
        using var detector = Create();
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)"), Is.True);
            Assert.That(detector.IsBot("curl/8.5.0 something"), Is.True);
            Assert.That(detector.IsBot(ChromeUa), Is.False);
        });
    }

    [Test]
    public void Matching_IsCaseInsensitive()
    {
        using var detector = Create();
        Assert.That(detector.IsBot("Mozilla/5.0 GPTBot/1.0 (+https://openai.com/gptbot)"), Is.True);
    }

    [Test]
    public void Exception_ClearsAgent_BeforeMarkersRun()
    {
        using var detector = Create();
        // "Cubot" contains the broad "bot" marker; the built-in exception must win.
        Assert.That(detector.IsBot("Mozilla/5.0 (Linux; Android 9; Cubot X19) AppleWebKit/537.36"), Is.False);
    }

    [Test]
    public void ShortOrMissingUserAgent_IsFlagged()
    {
        using var detector = Create();
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot(null), Is.True);
            Assert.That(detector.IsBot(""), Is.True);
            Assert.That(detector.IsBot("short"), Is.True);
        });
    }

    [Test]
    public void MinUserAgentLengthZero_DisablesTheLengthCheck()
    {
        using var detector = Create(new BotDetectionConfig { MinUserAgentLength = 0 });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot(null), Is.False);
            Assert.That(detector.IsBot("short"), Is.False);
        });
    }

    [Test]
    public void ConfiguredMarkers_MergeOnTopOfDefaults()
    {
        using var detector = Create(new BotDetectionConfig { Markers = ["companyscanner"] });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("Mozilla/5.0 CompanyScanner/1.0 internal probe"), Is.True);
            Assert.That(detector.IsBot("curl/8.5.0 something"), Is.True, "defaults must survive the merge");
        });
    }

    [Test]
    public void OptedOutOfDefaults_WithNothingConfigured_FlagsNothing_AndLogsError()
    {
        var logger = new CapturingLogger<BotDetector>();
        using var detector = Create(
            new BotDetectionConfig { IncludeDefaultMarkers = false, MinUserAgentLength = 0 }, logger);

        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("Mozilla/5.0 (compatible; Googlebot/2.1)"), Is.False);
            Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Error), Is.True);
        });
    }

    [Test]
    public void HotReload_SwapsTheMarkerSet()
    {
        var monitor = new TestOptionsMonitor<BotDetectionConfig>(new BotDetectionConfig());
        using var detector = new BotDetector(monitor, NullLogger<BotDetector>.Instance);

        Assert.That(detector.IsBot("Mozilla/5.0 SpecialAgent/1.0 probe"), Is.False);

        monitor.Set(new BotDetectionConfig { Markers = ["specialagent"] });

        Assert.That(detector.IsBot("Mozilla/5.0 SpecialAgent/1.0 probe"), Is.True);
    }
}