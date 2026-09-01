using System.Net;
using Regira.Web.Analytics.Services;

namespace Web.Analytics.Testing;

[TestFixture]
public class IpMaskerTests
{
    [Test]
    public void Mask_Ipv4_TruncatesTo24()
        => Assert.That(IpMasker.Mask(IPAddress.Parse("203.0.113.45")), Is.EqualTo("203.0.113.0"));

    [Test]
    public void Mask_Ipv6_TruncatesTo48()
        => Assert.That(IpMasker.Mask(IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348")),
            Is.EqualTo("2001:db8:85a3::"));

    [Test]
    public void Mask_Ipv4MappedIpv6_UnwrapsToIpv4()
        => Assert.That(IpMasker.Mask(IPAddress.Parse("::ffff:203.0.113.45")), Is.EqualTo("203.0.113.0"));

    [Test]
    public void Mask_Null_ReturnsNull()
        => Assert.That(IpMasker.Mask(null), Is.Null);

    [Test]
    public void Normalize_Ipv4MappedIpv6_ReturnsPlainIpv4()
        => Assert.That(IpMasker.Normalize(IPAddress.Parse("::ffff:203.0.113.45")),
            Is.EqualTo(IPAddress.Parse("203.0.113.45")));

    [Test]
    public void Normalize_PlainAddress_IsUntouched()
    {
        var ip = IPAddress.Parse("203.0.113.45");
        Assert.That(IpMasker.Normalize(ip), Is.SameAs(ip));
    }
}