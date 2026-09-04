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

    [TestCase(32, "203.0.113.45")]
    [TestCase(28, "203.0.113.32")]
    [TestCase(16, "203.0.0.0")]
    [TestCase(0, "0.0.0.0")]
    [TestCase(99, "203.0.113.0", TestName = "Mask_Ipv4_OutOfRange_FallsBackToDefault_NotToFull")]
    [TestCase(-1, "203.0.113.0")]
    public void Mask_Ipv4_HonoursThePrefixLength(int prefixLength, string expected)
        => Assert.That(IpMasker.Mask(IPAddress.Parse("203.0.113.45"), ipv4PrefixLength: prefixLength), Is.EqualTo(expected));

    [TestCase(64, "2001:db8:85a3:8d3::")]
    [TestCase(32, "2001:db8::")]
    [TestCase(200, "2001:db8:85a3::")]
    public void Mask_Ipv6_HonoursThePrefixLength(int prefixLength, string expected)
        => Assert.That(IpMasker.Mask(IPAddress.Parse("2001:db8:85a3:8d3:1319:8a2e:370:7348"), ipv6PrefixLength: prefixLength),
            Is.EqualTo(expected));

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