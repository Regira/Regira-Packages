using Regira.Licensing.Models;
using Regira.Licensing.Utilities;

namespace Licensing.Testing;

[TestFixture]
public class LicenseDefaultsTests
{
    // --- Free-tier constants ---

    [Test]
    public void EntityFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.EntityFreeLimits["entities.simple"], Is.EqualTo(LicenseDefaults.Free.Entities.Simple));
        Assert.That(LicenseDefaults.EntityFreeLimits["entities.complex"], Is.EqualTo(LicenseDefaults.Free.Entities.Complex));
    }

    [Test]
    public void ServiceFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.permit"], Is.EqualTo(LicenseDefaults.Free.Services.Permit));
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.window"], Is.EqualTo(LicenseDefaults.Free.Services.Window));
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.queue"], Is.EqualTo(LicenseDefaults.Free.Services.Queue));
    }

    [Test]
    public void McpFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.permit"], Is.EqualTo(LicenseDefaults.Free.Mcp.Permit));
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.window"], Is.EqualTo(LicenseDefaults.Free.Mcp.Window));
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.queue"], Is.EqualTo(LicenseDefaults.Free.Mcp.Queue));
    }

    // --- CreateFree covers all products including MCP ---

    [Test]
    public void CreateFree_IncludesAllProductsWithFreeLimits()
    {
        var license = LicenseUtility.CreateFree();

        Assert.That(license.IsPaid, Is.False);
        Assert.That(license.Products, Does.Contain(LicenseDefaults.Products.Entities));
        Assert.That(license.Products, Does.Contain(LicenseDefaults.Products.Services));
        Assert.That(license.Products, Does.Contain(LicenseDefaults.Products.Mcp));

        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["entities.simple"], Is.EqualTo(LicenseDefaults.EntityFreeLimits["entities.simple"]));
        Assert.That(license.Limits["services.ratelimit.permit"], Is.EqualTo(LicenseDefaults.ServiceFreeLimits["services.ratelimit.permit"]));
        Assert.That(license.Limits["mcp.ratelimit.permit"], Is.EqualTo(LicenseDefaults.McpFreeLimits["mcp.ratelimit.permit"]));
    }
}
