using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Models;
using System.Text;

namespace Regira.Security.Authentication.Jwt.Services;

/// <summary>
/// The key-length rule RFC 7518 §3.2 puts on the HMAC signing algorithms: the key must be at least as long as
/// the hash it feeds. Enforced at registration so a secret that cannot possibly sign fails while the host is
/// starting, naming the field.
/// <para>
/// Without this the failure surfaces as <c>IDX10720</c> thrown from the *first login attempt* — an HTTP 500 on
/// an endpoint that looks like it has a code bug, long after the deploy that caused it, and only on the one
/// code path that mints a token.
/// </para>
/// </summary>
internal static class JwtSigningKeyRules
{
    /// <summary>Minimum key length in bytes per algorithm, by both the JWA id and the XML-dsig URI spelling.</summary>
    private static readonly Dictionary<string, int> MinimumSecretBytes = new(StringComparer.OrdinalIgnoreCase)
    {
        [SecurityAlgorithms.HmacSha256] = 32,
        [SecurityAlgorithms.HmacSha256Signature] = 32,
        [SecurityAlgorithms.HmacSha384] = 48,
        [SecurityAlgorithms.HmacSha384Signature] = 48,
        [SecurityAlgorithms.HmacSha512] = 64,
        [SecurityAlgorithms.HmacSha512Signature] = 64
    };

    /// <summary>
    /// Throws when <paramref name="secret"/> is too short for <paramref name="algorithm"/>.
    /// <para>
    /// Counted in <see cref="Encoding.ASCII"/> bytes because that is the encoding the signing key is actually
    /// derived from — a secret padded with non-ASCII characters is not as long as it looks.
    /// </para>
    /// An unrecognised <paramref name="algorithm"/> is left alone: asymmetric and future algorithms carry
    /// different rules, and inventing one here would reject a configuration that works.
    /// </summary>
    public static void EnsureSecretLength(string secret, string? algorithm)
    {
        // The same fallback JwtTokenHelper applies, so the rule matches the algorithm that will sign.
        var effectiveAlgorithm = algorithm ?? SecurityAlgorithms.HmacSha512Signature;
        if (!MinimumSecretBytes.TryGetValue(effectiveAlgorithm, out var minimumBytes))
        {
            return;
        }

        var actualBytes = Encoding.ASCII.GetByteCount(secret);
        if (actualBytes >= minimumBytes)
        {
            return;
        }

        throw new InvalidOperationException(
            $"{nameof(JwtTokenOptions)}.{nameof(JwtTokenOptions.Secret)} is {actualBytes} bytes, but " +
            $"{DescribeAlgorithm(effectiveAlgorithm)} requires at least {minimumBytes}. " +
            $"Lengthen the secret, or set {nameof(JwtTokenOptions)}.{nameof(JwtTokenOptions.Algorithm)} to an " +
            $"algorithm this key is long enough for. To skip this check — for a scheme that only validates " +
            $"tokens signed elsewhere and never issues any — set " +
            $"{nameof(JwtTokenOptions)}.{nameof(JwtTokenOptions.ValidateSecretLength)} to false.");
    }

    /// <summary>The JWA id, which is what the token header carries and what a reader recognises.</summary>
    private static string DescribeAlgorithm(string algorithm) => algorithm switch
    {
        SecurityAlgorithms.HmacSha256Signature => SecurityAlgorithms.HmacSha256,
        SecurityAlgorithms.HmacSha384Signature => SecurityAlgorithms.HmacSha384,
        SecurityAlgorithms.HmacSha512Signature => SecurityAlgorithms.HmacSha512,
        _ => algorithm
    };
}
