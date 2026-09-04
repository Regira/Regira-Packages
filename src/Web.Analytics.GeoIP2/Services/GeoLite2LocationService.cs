using System.Net;
using System.Net.Sockets;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Web.Analytics.GeoIP2.Config;
using Regira.Web.Analytics.GeoIP2.Models;

namespace Regira.Web.Analytics.GeoIP2.Services;

/// <summary>
/// Country/city from a local MaxMind database — local on purpose, so visitor addresses never leave
/// the server. Disabled (never throwing) when no usable .mmdb is configured.
/// </summary>
public sealed class GeoLite2LocationService : IGeoLocationService, IDisposable
{
    private readonly DatabaseReader? _reader;
    private readonly bool _hasCityData;
    private readonly ILogger<GeoLite2LocationService> _logger;

    public GeoLite2LocationService(GeoIP2Config config, IHostEnvironment environment, ILogger<GeoLite2LocationService> logger)
    {
        _logger = logger;

        if (string.IsNullOrWhiteSpace(config.DatabasePath))
        {
            logger.LogInformation("Analytics: no GeoIP2 DatabasePath configured, geo lookup disabled");
            return;
        }

        var path = ResolvePath(config.DatabasePath, environment.ContentRootPath);
        if (!File.Exists(path))
        {
            logger.LogWarning("Analytics: no GeoIP2 .mmdb found at {Path}, geo lookup disabled", path);
            return;
        }

        try
        {
            _reader = new DatabaseReader(path);
            // A Country database has no city section, and asking it for one throws. Enterprise has one.
            var type = _reader.Metadata.DatabaseType;
            _hasCityData = type.Contains("City", StringComparison.OrdinalIgnoreCase)
                || type.Contains("Enterprise", StringComparison.OrdinalIgnoreCase);
            logger.LogInformation("Analytics: geo lookup enabled using {Path} ({DatabaseType})",
                path, _reader.Metadata.DatabaseType);
        }
        catch (Exception ex) when (
            ex is IOException
            or UnauthorizedAccessException
            or System.Security.SecurityException
            or MaxMind.Db.InvalidDatabaseException)
        {
            logger.LogError(ex, "Analytics: failed to open GeoIP2 database at {Path}, geo lookup disabled", path);
        }
    }

    public GeoLocation? Lookup(IPAddress? ip)
    {
        if (_reader == null || ip == null || IsLocal(ip))
            return null;

        try
        {
            if (_hasCityData)
            {
                if (_reader.TryCity(ip, out var city) && city != null)
                    return new GeoLocation(city.Country.IsoCode, city.Country.Name, city.City?.Name);
            }
            else if (_reader.TryCountry(ip, out var country) && country != null)
            {
                return new GeoLocation(country.Country.IsoCode, country.Country.Name, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Analytics: geo lookup failed");
        }

        return null;
    }

    /// <summary>Content root first (development), then the deployed base directory; a directory picks City over Country.</summary>
    internal static string ResolvePath(string configured, string contentRoot)
    {
        var candidates = Path.IsPathRooted(configured)
            ? [configured]
            : new[]
            {
                Path.GetFullPath(Path.Combine(contentRoot, configured)),
                Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, configured))
            };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return candidate;

            if (Directory.Exists(candidate) && PickDatabase(candidate) is { } fromDirectory)
                return fromDirectory;
        }

        return candidates[0];
    }

    private static string? PickDatabase(string directory)
    {
        var files = Directory.GetFiles(directory, "*.mmdb");
        return files.FirstOrDefault(f => Path.GetFileName(f).Contains("City", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault(f => Path.GetFileName(f).Contains("Country", StringComparison.OrdinalIgnoreCase))
            ?? files.FirstOrDefault();
    }

    /// <summary>Loopback, link-local and private ranges have no geography.</summary>
    internal static bool IsLocal(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6UniqueLocal)
            return true;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();
        if (ip.AddressFamily != AddressFamily.InterNetwork)
            return false;

        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 169 && b[1] == 254);
    }

    public void Dispose() => _reader?.Dispose();
}