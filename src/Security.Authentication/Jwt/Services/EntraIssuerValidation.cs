using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Models;

namespace Regira.Security.Authentication.Jwt.Services;

/// <summary>
/// Issuer validation for a <b>multi-tenant</b> Entra application, shared by the API and interactive sign-in schemes.
/// <para>
/// With <c>organizations</c> or <c>common</c> there is no single issuer to compare against — every tenant issues
/// under its own id, and the discovery document advertises a templated <c>{tenantid}</c> placeholder. Leaving the
/// default in place means the application accepts tokens from <b>any</b> directory: each one genuinely valid and
/// correctly signed, just not from a tenant anybody agreed to trust. Nothing errors and nothing logs, which is why
/// this has to be wired explicitly rather than relied on.
/// </para>
/// </summary>
internal static class EntraIssuerValidation
{
    /// <summary>
    /// Accepts an issuer only when it is <paramref name="instance"/>'s endpoint for the tenant the token itself
    /// declares in <c>tid</c>. Tying the two together is what stops an issuer that merely looks Microsoft-shaped
    /// from being trusted.
    /// </summary>
    public static IssuerValidator ForMultiTenant(string instance, string configuredTenant)
    {
        return (issuer, token, _) =>
        {
            var tenantId = (token as JsonWebToken)?.Claims
                .FirstOrDefault(claim => claim.Type == EntraIdDefaults.TenantIdClaimType)?.Value;

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new SecurityTokenInvalidIssuerException(
                    $"The token carries no '{EntraIdDefaults.TenantIdClaimType}' claim, so its issuer cannot be tied " +
                    $"to a tenant. A multi-tenant application ('{configuredTenant}') cannot validate the issuer " +
                    "without it.")
                {
                    InvalidIssuer = issuer
                };
            }

            string[] accepted =
            [
                $"{instance.TrimEnd('/')}/{tenantId}/v2.0",
                $"{EntraIdDefaults.V1IssuerHost}/{tenantId}/"
            ];

            if (!accepted.Contains(issuer, StringComparer.OrdinalIgnoreCase))
            {
                throw new SecurityTokenInvalidIssuerException(
                    $"Issuer '{issuer}' does not match the endpoint for the token's own tenant '{tenantId}'. " +
                    $"Expected one of: {string.Join(", ", accepted)}.")
                {
                    InvalidIssuer = issuer
                };
            }

            return issuer;
        };
    }

    /// <summary>The issuers a single-tenant application accepts — both the v2 and v1 spellings.</summary>
    public static string[] ForSingleTenant(string instance, string tenantId) =>
    [
        $"{instance.TrimEnd('/')}/{tenantId}/v2.0",
        $"{EntraIdDefaults.V1IssuerHost}/{tenantId}/"
    ];
}
