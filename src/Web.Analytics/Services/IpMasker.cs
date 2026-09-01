using System.Net;
using System.Net.Sockets;

namespace Regira.Web.Analytics.Services;

public static class IpMasker
{
    /// <summary>
    /// Truncates an address to /24 (IPv4) or /48 (IPv6). What remains is enough to tell networks apart and to
    /// keep the country/region meaningful, but no longer points at a household or a single connection.
    /// </summary>
    public static string? Mask(IPAddress? ip)
    {
        if (ip == null)
            return null;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();
        var keep = ip.AddressFamily == AddressFamily.InterNetwork ? 3 : 6;

        for (var i = keep; i < bytes.Length; i++)
            bytes[i] = 0;

        return new IPAddress(bytes).ToString();
    }

    /// <summary>Normalises IPv4-mapped IPv6 addresses (what Kestrel reports on dual-stack sockets) to plain IPv4.</summary>
    public static IPAddress? Normalize(IPAddress? ip) =>
        ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
}