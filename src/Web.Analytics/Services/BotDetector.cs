using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regira.Web.Analytics.Config;

namespace Regira.Web.Analytics.Services;

/// <summary>
/// Cheap user-agent substring matching; built-in markers merge with Analytics:BotDetection and reload
/// when that configuration changes.
/// </summary>
public sealed class BotDetector : IDisposable
{
    /// <summary>Markers as the matcher wants them: merged, lower-cased and de-duplicated once per reload.</summary>
    private sealed class MarkerSet
    {
        public MarkerSet(BotDetectionConfig config)
        {
            MinUserAgentLength = config.MinUserAgentLength;
            Markers = RuleList.Normalize(config.IncludeDefaultMarkers
                ? [.. BotDetectorDefaults.Markers, .. config.Markers]
                : config.Markers);
            Exceptions = RuleList.Normalize(config.IncludeDefaultMarkers
                ? [.. BotDetectorDefaults.Exceptions, .. config.Exceptions]
                : config.Exceptions);
        }

        public int MinUserAgentLength { get; }
        public string[] Markers { get; }
        public string[] Exceptions { get; }

        public int Count => Markers.Length + Exceptions.Length;
        public bool IsEmpty => Markers.Length == 0 && MinUserAgentLength <= 0;
    }

    private readonly IDisposable? _reloadSubscription;
    private volatile MarkerSet _markers;

    public BotDetector(IOptionsMonitor<BotDetectionConfig> config, ILogger<BotDetector> logger)
    {
        _markers = new MarkerSet(config.CurrentValue);

        if (_markers.IsEmpty)
            logger.LogError("Analytics: no bot markers configured, every visit will count as human");
        else
            logger.LogInformation("Analytics: loaded {Count} bot markers", _markers.Count);

        _reloadSubscription = config.OnChange(updated =>
        {
            var reloaded = new MarkerSet(updated);
            _markers = reloaded;
            logger.LogInformation("Analytics: reloaded {Count} bot markers", reloaded.Count);
        });
    }

    public bool IsBot(string? userAgent)
    {
        var markers = _markers;

        if (string.IsNullOrWhiteSpace(userAgent))
            return markers.MinUserAgentLength > 0;

        if (userAgent.Length < markers.MinUserAgentLength)
            return true;

        var ua = userAgent.ToLowerInvariant();

        // Checked first: an exception exists precisely because the agent also matches a marker.
        foreach (var exception in markers.Exceptions)
        {
            if (ua.Contains(exception, StringComparison.Ordinal))
                return false;
        }

        foreach (var marker in markers.Markers)
        {
            if (ua.Contains(marker, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public void Dispose() => _reloadSubscription?.Dispose();
}