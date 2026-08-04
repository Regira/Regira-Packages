using Microsoft.AspNetCore.Authentication.JwtBearer;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// Microsoft Entra ID, expressed as the few values an app registration gives you. Expands to
/// <see cref="BearerValidationOptions"/> with the authority, audiences, issuers and claim types Entra actually uses.
/// </summary>
public class EntraIdOptions
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

    /// <summary>The application (client) id of *this* API's app registration.</summary>
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

    /// <summary>
    /// Overrides the derived audiences. Left null, both <c>api://{ClientId}</c> and <c>{ClientId}</c> are accepted —
    /// Entra issues one or the other depending on how the client requested the scope, and pinning a single form
    /// rejects half of otherwise valid callers.
    /// </summary>
    public ICollection<string>? Audiences { get; set; }

    public string AuthenticationScheme { get; set; } = JwtBearerDefaults.AuthenticationScheme;

    public bool SaveToken { get; set; }

    /// <summary>Source claim types folded into the canonical set. Defaults already cover Entra's spellings.</summary>
    public ClaimNormalizationOptions Claims { get; } = new();

    public Action<JwtBearerOptions>? Configure { get; set; }

    /// <summary>Whether <see cref="TenantId"/> names one directory rather than one of the wildcards.</summary>
    public bool IsSingleTenant => !string.Equals(TenantId, EntraIdDefaults.CommonTenant, StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(TenantId, EntraIdDefaults.OrganizationsTenant, StringComparison.OrdinalIgnoreCase);

    public string Authority => UseV2Endpoint
        ? $"{Instance.TrimEnd('/')}/{TenantId}/v2.0"
        : $"{Instance.TrimEnd('/')}/{TenantId}";
}
