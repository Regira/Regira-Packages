using Regira.Security.Authentication.Jwt.Models;

namespace Regira.Security.Authentication.OpenIdConnect.Models;

/// <summary>
/// Signing users in with Microsoft Entra ID, expressed as the values an app registration gives you. Expands to
/// <see cref="OidcAuthOptions"/>.
/// </summary>
public class EntraIdSignInOptions
{
    /// <summary>
    /// The directory (tenant) id — or <c>organizations</c> / <c>common</c> for a multi-tenant application, in which
    /// case the issuer is validated against each token's own <c>tid</c> rather than a fixed value.
    /// </summary>
    public string TenantId { get; set; } = null!;

    public string ClientId { get; set; } = null!;

    /// <summary>
    /// ⚠️ Required. A server-side web application is a <b>confidential</b> client, and the authorization-code
    /// exchange is authenticated with this. PKCE protects the code in transit; it does not replace client
    /// authentication for this client type.
    /// </summary>
    public string ClientSecret { get; set; } = null!;

    public string Instance { get; set; } = EntraIdDefaults.Instance;

    public bool UseV2Endpoint { get; set; } = true;

    public ICollection<string> Scopes { get; set; } = ["openid", "profile", "email"];

    public string CallbackPath { get; set; } = "/signin-oidc";
    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";
    public string? SignedOutRedirectUri { get; set; }

    /// <summary>Keep the tokens in the cookie for a downstream call. Makes the cookie considerably larger.</summary>
    public bool SaveTokens { get; set; }

    /// <summary>Reach anything <see cref="OidcAuthOptions"/> exposes that this class does not.</summary>
    public Action<OidcAuthOptions>? Configure { get; set; }

    /// <summary>Whether <see cref="TenantId"/> names one directory rather than one of the wildcards.</summary>
    public bool IsSingleTenant => !string.Equals(TenantId, EntraIdDefaults.CommonTenant, StringComparison.OrdinalIgnoreCase)
                                  && !string.Equals(TenantId, EntraIdDefaults.OrganizationsTenant, StringComparison.OrdinalIgnoreCase);

    public string Authority => UseV2Endpoint
        ? $"{Instance.TrimEnd('/')}/{TenantId}/v2.0"
        : $"{Instance.TrimEnd('/')}/{TenantId}";
}
