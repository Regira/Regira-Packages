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

    /// <summary>Agents that fetch for a prompt name no bot; only the product token gives them away.</summary>
    [TestCase("Mozilla/5.0 (compatible; Claude-User/1.0; +Claude-User@anthropic.com)")]
    [TestCase("Mozilla/5.0 AppleWebKit/537.36 (KHTML, like Gecko; compatible; Perplexity-User/1.0)")]
    [TestCase("Mozilla/5.0 (Linux; Android 6.0.1; Nexus 5X Build/MMB29P) AppleWebKit/537.36 "
              + "(KHTML, like Gecko) Chrome/151.0.0.0 Mobile Safari/537.36 (compatible; GoogleOther)")]
    public void AiAgentsWithoutBotInTheirName_AreFlagged(string userAgent)
    {
        using var detector = Create();
        Assert.That(detector.IsBot(userAgent), Is.True);
    }

    [Test]
    public void ContactUrlConvention_IsAMarker_OnItsOwn()
    {
        using var detector = Create();
        // Crawlers announce where to complain; browsers never do.
        Assert.That(detector.IsBot("Mozilla/5.0 (compatible; ForestEngine/1.0; +https://forestengine.net/)"), Is.True);
    }

    [Test]
    public void Exception_CancelsOnlyTheMarkerItOverlaps()
    {
        using var detector = Create();
        Assert.Multiple(() =>
        {
            // "Cubot" contains the broad "bot" marker; the built-in exception must win.
            Assert.That(detector.IsBot("Mozilla/5.0 (Linux; Android 9; Cubot X19) AppleWebKit/537.36"), Is.False);
            // ... but it must not excuse the rest of the agent.
            Assert.That(detector.IsBot("Mozilla/5.0 (Linux; Android 9; Cubot X19) AppleWebKit/537.36 PetalBot"), Is.True);
        });
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
        using var detector = Create(new BotDetectionConfig { MinUserAgentLength = 0, RequireBrowserToken = false });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot(null), Is.False);
            Assert.That(detector.IsBot("short"), Is.False);
        });
    }

    /// <summary>The long tail of clients and one-off crawlers, none of which any marker list names.</summary>
    [TestCase("RootEvidence/1.0")]
    [TestCase("TrashHound-Wildcard-Resolver/1")]
    [TestCase("SimplePie/1.9.0 (Feed Parser; http://simplepie.org; Allow like Gecko) Build/1761674916")]
    [TestCase("Mozlila/5.0 (Linux; Android 7.0; SM-G892A Bulid/NRD90M; wv) AppleWebKit/537.36 "
              + "(KHTML, like Gecko) Version/4.0 Chrome/60.0.3112.107 Moblie Safari/537.36")]
    public void AgentNamingNoBrowser_IsFlagged(string userAgent)
    {
        using var detector = Create();
        Assert.That(detector.IsBot(userAgent), Is.True);
    }

    [Test]
    public void RequireBrowserTokenFalse_LeavesDetectionToTheMarkers()
    {
        using var detector = Create(new BotDetectionConfig { RequireBrowserToken = false });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("RootEvidence/1.0"), Is.False);
            Assert.That(detector.IsBot("curl/8.5.0 something"), Is.True, "markers still run");
        });
    }

    [Test]
    public void ConfiguredBrowserToken_KeepsANonBrowserClientHuman()
    {
        using var detector = Create(new BotDetectionConfig { BrowserTokens = ["kioskshell/"] });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("KioskShell/2.1 (lobby terminal)"), Is.False);
            Assert.That(detector.IsBot("RootEvidence/1.0"), Is.True, "defaults must survive the merge");
        });
    }

    /// <summary>Guards the two broad additions — the "+contact-url" marker and the browser-token check.</summary>
    [TestCase("Opera/9.80 (J2ME/MIDP; Opera Mini/9.80/37.8018; U; en) Presto/2.12.423 Version/12.16")]
    [TestCase("Mozilla/5.0 (Nintendo Switch; WifiWebAuthApplet) AppleWebKit/609.4 (KHTML, like Gecko) NintendoBrowser/5.1.0.22023")]
    [TestCase("Mozilla/5.0 (SMART-TV; LINUX; Tizen 6.0) AppleWebKit/537.36 (KHTML, like Gecko) 76.0.3809.146 TV Safari/537.36")]
    [TestCase("Mozilla/5.0 (iPhone; CPU iPhone OS 17_2 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Mobile/21C62 [Pinterest/iOS]")]
    [TestCase("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) "
              + "Claude/1.40609.0 Chrome/148.0.7778.280 Safari/537.36 MSIX")]
    public void RealBrowsers_AreNotFlagged(string userAgent)
    {
        using var detector = Create();
        Assert.That(detector.IsBot(userAgent), Is.False);
    }

    /// <summary>The signal a scanner cannot dress up: these all arrived behind a copied Chrome string.</summary>
    [TestCase("/wp-admin/css/colors/midnight/", null)]
    [TestCase("//wp-includes/fonts/", null)]
    [TestCase("/.git/config", null)]
    [TestCase("/home/ubuntu/.aws/credentials", null)]
    [TestCase("/@fs/root/.aws/credentials", "?raw??")]
    [TestCase("/actuator/env", null)]
    [TestCase("/telescope/requests", null)]
    [TestCase("/proc/self/environ", null)]
    [TestCase("/secure/..;/actuator/env", null)]
    [TestCase("/id_rsa", null)]
    [TestCase("/Dockerfile", null)]
    [TestCase("/vendor/phpunit/phpunit/src/Util/PHP/", null)]
    // The path is the site's own home page; the attack rides in the query.
    [TestCase("/", "?file=../../../../etc/passwd")]
    [TestCase("/", "?file=..%2F..%2Fetc%2Fpasswd")]
    public void ProbeRequests_AreFlagged(string path, string? queryString)
    {
        using var detector = Create();
        Assert.That(detector.IsProbe(path, queryString), Is.True);
    }

    /// <summary>Page routes a real site serves; a probe rule that flags one of these costs a visitor.</summary>
    [TestCase("/", null)]
    [TestCase("/contact", "?subject=custom-webapp")]
    [TestCase("/licensing/confirm", "?requestId=339ce3801e3643e62a1908df08d91317")]
    [TestCase("/the-searchobject-pattern-composable-filtering-for-ef-core-apis", null)]
    [TestCase("/", "?category=data-ef-core&tag=recursive-cte")]
    [TestCase("/admin", null)]
    [TestCase("/login", null)]
    [TestCase("/dashboard", null)]
    [TestCase("/graphql", null)]
    [TestCase("/account/credentials", null)]
    [TestCase("/.well-known/change-password", null)]
    // Dots a visitor typed: ".." is an attack only with a separator behind it.
    [TestCase("/search", "?q=wait..%20what")]
    [TestCase("/articles", "?published=2020..2024")]
    public void RealPageRoutes_AreNotProbes(string path, string? queryString)
    {
        using var detector = Create();
        Assert.That(detector.IsProbe(path, queryString), Is.False);
    }

    [Test]
    public void DetectProbeRequestsFalse_TurnsTheWholeShapeCheckOff()
    {
        using var detector = Create(new BotDetectionConfig { DetectProbeRequests = false });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsProbe("/wp-admin/", null), Is.False);
            Assert.That(detector.IsProbe("/", "?file=../../.env"), Is.False, "the shape checks go too");
        });
    }

    [Test]
    public void ConfiguredProbePaths_MergeOnTopOfDefaults()
    {
        using var detector = Create(new BotDetectionConfig { ProbePaths = ["/legacy-cms"] });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsProbe("/legacy-cms/login"), Is.True);
            Assert.That(detector.IsProbe("/actuator/env"), Is.True, "defaults must survive the merge");
        });
    }

    [Test]
    public void ProbeDetection_IsIndependentOfTheAgent()
    {
        using var detector = Create();
        Assert.Multiple(() =>
        {
            // A scanner wearing a real browser string: the agent alone says nothing.
            Assert.That(detector.IsBot(ChromeUa), Is.False);
            Assert.That(detector.IsProbe("/wp-admin/", null), Is.True);
        });
    }

    [Test]
    public void ConfiguredMarkers_MergeOnTopOfDefaults()
    {
        using var detector = Create(new BotDetectionConfig { Markers = ["acmeprobe"] });
        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("Mozilla/5.0 AcmeProbe/1.0 uptime check"), Is.True);
            Assert.That(detector.IsBot("curl/8.5.0 something"), Is.True, "defaults must survive the merge");
        });
    }

    [Test]
    public void OptedOutOfDefaults_WithNothingConfigured_FlagsNothing_AndLogsError()
    {
        var logger = new CapturingLogger<BotDetector>();
        using var detector = Create(
            new BotDetectionConfig
            {
                IncludeDefaultMarkers = false, MinUserAgentLength = 0, DetectProbeRequests = false
            }, logger);

        Assert.Multiple(() =>
        {
            Assert.That(detector.IsBot("Mozilla/5.0 (compatible; Googlebot/2.1)"), Is.False);
            Assert.That(detector.IsBot("RootEvidence/1.0"), Is.False, "no browser tokens left to require");
            Assert.That(detector.IsProbe("/wp-admin/"), Is.False);
            Assert.That(logger.Entries.Any(e => e.Level == LogLevel.Error), Is.True);
        });
    }

    [Test]
    public void HotReload_SwapsTheMarkerSet()
    {
        var monitor = new TestOptionsMonitor<BotDetectionConfig>(new BotDetectionConfig());
        using var detector = new BotDetector(monitor, NullLogger<BotDetector>.Instance);

        Assert.That(detector.IsBot("Mozilla/5.0 AcmeProbe/1.0 uptime check"), Is.False);

        monitor.Set(new BotDetectionConfig { Markers = ["acmeprobe"] });

        Assert.That(detector.IsBot("Mozilla/5.0 AcmeProbe/1.0 uptime check"), Is.True);
    }
}
