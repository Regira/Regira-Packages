using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Services;

namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// The values a Microsoft Entra ID app registration gives you, shared by <see cref="EntraIdOptions"/>, which protects an
/// API, and <c>EntraIdSignInOptions</c>, which signs users in.
/// </summary>
public abstract class EntraIdOptionsBase
{
    /// <summary>
    /// The directory (tenant) id — or <c>organizations</c> for any work/school account, or <c>common</c> to include
    /// personal Microsoft accounts.
    /// <para>
    /// ⚠️ The two wildcards make this a <b>multi-tenant</b> application: tokens then arrive from every tenant, each
    /// issued under its own id, so a fixed issuer cannot be checked and the issuer is validated against the token's
    /// own <c>tid</c> instead. Authorizing a caller past that point is the application's job — signing in is not
    /// the same as being entitled to anything.
    /// </para>
    /// </summary>
    public string TenantId { get; set; } = null!;

    /// <summary>The application (client) id of the app registration.</summary>
    public string ClientId { get; set; } = null!;

    /// <summary>Sovereign clouds use a different host — <c>https://login.microsoftonline.us</c> and so on.</summary>
    public string Instance { get; set; } = EntraIdDefaults.Instance;

    /// <summary>
    /// Whether the app registration issues v2.0 tokens (the default for anything created in the last several years).
    /// <para>
    /// ⚠️ Get this wrong and issuer validation fails with <c>IDX10205</c>. A registration left at
    /// <c>accessTokenAcceptedVersion: null</c> issues <b>v1</b> tokens whose <c>iss</c> is
    /// <c>https://sts.windows.net/{tid}/</c>, not <c>{Instance}/{tid}/v2.0</c>. Both spellings are accepted for a
    /// single tenant regardless of this flag, so the failure mode is narrower than it used to be — but the audience
    /// differs too, and only this flag decides which one is expected.
    /// </para>
    /// </summary>
    public bool UseV2Endpoint { get; set; } = true;

    /// <summary>Whether <see cref="TenantId"/> names one directory rather than one of the wildcards.</summary>
    public bool IsSingleTenant => !string.Equals(TenantId, EntraIdDefaults.CommonTenant, StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(TenantId, EntraIdDefaults.OrganizationsTenant, StringComparison.OrdinalIgnoreCase);

    public string Authority => UseV2Endpoint
        ? $"{Instance.TrimEnd('/')}/{TenantId}/v2.0"
        : $"{Instance.TrimEnd('/')}/{TenantId}";

    /// <summary>
    /// For a wildcard tenant, which has no fixed issuer, validates the issuer against each token's own <c>tid</c>; a
    /// single tenant's issuers are listed instead. Applied <b>after</b> the consumer's own configuration, and
    /// deliberately so: a security control that customization can overwrite is one that will eventually be
    /// overwritten, and this hole is silent — every token it then accepts is genuinely valid, just from a tenant
    /// nobody agreed to trust. Setting it last also means it survives a delegate that replaces
    /// <see cref="TokenValidationParameters"/> outright.
    /// </summary>
    internal void ValidateWildcardIssuer(TokenValidationParameters parameters)
    {
        if (!IsSingleTenant)
        {
            parameters.ValidateIssuer = true;
            parameters.IssuerValidator = EntraIssuerValidation.ForMultiTenant(Instance, TenantId);
        }
    }
}
