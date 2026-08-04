using Regira.Licensing.Models;
using System.Security.Cryptography;
using System.Text.Json;

namespace Regira.Licensing.Services;

/// <summary>
/// Signs a <see cref="License"/> payload into a license key string.
/// License format: {Base64Url(utf8-json-payload)}.{Base64Url(RSA-SHA256-PKCS1-signature)}
/// </summary>
public static class LicenseSigner
{
    /// <summary>
    /// Signs the given <paramref name="license"/> payload with <paramref name="privateKey"/>
    /// and returns a license key string in the format expected by <see cref="LicenseParser"/>.
    /// </summary>
    public static string Sign(License license, RSA privateKey)
    {
        var payloadBytes = System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(license));
        var signatureBytes = privateKey.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Base64UrlEncode(payloadBytes) + "." + Base64UrlEncode(signatureBytes);
    }

    private static string Base64UrlEncode(byte[] data)
        => Convert.ToBase64String(data).Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
