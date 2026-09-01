using System.Net;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Web.Analytics.GeoIP2.Config;
using Regira.Web.Analytics.GeoIP2.Services;

namespace Web.Analytics.GeoIP2.Testing;

[TestFixture]
public class GeoLite2LocationServiceTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "regira-geoip2-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    private GeoLite2LocationService Create(string? databasePath)
        => new(new GeoIP2Config { DatabasePath = databasePath }, new FakeEnvironment(_root),
            NullLogger<GeoLite2LocationService>.Instance);

    [Test]
    public void NoPath_IsDisabled()
    {
        using var service = Create(null);
        Assert.That(service.Lookup(IPAddress.Parse("203.0.113.45")), Is.Null);
    }

    [Test]
    public void MissingFile_IsDisabled()
    {
        using var service = Create("does-not-exist.mmdb");
        Assert.That(service.Lookup(IPAddress.Parse("203.0.113.45")), Is.Null);
    }

    [Test]
    public void UnreadableFile_IsDisabled_WithoutThrowing()
    {
        File.WriteAllText(Path.Combine(_root, "GeoLite2-City.mmdb"), "not a database");

        GeoLite2LocationService service = null!;
        Assert.DoesNotThrow(() => service = Create("GeoLite2-City.mmdb"));
        using (service)
            Assert.That(service.Lookup(IPAddress.Parse("203.0.113.45")), Is.Null);
    }

    [Test]
    public void ResolvePath_Directory_PrefersCityOverCountry()
    {
        var dir = Path.Combine(_root, "geo");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "GeoLite2-Country.mmdb"), "");
        File.WriteAllText(Path.Combine(dir, "GeoLite2-City.mmdb"), "");

        var resolved = GeoLite2LocationService.ResolvePath("geo", _root);

        Assert.That(Path.GetFileName(resolved), Is.EqualTo("GeoLite2-City.mmdb"));
    }

    [Test]
    public void ResolvePath_RelativeFile_ResolvesAgainstContentRoot()
    {
        File.WriteAllText(Path.Combine(_root, "db.mmdb"), "");

        Assert.That(GeoLite2LocationService.ResolvePath("db.mmdb", _root),
            Is.EqualTo(Path.GetFullPath(Path.Combine(_root, "db.mmdb"))));
    }

    [TestCase("127.0.0.1", true)]
    [TestCase("::1", true)]
    [TestCase("10.1.2.3", true)]
    [TestCase("172.16.0.1", true)]
    [TestCase("172.32.0.1", false)]
    [TestCase("192.168.1.1", true)]
    [TestCase("169.254.1.1", true)]
    [TestCase("::ffff:10.0.0.1", true)]
    [TestCase("fe80::1", true)]
    [TestCase("fd00::1", true)]
    [TestCase("8.8.8.8", false)]
    [TestCase("2001:4860:4860::8888", false)]
    public void IsLocal(string address, bool expected)
        => Assert.That(GeoLite2LocationService.IsLocal(IPAddress.Parse(address)), Is.EqualTo(expected));

    private class FakeEnvironment(string contentRoot) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "GeoIP2.Testing";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}