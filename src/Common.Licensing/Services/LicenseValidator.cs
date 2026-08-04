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
    /// Overrides the embedded public key during testing. Set via InternalsVisibleTo in test assemblies.
    /// Always reset to null after the test.
    /// </summary>
    internal static RSA? TestPublicKey
    {
        get => LicenseParser.TestPublicKey;
        set => LicenseParser.TestPublicKey = value;
    }

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

        if (license.ExpiresAt.HasValue && license.ExpiresAt.Value < DateTimeOffset.UtcNow)
            throw new LicenseException($"The Regira license key for '{productCode}' expired on {license.ExpiresAt.Value:yyyy-MM-dd}. Renew at https://regira.com/licensing");

        if (requiredMajorVersion.HasValue && license.MajorVersion.HasValue && license.MajorVersion.Value != requiredMajorVersion.Value)
            throw new LicenseException($"The Regira license key for '{productCode}' requires version {license.MajorVersion.Value}.x but version {requiredMajorVersion.Value}.x is required. Renew at https://regira.com/licensing");

        return license;
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
