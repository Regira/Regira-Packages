using Regira.Licensing.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace Regira.Licensing.Services;

/// <summary>
/// Decodes and signature-verifies a Regira license key string into a <see cref="License"/> object.
/// License format: {Base64Url(utf8-json-payload)}.{Base64Url(RSA-SHA256-PKCS1-signature)}
/// </summary>
public static class LicenseParser
{
    // Base64-encoded DER of RSA-2048 SubjectPublicKeyInfo (dev/test key — replace with production key before publishing).
    // The corresponding private key is stored outside this repository.
    private const string EmbeddedPublicKeyBase64 = "MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAz6/Ggy7H6y83Jub+twRDguE5eBHh59/nZppDNUmPBYSnr/WFgnB/mfrpStH6uJrrKEOL1zILOxLGp5JfnhbqazMXtr3wGUpT/wgg/E/IFLcYUBjIXGWczmXbHtXvxk2Rz76re5+jAau0tyi5Xab1+9535QsOsckGMSBm/16X1AxTntg0oApEZzdmefzQJ4EhCu0OmdjL+BLsGyDVo9wGRP2725Tq9qhCmQW+749TpE3+0+FhOYXRF0YwD+gZU0a00c2fa31GcXnUxg43EjJgKH+q0oXIdxtBpjbbARFfBan2trz5p/NZiUapBs/FvylTnG04qN9My6hg5/LVFD51DQIDAQAB";

    private static readonly Lazy<RSA> EmbeddedPublicKey = new(LoadEmbeddedPublicKey);

    /// <summary>
    /// Overrides the embedded public key during testing. Set via InternalsVisibleTo in test assemblies.
    /// Always reset to null after the test.
    /// </summary>
    internal static RSA? TestPublicKey;

    /// <summary>
    /// Decodes and verifies the signature of a license key string, returning the <see cref="License"/> payload.
    /// Throws <see cref="LicenseException"/> if the format or signature is invalid.
    /// Does not check product codes or expiry — use <see cref="LicenseValidator"/> for full validation.
    /// </summary>
    public static License Parse(string? licenseKey)
    {
        var key = licenseKey?.Trim() ?? string.Empty;
        if (key.Length == 0)
            throw new LicenseException("The license key format is invalid.");

        var parts = key.Split('.');
        if (parts.Length != 2)
            throw new LicenseException("The license key format is invalid.");

        byte[] payloadBytes;
        byte[] signatureBytes;
        try
        {
            payloadBytes = Base64UrlDecode(parts[0]);
            signatureBytes = Base64UrlDecode(parts[1]);
        }
        catch
        {
            throw new LicenseException("The license key format is invalid.");
        }

        var publicKey = TestPublicKey ?? EmbeddedPublicKey.Value;
        if (!publicKey.VerifyData(payloadBytes, signatureBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new LicenseException("The license key signature is invalid.");

        try
        {
            var payloadJson = System.Text.Encoding.UTF8.GetString(payloadBytes);
            var license = JsonSerializer.Deserialize<License>(payloadJson)
                ?? throw new LicenseException("The license key payload is empty.");
            license.RawKey = licenseKey;
            return license;
        }
        catch (JsonException)
        {
            throw new LicenseException("The license key payload could not be read.");
        }
    }

    internal static byte[] Base64UrlDecode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        s = (s.Length % 4) switch
        {
            2 => s + "==",
            3 => s + "=",
            _ => s
        };
        return Convert.FromBase64String(s);
    }

    private static RSA LoadEmbeddedPublicKey()
    {
        var keyBytes = Convert.FromBase64String(EmbeddedPublicKeyBase64);
        var rsa = RSA.Create();
        rsa.ImportSubjectPublicKeyInfo(keyBytes, out _);
        return rsa;
    }
}
