using System.Net;
using System.Net.Sockets;

namespace Regira.Web.Analytics.Services;

public static class IpMasker
{
    public const int DefaultIpv4PrefixLength = 24;
    public const int DefaultIpv6PrefixLength = 48;

    /// <summary>
    /// Keeps only the leading prefix bits (/24 IPv4, /48 IPv6 by default): enough to tell networks apart
    /// and keep the region meaningful, no longer pointing at a household or a single connection.
    /// </summary>
    public static string? Mask(IPAddress? ip,
        int ipv4PrefixLength = DefaultIpv4PrefixLength, int ipv6PrefixLength = DefaultIpv6PrefixLength)
    {
        if (ip == null)
            return null;

        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        var bytes = ip.GetAddressBytes();
        var isIpv4 = ip.AddressFamily == AddressFamily.InterNetwork;
        var prefix = isIpv4 ? ipv4PrefixLength : ipv6PrefixLength;

        // Out of range falls back to the default, not to "keep everything": a typo must not ship full addresses.
        if (prefix < 0 || prefix > bytes.Length * 8)
            prefix = isIpv4 ? DefaultIpv4PrefixLength : DefaultIpv6PrefixLength;

        for (var i = 0; i < bytes.Length; i++)
        {
            var bitsKept = Math.Clamp(prefix - i * 8, 0, 8);
            bytes[i] &= (byte)(0xFF << (8 - bitsKept));
        }

        return new IPAddress(bytes).ToString();
    }

    /// <summary>Normalises IPv4-mapped IPv6 addresses (what Kestrel reports on dual-stack sockets) to plain IPv4.</summary>
    public static IPAddress? Normalize(IPAddress? ip) =>
        ip is { IsIPv4MappedToIPv6: true } ? ip.MapToIPv4() : ip;
}