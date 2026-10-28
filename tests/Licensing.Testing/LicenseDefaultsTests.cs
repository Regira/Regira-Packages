using Regira.Licensing.Models;
using Regira.Licensing.Utilities;

namespace Licensing.Testing;

[TestFixture]
public class LicenseDefaultsTests
{
    // --- Free-tier constants ---
    // The published free tier, as literals: compared with the constants they are built from, a changed limit would pass.

    [Test]
    public void EntityFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.EntityFreeLimits["entities.simple"], Is.EqualTo(5));
        Assert.That(LicenseDefaults.EntityFreeLimits["entities.complex"], Is.EqualTo(2));
    }

    [Test]
    public void ServiceFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.permit"], Is.EqualTo(5));
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.window"], Is.EqualTo(60));
        Assert.That(LicenseDefaults.ServiceFreeLimits["services.ratelimit.queue"], Is.EqualTo(0));
    }

    [Test]
    public void McpFreeLimits_HoldExpectedValues()
    {
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.permit"], Is.EqualTo(30));
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.window"], Is.EqualTo(60));
        Assert.That(LicenseDefaults.McpFreeLimits["mcp.ratelimit.queue"], Is.EqualTo(0));
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
