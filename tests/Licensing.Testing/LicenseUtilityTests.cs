using Regira.Licensing.Models;
using Regira.Licensing.Utilities;

namespace Licensing.Testing;

[TestFixture]
public class LicenseUtilityTests
{
    // Mirrors tools/LicenseKeyGen/appsettings.json (product -> tier -> limitKey -> value).
    private static readonly Dictionary<string, Dictionary<string, Dictionary<string, int>>> LimitsConfig = new()
    {
        ["regira.services"] = new()
        {
            ["paid"] = new()
            {
                ["services.ratelimit.permit"] = 200,
                ["services.ratelimit.window"] = 60,
                ["services.ratelimit.queue"] = 20
            }
        },
        ["regira.mcp"] = new()
        {
            ["paid"] = new()
            {
                ["mcp.ratelimit.permit"] = 200,
                ["mcp.ratelimit.window"] = 60,
                ["mcp.ratelimit.queue"] = 20
            }
        }
    };

    private static License Bake(string? tier, params string[] products) =>
        LicenseUtility.ApplyLimits(new License { Tier = tier, Products = products }, LimitsConfig);

    // --- Free tier: entities ---

    [Test]
    public void Free_Entities_SetsEntityLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Entities);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["entities.simple"], Is.EqualTo(LicenseDefaults.EntityFreeLimits["entities.simple"]));
        Assert.That(license.Limits["entities.complex"], Is.EqualTo(LicenseDefaults.EntityFreeLimits["entities.complex"]));
    }

    [Test]
    public void Free_Entities_DoesNotSetServiceLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Entities);
        Assert.That(license.Limits!.ContainsKey("services.ratelimit.permit"), Is.False);
    }

    // --- Free tier: services ---

    [Test]
    public void Free_Services_SetsFreeLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Services);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["services.ratelimit.permit"], Is.EqualTo(LicenseDefaults.ServiceFreeLimits["services.ratelimit.permit"]));
        Assert.That(license.Limits["services.ratelimit.window"], Is.EqualTo(LicenseDefaults.ServiceFreeLimits["services.ratelimit.window"]));
    }

    [Test]
    public void Free_Services_DoesNotSetEntityLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Services);
        Assert.That(license.Limits!.ContainsKey("entities.simple"), Is.False);
    }

    // --- Free tier: mcp ---

    [Test]
    public void Free_Mcp_SetsFreeLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Mcp);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["mcp.ratelimit.permit"], Is.EqualTo(LicenseDefaults.McpFreeLimits["mcp.ratelimit.permit"]));
        Assert.That(license.Limits["mcp.ratelimit.window"], Is.EqualTo(LicenseDefaults.McpFreeLimits["mcp.ratelimit.window"]));
        Assert.That(license.Limits["mcp.ratelimit.queue"], Is.EqualTo(LicenseDefaults.McpFreeLimits["mcp.ratelimit.queue"]));
    }

    // --- Free tier: all products ---

    [Test]
    public void Free_AllProducts_SetsAllLimits()
    {
        var license = Bake("free", LicenseDefaults.Products.Entities, LicenseDefaults.Products.Services, LicenseDefaults.Products.Mcp);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!.ContainsKey("entities.simple"), Is.True);
        Assert.That(license.Limits.ContainsKey("services.ratelimit.permit"), Is.True);
        Assert.That(license.Limits.ContainsKey("mcp.ratelimit.permit"), Is.True);
    }

    // --- Paid tier: from config ---

    [Test]
    public void Paid_Services_SetsConfiguredLimits()
    {
        var license = Bake("paid", LicenseDefaults.Products.Services);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["services.ratelimit.permit"], Is.EqualTo(200));
        Assert.That(license.Limits["services.ratelimit.queue"], Is.EqualTo(20));
    }

    [Test]
    public void Paid_Mcp_SetsConfiguredLimits()
    {
        var license = Bake("paid", LicenseDefaults.Products.Mcp);
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["mcp.ratelimit.permit"], Is.EqualTo(200));
        Assert.That(license.Limits["mcp.ratelimit.queue"], Is.EqualTo(20));
    }

    [Test]
    public void Paid_Entities_HasNoConfiguredLimits()
    {
        // Entities are not paid-rate-limited; no config entry => no limits.
        var license = Bake("paid", LicenseDefaults.Products.Entities);
        Assert.That(license.Limits, Is.Null);
    }

    // --- Commercial / unmapped tiers: no limits ---

    [Test]
    public void Commercial_NullTier_SetsNoLimits()
    {
        var license = Bake(null, LicenseDefaults.Products.Services);
        Assert.That(license.Limits, Is.Null);
    }

    [Test]
    public void Commercial_Mcp_SetsNoLimits()
    {
        var license = Bake(null, LicenseDefaults.Products.Mcp);
        Assert.That(license.Limits, Is.Null);
    }
}
