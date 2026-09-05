using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regira.Web.Analytics.Config;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Cheap substring matching over two independent questions: does the agent claim to be a person
/// (<see cref="IsBot"/>), and could a person have asked for this target (<see cref="IsProbe"/>). The
/// second is what survives a scanner copying a real browser's user agent. Built-in rules merge with
/// Analytics:BotDetection and reload when that configuration changes.
/// </summary>
public sealed class BotDetector : IDisposable
{
    /// <summary>Rules as the matcher wants them: merged, lower-cased and de-duplicated once per reload.</summary>
    private sealed class RuleSet
    {
        public RuleSet(BotDetectionConfig config)
        {
            MinUserAgentLength = config.MinUserAgentLength;
            Markers = Merge(config.Markers, BotDetectorDefaults.Markers, config.IncludeDefaultMarkers);
            Exceptions = Merge(config.Exceptions, BotDetectorDefaults.Exceptions, config.IncludeDefaultMarkers);
            BrowserTokens = config.RequireBrowserToken
                ? Merge(config.BrowserTokens, BotDetectorDefaults.BrowserTokens, config.IncludeDefaultMarkers)
                : [];
            DetectProbes = config.DetectProbeRequests;
            ProbePaths = DetectProbes
                ? Merge(config.ProbePaths, BotDetectorDefaults.ProbePaths, config.IncludeDefaultMarkers)
                : [];
        }

        private static string[] Merge(string[] configured, string[] defaults, bool includeDefaults)
            => RuleList.Normalize(includeDefaults ? [.. defaults, .. configured] : configured);

        public int MinUserAgentLength { get; }
        public string[] Markers { get; }
        public string[] Exceptions { get; }

        /// <summary>Empty when the check is off, or when opting out of the defaults left nothing to match.</summary>
        public string[] BrowserTokens { get; }

        public string[] ProbePaths { get; }

        /// <summary>Gates the shape checks too, which hold no list of their own.</summary>
        public bool DetectProbes { get; }

        public int Count => Markers.Length + Exceptions.Length + BrowserTokens.Length + ProbePaths.Length;
        public bool IsEmpty => Markers.Length == 0 && BrowserTokens.Length == 0
                               && MinUserAgentLength <= 0 && !DetectProbes;
    }

    private readonly IDisposable? _reloadSubscription;
    private volatile RuleSet _rules;

    public BotDetector(IOptionsMonitor<BotDetectionConfig> config, ILogger<BotDetector> logger)
    {
        _rules = new RuleSet(config.CurrentValue);

        if (_rules.IsEmpty)
            logger.LogError("Analytics: no bot detection rules configured, every visit will count as human");
        else
            logger.LogInformation("Analytics: loaded {Count} bot detection rules", _rules.Count);

        _reloadSubscription = config.OnChange(updated =>
        {
            var reloaded = new RuleSet(updated);
            _rules = reloaded;
            logger.LogInformation("Analytics: reloaded {Count} bot detection rules", reloaded.Count);
        });
    }

    public bool IsBot(string? userAgent)
    {
        var rules = _rules;

        if (string.IsNullOrWhiteSpace(userAgent))
            return rules.MinUserAgentLength > 0;

        if (userAgent.Length < rules.MinUserAgentLength)
            return true;

        var ua = userAgent.ToLowerInvariant();

        if (ContainsMarker(ua, rules))
            return true;

        // Last, because it is the weakest claim: nothing here says "bot", only "not a browser".
        return rules.BrowserTokens.Length > 0 && !ContainsAny(ua, rules.BrowserTokens);
    }

    /// <summary>
    /// Whether the request asks for something no visitor of this site could have navigated to. Reads the
    /// target alone, so it is the one signal a scanner cannot dress up by copying a browser's user agent.
    /// </summary>
    /// <param name="path">Request path; the query is matched too, because an attack often rides in it.</param>
    /// <param name="queryString">Raw query string, leading "?" included, or null.</param>
    public bool IsProbe(string? path, string? queryString = null)
    {
        var rules = _rules;

        if (!rules.DetectProbes || string.IsNullOrEmpty(path))
            return false;

        var lowerPath = path.ToLowerInvariant();
        var target = string.IsNullOrEmpty(queryString) ? lowerPath : lowerPath + queryString.ToLowerInvariant();

        // A link a visitor followed never climbs out of the site root, however it is spelled.
        if (ClimbsOutOfRoot(target))
            return true;

        // The dot check reads the path only: a query legitimately carries dots, a path segment does not.
        return ContainsAny(target, rules.ProbePaths) || HasDotSegment(lowerPath);
    }

    /// <summary>
    /// A "../" anywhere in the target, in the spellings a scanner reaches for: the dots and the separator
    /// each arrive percent-encoded as often as not. The separator is what makes it an attack — without it,
    /// a search box carries "2020..2024" and "wait.. what" past this check as the visitor traffic it is.
    /// </summary>
    private static bool ClimbsOutOfRoot(string target)
    {
        var decoded = target.Contains('%', StringComparison.Ordinal)
            ? target.Replace("%2e", ".", StringComparison.Ordinal)
                .Replace("%2f", "/", StringComparison.Ordinal)
                .Replace("%5c", "\\", StringComparison.Ordinal)
            : target;

        return decoded.Contains("../", StringComparison.Ordinal)
               || decoded.Contains("..\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// A dot-directory or dot-file anywhere in the path — /.git/config, /home/ubuntu/.aws/credentials.
    /// Nothing links to one; /.well-known is the single addressable exception.
    /// </summary>
    private static bool HasDotSegment(string path)
    {
        var index = path.IndexOf("/.", StringComparison.Ordinal);

        while (index >= 0)
        {
            if (!path.AsSpan(index).StartsWith("/.well-known", StringComparison.Ordinal))
                return true;

            index = path.IndexOf("/.", index + 2, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// An exception cancels only the marker it overlaps with — cutting it out of the copy being scanned
    /// leaves the rest of the agent, so a Cubot phone running a crawler is still a bot.
    /// </summary>
    private static bool ContainsMarker(string ua, RuleSet rules)
    {
        var scanned = ua;

        foreach (var exception in rules.Exceptions)
        {
            if (scanned.Contains(exception, StringComparison.Ordinal))
                scanned = scanned.Replace(exception, " ", StringComparison.Ordinal);
        }

        return ContainsAny(scanned, rules.Markers);
    }

    private static bool ContainsAny(string ua, string[] needles)
    {
        foreach (var needle in needles)
        {
            if (ua.Contains(needle, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void Dispose() => _reloadSubscription?.Dispose();
}
