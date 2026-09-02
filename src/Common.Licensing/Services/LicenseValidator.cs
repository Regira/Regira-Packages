using Regira.Licensing.Models;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;

[assembly: InternalsVisibleTo("Licensing.Testing")]
[assembly: InternalsVisibleTo("Entities.DependencyInjection.Testing")]

namespace Regira.Licensing.Services;

/// <summary>
/// Validates offline RSA-signed Regira license keys against a product code and expiry.
/// For raw decoding only, use <see cref="LicenseParser.Parse"/>.
/// </summary>
public static class LicenseValidator
{
    /// <summary>
    /// How long a key keeps validating after its expiry date. Gives the owner time to renew after the
    /// startup warning (see <see cref="IsPastExpiry"/>) instead of failing the application on the day
    /// itself. That a grace period exists is public; its length is deliberately neither documented nor
    /// configurable, so it can change between versions without becoming part of the license terms.
    /// </summary>
    internal static readonly TimeSpan ExpiryGracePeriod = TimeSpan.FromDays(14);

    /// <summary>
    /// Overrides the embedded public key during testing. Set via InternalsVisibleTo in test assemblies.
    /// Always reset to null after the test.
    /// </summary>
    internal static RSA? TestPublicKey
    {
        get => LicenseParser.TestPublicKey;
        set => LicenseParser.TestPublicKey = value;
    }

    /// <summary>
    /// How far ahead of the expiry date <c>UseRegira</c> starts reminding the owner to renew.
    /// </summary>
    public static readonly TimeSpan RenewalReminderPeriod = TimeSpan.FromDays(14);

    /// <summary>The license has an expiry date and it has passed (grace period not considered).</summary>
    internal static bool IsPastExpiry(License license, DateTimeOffset now)
        => license.ExpiresAt is { } expiresAt && expiresAt < now;

    /// <summary>
    /// The license is still valid but expires within <see cref="RenewalReminderPeriod"/>. Consumers read
    /// <see cref="LicenseState.ExpiringSoon"/> from <see cref="GetStatus"/> rather than this predicate.
    /// </summary>
    internal static bool IsExpiringSoon(License license, DateTimeOffset now)
        => license.ExpiresAt is { } expiresAt && now <= expiresAt && expiresAt - now <= RenewalReminderPeriod;

    /// <summary>The license expired and the <see cref="ExpiryGracePeriod"/> after it has run out too.</summary>
    internal static bool IsRefusedAsExpired(License license, DateTimeOffset now)
        => license.ExpiresAt is { } expiresAt && expiresAt + ExpiryGracePeriod < now;

    /// <summary>
    /// Validates a DI-resolved <see cref="License"/> for the given product code.
    /// Uses the stored <see cref="License.RawKey"/> to re-verify the RSA signature.
    /// Throws <see cref="LicenseException"/> if the key is missing, invalid, wrong product, or expired.
    /// </summary>
    public static License Validate(License license, string productCode, int? requiredMajorVersion = null)
    {
        if (string.IsNullOrWhiteSpace(license.RawKey))
            throw new LicenseException(BuildMissingKeyMessage(productCode));
        return Validate(license.RawKey, productCode, requiredMajorVersion);
    }

    /// <summary>
    /// Validates a Regira license key for the given product code and returns the decoded license payload.
    /// Throws <see cref="LicenseException"/> if the key is missing, invalid, wrong product, or expired.
    /// </summary>
    public static License Validate(string? licenseKey, string productCode, int? requiredMajorVersion = null)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
            throw new LicenseException(BuildMissingKeyMessage(productCode));

        License license;
        try
        {
            license = LicenseParser.Parse(licenseKey);
        }
        catch (LicenseException)
        {
            throw new LicenseException(BuildInvalidKeyMessage(productCode));
        }

        if (!license.Products.Any(p => string.Equals(p, productCode, StringComparison.OrdinalIgnoreCase)))
            throw new LicenseException($"The Regira license key is not valid for '{productCode}'. Verify your license at https://regira.com/licensing");

        // A key that only just expired still validates for a while; UseRegira warned about it at startup.
        if (IsRefusedAsExpired(license, DateTimeOffset.UtcNow))
            throw new LicenseException($"The Regira license key for '{productCode}' expired on {license.ExpiresAt!.Value:yyyy-MM-dd}. Renew at https://regira.com/licensing");

        if (requiredMajorVersion.HasValue && license.MajorVersion.HasValue && license.MajorVersion.Value != requiredMajorVersion.Value)
            throw new LicenseException($"The Regira license key for '{productCode}' requires version {license.MajorVersion.Value}.x but version {requiredMajorVersion.Value}.x is required. Renew at https://regira.com/licensing");

        return license;
    }

    /// <summary>
    /// Describes what <paramref name="productCode"/> makes of <paramref name="licenseKey"/> without throwing:
    /// a missing, unreadable, foreign, expired or valid key each map to a <see cref="LicenseState"/> with a
    /// one-sentence message. Products expose this so a consumer can ask about a key that no longer works.
    /// The message describes the key only. What a product does with a key it does not accept differs — the
    /// hosted services fall back to the free tier, in-process modules refuse to start — so that part is the
    /// product's to add.
    /// </summary>
    public static LicenseStatus GetStatus(string? licenseKey, string productCode, DateTimeOffset? now = null)
    {
        var at = now ?? DateTimeOffset.UtcNow;
        var status = new LicenseStatus { ProductCode = productCode };

        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            status.State = LicenseState.Missing;
            status.Message = "No license key was presented.";
            return status;
        }

        License license;
        try
        {
            license = LicenseParser.Parse(licenseKey);
        }
        catch (LicenseException ex)
        {
            status.State = LicenseState.Invalid;
            status.Message = $"The license key could not be read ({ex.Message.TrimEnd('.')}) and is ignored.";
            return status;
        }

        status.CustomerId = license.CustomerId;
        status.Tier = license.Tier;
        status.Products = license.Products.ToArray();
        status.IssuedAt = license.IssuedAt;
        status.ExpiresAt = license.ExpiresAt;
        status.Limits = license.Limits is null ? null : new Dictionary<string, int>(license.Limits);
        if (license.ExpiresAt is { } expiresAt)
        {
            var days = (expiresAt - at).TotalDays;
            status.DaysUntilExpiry = (int)(days >= 0 ? Math.Ceiling(days) : Math.Floor(days));
        }

        var expiry = license.ExpiresAt?.ToString("yyyy-MM-dd");
        const string renew = "https://regira.com/licensing";

        if (!license.Products.Any(p => string.Equals(p, productCode, StringComparison.OrdinalIgnoreCase)))
        {
            status.State = LicenseState.NotAccepted;
            status.Message = $"The license key does not cover '{productCode}'. Verify your license at {renew}";
        }
        else if (IsRefusedAsExpired(license, at))
        {
            status.State = LicenseState.Expired;
            status.Message = $"The license key expired on {expiry} and is no longer accepted. Renew at {renew}";
        }
        else if (IsPastExpiry(license, at))
        {
            status.State = LicenseState.ExpiredInGrace;
            status.Accepted = true;
            status.Message = $"The license key expired on {expiry}. It is still accepted for a short grace period but will stop working soon. Renew now at {renew}";
        }
        else if (IsExpiringSoon(license, at))
        {
            status.State = LicenseState.ExpiringSoon;
            status.Accepted = true;
            var days = status.DaysUntilExpiry!.Value;
            status.Message = $"The license key expires in {days} day{(days == 1 ? "" : "s")}, on {expiry}. Renew now at {renew}";
        }
        else
        {
            status.State = LicenseState.Valid;
            status.Accepted = true;
            status.Message = expiry is null
                ? "The license key is valid and never expires."
                : $"The license key is valid until {expiry}.";
        }

        return status;
    }

    private static string BuildMissingKeyMessage(string productCode) => $"""
        A Regira license key is required to use '{productCode}'.
        Obtain a license at https://regira.com/licensing and register it once at startup:
        services.UseRegira(configuration)
        """;

    private static string BuildInvalidKeyMessage(string productCode) => $"""
        The Regira license key is not valid for '{productCode}'.
        Verify the key or obtain a new one at https://regira.com/licensing
        """;
}
