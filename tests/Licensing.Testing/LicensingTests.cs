using Microsoft.Extensions.DependencyInjection;
using Regira.Licensing.DependencyInjection;
using Regira.Licensing.Models;
using Regira.Licensing.Services;
using System.Security.Cryptography;
using System.Text.Json;
using License = Regira.Licensing.Models.License;

namespace Licensing.Testing;

[TestFixture]
public class LicensingTests
{
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

    // --- LicenseSigner ---

    [Test]
    public void Sign_ProducesTwoDotSeparatedBase64UrlSegments()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parts = key.Split('.');
        Assert.That(parts, Has.Length.EqualTo(2));
        Assert.That(parts[0], Is.Not.Empty);
        Assert.That(parts[1], Is.Not.Empty);
    }

    [Test]
    public void Sign_PayloadRoundTripsAsJson()
    {
        var license = ValidLicense();
        var key = LicenseSigner.Sign(license, _rsa);
        var payloadBytes = LicenseParser.Base64UrlDecode(key.Split('.')[0]);
        var decoded = JsonSerializer.Deserialize<License>(payloadBytes)!;

        Assert.That(decoded.CustomerId, Is.EqualTo(license.CustomerId));
        Assert.That(decoded.Products, Is.EquivalentTo(license.Products));
        Assert.That(decoded.ExpiresAtUnix, Is.EqualTo(license.ExpiresAtUnix));
    }

    [Test]
    public void Sign_LimitsRoundTripAsJson()
    {
        var license = ValidLicense();
        license.Limits = new Dictionary<string, int> { ["entities.simple"] = 5, ["entities.complex"] = 2 };
        var key = LicenseSigner.Sign(license, _rsa);
        var payloadBytes = LicenseParser.Base64UrlDecode(key.Split('.')[0]);
        var decoded = JsonSerializer.Deserialize<License>(payloadBytes)!;

        Assert.That(decoded.Limits, Is.Not.Null);
        Assert.That(decoded.Limits!["entities.simple"], Is.EqualTo(5));
        Assert.That(decoded.Limits["entities.complex"], Is.EqualTo(2));
    }

    [Test]
    public void Sign_DifferentPayloads_ProduceDifferentKeys()
    {
        var key1 = LicenseSigner.Sign(ValidLicense("customer-a"), _rsa);
        var key2 = LicenseSigner.Sign(ValidLicense("customer-b"), _rsa);
        Assert.That(key1, Is.Not.EqualTo(key2));
    }

    // --- LicenseValidator.Validate ---

    [Test]
    public void Validate_ValidKey_ReturnsLicense()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var license = LicenseValidator.Validate(key, "regira.entities");
        Assert.That(license, Is.Not.Null);
        Assert.That(license.Products, Contains.Item("regira.entities"));
    }

    [Test]
    public void Validate_LicenseWithLimits_ReturnsCorrectLimits()
    {
        var original = ValidLicense();
        original.Limits = new Dictionary<string, int> { ["entities.simple"] = 5, ["entities.complex"] = 2 };
        var key = LicenseSigner.Sign(original, _rsa);
        var license = LicenseValidator.Validate(key, "regira.entities");
        Assert.That(license.Limits, Is.Not.Null);
        Assert.That(license.Limits!["entities.simple"], Is.EqualTo(5));
        Assert.That(license.Limits["entities.complex"], Is.EqualTo(2));
    }

    [Test]
    public void Validate_NullKey_ThrowsLicenseException()
    {
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate((string?)null, "regira.entities"));
    }

    [Test]
    public void Validate_EmptyKey_ThrowsLicenseException()
    {
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate("  ", "regira.entities"));
    }

    [Test]
    public void Validate_WrongSegmentCount_ThrowsLicenseException()
    {
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate("one.two.three", "regira.entities"));
    }

    [Test]
    public void Validate_TamperedPayload_ThrowsLicenseException()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parts = key.Split('.');
        var payloadBytes = LicenseParser.Base64UrlDecode(parts[0]);
        payloadBytes[0] ^= 0xFF;
        var tampered = Convert.ToBase64String(payloadBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(tampered + "." + parts[1], "regira.entities"));
    }

    [Test]
    public void Validate_SignedWithDifferentKey_ThrowsLicenseException()
    {
        using var otherRsa = RSA.Create(2048);
        var key = LicenseSigner.Sign(ValidLicense(), otherRsa);
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(key, "regira.entities"));
    }

    [Test]
    public void Validate_ExpiredKey_ThrowsWithExpiredMessage()
    {
        var license = new License
        {
            CustomerId = "test",
            Products = ["regira.entities"],
            IssuedAtUnix = DateTimeOffset.UtcNow.AddYears(-2).ToUnixTimeSeconds(),
            ExpiresAtUnix = DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds()
        };
        var key = LicenseSigner.Sign(license, _rsa);
        var ex = Assert.Throws<LicenseException>(() => LicenseValidator.Validate(key, "regira.entities"));
        Assert.That(ex!.Message, Does.Contain("expired"));
    }

    [Test]
    public void Validate_KeyExpiredWithinGracePeriod_ReturnsLicense()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(LicenseValidator.ExpiryGracePeriod - TimeSpan.FromDays(1)), _rsa);
        var license = LicenseValidator.Validate(key, "regira.entities");
        Assert.That(license.Products, Contains.Item("regira.entities"));
    }

    [Test]
    public void Validate_KeyExpiredBeyondGracePeriod_ThrowsWithExpiredMessage()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(LicenseValidator.ExpiryGracePeriod + TimeSpan.FromDays(1)), _rsa);
        var ex = Assert.Throws<LicenseException>(() => LicenseValidator.Validate(key, "regira.entities"));
        Assert.That(ex!.Message, Does.Contain("expired"));
    }

    [Test]
    public void UseRegira_KeyExpiredWithinGracePeriod_WarnsOnConsoleError()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(TimeSpan.FromDays(1)), _rsa);
        var stderr = CaptureConsoleError(() => new ServiceCollection().UseRegira(key));
        Assert.That(stderr, Does.Contain("WARNING").And.Contain("expired"));
    }

    [Test]
    public void UseRegira_KeyExpiredBeyondGracePeriod_ReportsErrorOnConsoleError()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(LicenseValidator.ExpiryGracePeriod + TimeSpan.FromDays(1)), _rsa);
        var stderr = CaptureConsoleError(() => new ServiceCollection().UseRegira(key));
        Assert.That(stderr, Does.Contain("ERROR").And.Contain("no longer accepted"));
    }

    [Test]
    public void UseRegira_KeyExpiringWithinReminderPeriod_RemindsOnStandardOutput()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(-(LicenseValidator.RenewalReminderPeriod - TimeSpan.FromDays(1))), _rsa);
        var (stdout, stderr) = CaptureConsole(() => new ServiceCollection().UseRegira(key));
        Assert.That(stdout, Does.Contain("Reminder").And.Contain("expires in 13 day(s)"));
        Assert.That(stderr, Is.Empty, "a key that is still valid must not start the application with an error-level line");
    }

    [Test]
    public void UseRegira_KeyExpiringAfterReminderPeriod_WritesNoReminder()
    {
        var key = LicenseSigner.Sign(ExpiredLicense(-(LicenseValidator.RenewalReminderPeriod + TimeSpan.FromDays(1))), _rsa);
        var (stdout, stderr) = CaptureConsole(() => new ServiceCollection().UseRegira(key));
        Assert.That(stdout, Does.Not.Contain("expires in"));
        Assert.That(stderr, Is.Empty);
    }

    [Test]
    public void IsExpiringSoon_PerpetualLicense_ReturnsFalse()
    {
        Assert.That(LicenseValidator.IsExpiringSoon(new License { ExpiresAtUnix = null }, DateTimeOffset.UtcNow), Is.False);
    }

    [Test]
    public void UseRegira_ValidKey_WritesNothingToConsoleError()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var stderr = CaptureConsoleError(() => new ServiceCollection().UseRegira(key));
        Assert.That(stderr, Is.Empty);
    }

    [Test]
    public void Validate_WrongProduct_ThrowsLicenseException()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(key, "regira.services"));
    }

    [Test]
    public void Validate_PerpetualLicense_DoesNotThrow()
    {
        var license = new License
        {
            CustomerId = "test",
            Products = ["regira.entities"],
            IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
            ExpiresAtUnix = null
        };
        var key = LicenseSigner.Sign(license, _rsa);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities"));
    }

    [Test]
    public void Validate_ProductCodeCaseInsensitive_DoesNotThrow()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "REGIRA.ENTITIES"));
    }

    // --- Trial tier ---

    [Test]
    public void Validate_TrialLicense_DoesNotThrow()
    {
        var key = LicenseSigner.Sign(TrialLicense(), _rsa);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities"));
    }

    [Test]
    public void Validate_TrialLicense_CanBeUsedMultipleTimes()
    {
        var key = LicenseSigner.Sign(TrialLicense(), _rsa);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities"));
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities"));
    }

    [Test]
    public void Validate_TrialLicense_IsPaid_ReturnsTrue()
    {
        var key = LicenseSigner.Sign(TrialLicense(), _rsa);
        var license = LicenseValidator.Validate(key, "regira.entities");
        Assert.That(license.IsPaid, Is.True);
    }

    // --- LicenseParser.RawKey ---

    [Test]
    public void Parse_SetsRawKey()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var license = LicenseParser.Parse(key);
        Assert.That(license.RawKey, Is.EqualTo(key));
    }

    [Test]
    public void Validate_String_SetsRawKey()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var license = LicenseValidator.Validate(key, "regira.entities");
        Assert.That(license.RawKey, Is.EqualTo(key));
    }

    // --- License.IssuedAt / ExpiresAt ---

    [Test]
    public void IssuedAt_ComputedFromIssuedAtUnix()
    {
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var license = new License { IssuedAtUnix = unix };
        Assert.That(license.IssuedAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(unix)));
    }

    [Test]
    public void ExpiresAt_NullWhenExpiresAtUnixIsNull()
    {
        var license = new License { ExpiresAtUnix = null };
        Assert.That(license.ExpiresAt, Is.Null);
    }

    [Test]
    public void ExpiresAt_ComputedFromExpiresAtUnix()
    {
        var unix = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds();
        var license = new License { ExpiresAtUnix = unix };
        Assert.That(license.ExpiresAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(unix)));
    }

    // --- License.Developers ---

    [Test]
    public void Developers_DefaultsToOne()
    {
        var license = new License();
        Assert.That(license.Developers, Is.EqualTo(1));
    }

    [Test]
    public void Developers_RoundTripsThroughSignAndParse()
    {
        var license = ValidLicense();
        license.Developers = 5;
        var key = LicenseSigner.Sign(license, _rsa);
        var parsed = LicenseParser.Parse(key);
        Assert.That(parsed.Developers, Is.EqualTo(5));
    }

    // --- License.MajorVersion ---

    [Test]
    public void MajorVersion_ParsedFromVersionString()
    {
        Assert.That(new License { Version = "5.1.0" }.MajorVersion, Is.EqualTo(5));
        Assert.That(new License { Version = "6" }.MajorVersion, Is.EqualTo(6));
        Assert.That(new License { Version = null }.MajorVersion, Is.Null);
        Assert.That(new License { Version = "abc" }.MajorVersion, Is.Null);
    }

    // --- Version validation ---

    [Test]
    public void Validate_MatchingMajorVersion_DoesNotThrow()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa); // Version = "5"
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities", requiredMajorVersion: 5));
    }

    [Test]
    public void Validate_WrongMajorVersion_ThrowsLicenseException()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa); // Version = "5"
        var ex = Assert.Throws<LicenseException>(() => LicenseValidator.Validate(key, "regira.entities", requiredMajorVersion: 6));
        Assert.That(ex!.Message, Does.Contain("version"));
    }

    [Test]
    public void Validate_NullLicenseVersion_SkipsVersionCheck()
    {
        var license = ValidLicense();
        license.Version = null;
        var key = LicenseSigner.Sign(license, _rsa);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities", requiredMajorVersion: 6));
    }

    [Test]
    public void Validate_NoRequiredVersion_SkipsVersionCheck()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa); // Version = "5"
        Assert.DoesNotThrow(() => LicenseValidator.Validate(key, "regira.entities"));
    }

    // --- Validate(License, productCode) overload ---

    [Test]
    public void Validate_LicenseOverload_ValidatesViaRawKey()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parsed = LicenseParser.Parse(key);
        var result = LicenseValidator.Validate(parsed, "regira.entities");
        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public void Validate_LicenseOverload_WrongProduct_ThrowsLicenseException()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parsed = LicenseParser.Parse(key);
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(parsed, "regira.services"));
    }

    [Test]
    public void Validate_LicenseOverload_NoRawKey_ThrowsLicenseException()
    {
        var license = new License { Products = ["regira.entities"] };
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(license, "regira.entities"));
    }

    [Test]
    public void Validate_LicenseOverload_MatchingMajorVersion_DoesNotThrow()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parsed = LicenseParser.Parse(key);
        Assert.DoesNotThrow(() => LicenseValidator.Validate(parsed, "regira.entities", requiredMajorVersion: 5));
    }

    [Test]
    public void Validate_LicenseOverload_WrongMajorVersion_ThrowsLicenseException()
    {
        var key = LicenseSigner.Sign(ValidLicense(), _rsa);
        var parsed = LicenseParser.Parse(key);
        Assert.Throws<LicenseException>(() => LicenseValidator.Validate(parsed, "regira.entities", requiredMajorVersion: 6));
    }

    private static License ValidLicense(string customer = "test-customer") => new()
    {
        CustomerId = customer,
        Products = ["regira.entities"],
        Version = "5",
        IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
        ExpiresAtUnix = DateTimeOffset.UtcNow.AddYears(1).ToUnixTimeSeconds()
    };

    /// <summary>A negative <paramref name="expiredSince"/> yields a key that expires that far in the future.</summary>
    private static License ExpiredLicense(TimeSpan expiredSince) => new()
    {
        CustomerId = "test-customer",
        Products = ["regira.entities"],
        Version = "5",
        IssuedAtUnix = DateTimeOffset.UtcNow.AddYears(-1).ToUnixTimeSeconds(),
        ExpiresAtUnix = (DateTimeOffset.UtcNow - expiredSince).ToUnixTimeSeconds()
    };

    private static string CaptureConsoleError(Action action) => CaptureConsole(action).Stderr;

    private static (string Stdout, string Stderr) CaptureConsole(Action action)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try { action(); }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
        return (stdout.ToString(), stderr.ToString());
    }

    private static License TrialLicense(string customer = "test-customer") => new()
    {
        CustomerId = customer,
        Tier = "trial",
        Products = ["regira.entities"],
        Version = "5",
        IssuedAtUnix = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
        ExpiresAtUnix = DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds()
    };
}
