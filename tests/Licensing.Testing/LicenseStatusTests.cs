using Regira.Licensing.Models;
using Regira.Licensing.Services;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using License = Regira.Licensing.Models.License;

namespace Licensing.Testing;

/// <summary>
/// <see cref="LicenseValidator.GetStatus"/> is the one place the license states are decided; the hosted APIs
/// and the MCP server only render what it returns. Every state is pinned here on signed keys.
/// </summary>
[TestFixture]
public class LicenseStatusTests
{
    private const string Product = "regira.services";
    private static readonly DateTimeOffset Now = new(2026, 9, 2, 12, 0, 0, TimeSpan.Zero);

    private RSA _rsa = null!;

    [SetUp]
    public void Setup()
    {
        _rsa = RSA.Create(2048);
        LicenseValidator.TestPublicKey = _rsa;
    }

    [TearDown]
    public void Teardown()
    {
        LicenseValidator.TestPublicKey = null;
        _rsa.Dispose();
    }

    [Test]
    public void Missing_Key_Is_Not_Accepted()
    {
        var status = LicenseValidator.GetStatus(null, Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Missing));
        Assert.That(status.Accepted, Is.False);
        Assert.That(status.ProductCode, Is.EqualTo(Product));
        Assert.That(status.Message, Does.Contain("No license key"));
    }

    [Test]
    public void Unreadable_Key_Is_Invalid_And_Not_Accepted()
    {
        var status = LicenseValidator.GetStatus("not-a-key", Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Invalid));
        Assert.That(status.Accepted, Is.False);
        Assert.That(status.CustomerId, Is.Null);
    }

    [Test]
    public void Key_Signed_By_Someone_Else_Is_Invalid()
    {
        using var other = RSA.Create(2048);
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddYears(1)), other), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Invalid));
    }

    [Test]
    public void Key_For_Another_Product_Is_Not_Accepted_But_Fully_Described()
    {
        var license = Paid(Now.AddYears(1));
        license.Products = ["regira.entities"];
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(license, _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.NotAccepted));
        Assert.That(status.Accepted, Is.False);
        Assert.That(status.CustomerId, Is.EqualTo("acme"));
        Assert.That(status.Products, Is.EquivalentTo(new[] { "regira.entities" }));
        Assert.That(status.Message, Does.Contain(Product));
    }

    [Test]
    public void Valid_Key_Reports_Dates_And_Days_Left()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddDays(100)), _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Valid));
        Assert.That(status.Accepted, Is.True);
        Assert.That(status.DaysUntilExpiry, Is.EqualTo(100));
        Assert.That(status.ExpiresAt, Is.EqualTo(Now.AddDays(100)));
        Assert.That(status.Tier, Is.EqualTo("paid"));
        Assert.That(status.Limits, Is.Null, "a commercial key without baked limits carries none");
        Assert.That(status.Message, Does.Contain("valid until 2026-12-11"));
    }

    [Test]
    public void Perpetual_Key_Has_No_Expiry()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(null), _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Valid));
        Assert.That(status.ExpiresAt, Is.Null);
        Assert.That(status.DaysUntilExpiry, Is.Null);
        Assert.That(status.Message, Does.Contain("never expires"));
    }

    [Test]
    public void Key_Inside_Reminder_Period_Is_Expiring_Soon()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddDays(5)), _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.ExpiringSoon));
        Assert.That(status.Accepted, Is.True);
        Assert.That(status.Message, Does.Contain("expires in 5 days").And.Contain("Renew now"));
    }

    [Test]
    public void Key_Expiring_In_One_Day_Uses_Singular()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddHours(20)), _rsa), Product, now: Now);
        Assert.That(status.DaysUntilExpiry, Is.EqualTo(1));
        Assert.That(status.Message, Does.Contain("expires in 1 day,"));
    }

    [Test]
    public void Key_Just_Past_Expiry_Is_Still_Accepted()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddDays(-3)), _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.ExpiredInGrace));
        Assert.That(status.Accepted, Is.True);
        Assert.That(status.DaysUntilExpiry, Is.EqualTo(-3));
        Assert.That(status.Message, Does.Contain("expired on 2026-08-30").And.Contain("grace period").And.Contain("Renew now"));
        Assert.That(status.Message, Does.Not.Contain("14"), "the grace length is not part of the contract");
    }

    [Test]
    public void Key_Past_The_Grace_Period_Is_Refused()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now - LicenseValidator.ExpiryGracePeriod - TimeSpan.FromDays(1)), _rsa), Product, now: Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.Expired));
        Assert.That(status.Accepted, Is.False);
        Assert.That(status.Message, Does.Contain("no longer accepted"));
    }

    [Test]
    public void Messages_Describe_The_Key_Not_The_Host()
    {
        // What happens to a rejected key differs per product (free tier on the hosted services, a refused start
        // in-process), so the shared message must not promise either.
        var otherProduct = Paid(Now.AddYears(1));
        otherProduct.Products = ["regira.entities"];
        var keys = new[] { null, "not-a-key", LicenseSigner.Sign(Paid(Now.AddDays(-60)), _rsa), LicenseSigner.Sign(otherProduct, _rsa), LicenseSigner.Sign(Paid(Now.AddYears(1)), _rsa) };
        foreach (var key in keys)
        {
            var status = LicenseValidator.GetStatus(key, Product, 7, Now);
            Assert.That(status.Accepted, Is.False, "every key in this set is one the product rejects");
            Assert.That(status.Message, Does.Not.Contain("free tier").And.Not.Contain("ignored"), status.State.ToString());
            Assert.That(status.Applied, Is.Null, "the package never fills in the host's line");
        }
    }

    [Test]
    public void Status_Agrees_With_Validate()
    {
        // Accepted must mean exactly "Validate does not throw", or the two views of one key could disagree.
        // Both sides read the same frozen clock: with a live one this test would start failing the day the
        // 3-days-expired key leaves the grace period in real time.
        foreach (var expiresAt in new DateTimeOffset?[] { null, Now.AddYears(1), Now.AddDays(3), Now.AddDays(-3), Now.AddDays(-60) })
        foreach (var requiredMajorVersion in new int?[] { null, 6, 7 })
        {
            var key = LicenseSigner.Sign(Paid(expiresAt), _rsa); // Version = "6"
            var accepted = LicenseValidator.GetStatus(key, Product, requiredMajorVersion, Now).Accepted;
            var validates = true;
            try { LicenseValidator.Validate(key, Product, requiredMajorVersion, Now); } catch (LicenseException) { validates = false; }
            Assert.That(accepted, Is.EqualTo(validates), $"expiry {expiresAt}, required version {requiredMajorVersion}");
        }
    }

    [Test]
    public void Key_For_Another_Major_Version_Is_A_Version_Mismatch()
    {
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddYears(1)), _rsa), Product, 7, Now);
        Assert.That(status.State, Is.EqualTo(LicenseState.VersionMismatch));
        Assert.That(status.Accepted, Is.False);
        Assert.That(status.Message, Does.Contain("version 6.x").And.Contain("version 7.x"));
    }

    [Test]
    public void Matching_Or_Unstated_Major_Version_Is_Accepted()
    {
        var key = LicenseSigner.Sign(Paid(Now.AddYears(1)), _rsa);
        Assert.That(LicenseValidator.GetStatus(key, Product, 6, Now).State, Is.EqualTo(LicenseState.Valid));
        Assert.That(LicenseValidator.GetStatus(key, Product, now: Now).State, Is.EqualTo(LicenseState.Valid));
    }

    [Test]
    public void Baked_Limits_Are_Copied()
    {
        var license = Paid(Now.AddYears(1));
        license.Limits = new Dictionary<string, int> { ["services.ratelimit.permit"] = 600 };
        var status = LicenseValidator.GetStatus(LicenseSigner.Sign(license, _rsa), Product, now: Now);
        Assert.That(status.Limits, Is.Not.Null);
        Assert.That(status.Limits!["services.ratelimit.permit"], Is.EqualTo(600));
    }

    [Test]
    public void Status_Round_Trips_As_Json_With_String_Enums()
    {
        // The hosted APIs serialize enums as strings and drop nulls; the clients read with the same converter.
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() },
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var original = LicenseValidator.GetStatus(LicenseSigner.Sign(Paid(Now.AddDays(5)), _rsa), Product, now: Now);
        var json = JsonSerializer.Serialize(original, options);
        Assert.That(json, Does.Contain("\"ExpiringSoon\""), "enums travel as their names, like the controllers' StringEnumConverter output");

        original.Applied = "unlimited";
        json = JsonSerializer.Serialize(original, options);

        var copy = JsonSerializer.Deserialize<LicenseStatus>(json, options)!;
        Assert.That(copy.Applied, Is.EqualTo("unlimited"), "the host's line travels with the rest");
        Assert.That(copy.State, Is.EqualTo(original.State));
        Assert.That(copy.Accepted, Is.EqualTo(original.Accepted));
        Assert.That(copy.CustomerId, Is.EqualTo(original.CustomerId));
        Assert.That(copy.ExpiresAt, Is.EqualTo(original.ExpiresAt));
        Assert.That(copy.DaysUntilExpiry, Is.EqualTo(original.DaysUntilExpiry));
        Assert.That(copy.Message, Is.EqualTo(original.Message));
    }

    private static License Paid(DateTimeOffset? expiresAt) => new()
    {
        CustomerId = "acme",
        Tier = "paid",
        Products = [Product, "regira.mcp"],
        Version = "6",
        IssuedAtUnix = Now.AddMonths(-6).ToUnixTimeSeconds(),
        ExpiresAtUnix = expiresAt?.ToUnixTimeSeconds()
    };
}
