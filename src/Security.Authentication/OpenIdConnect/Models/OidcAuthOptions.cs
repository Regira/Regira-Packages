using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Regira.Security.Authentication.Cookie.Models;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.OpenIdConnect.Models;

/// <summary>
/// Interactive sign-in through an OpenID Connect provider: authorization code + PKCE, landing in a cookie session.
/// <para>
/// This is always a <b>pair</b> of schemes — the OIDC handler runs the challenge and the code exchange, and a cookie
/// holds the resulting session. Registering only one of them is the most common way a hand-rolled setup fails: the
/// user completes the round trip and is immediately anonymous again.
/// </para>
/// </summary>
public class OidcAuthOptions
{
    public string AuthenticationScheme { get; set; } = OpenIdConnectDefaults.AuthenticationScheme;

    /// <summary>The scheme the signed-in principal is handed to. Defaults to <see cref="Cookie"/>'s scheme.</summary>
    public string? SignInScheme { get; set; }

    public string Authority { get; set; } = null!;
    public string ClientId { get; set; } = null!;

    /// <summary>
    /// Required for the confidential-client code exchange. A public client (SPA, desktop) has no place to keep one
    /// and should use PKCE alone — but this scheme is a server-side web application, which is confidential.
    /// </summary>
    public string? ClientSecret { get; set; }

    public string ResponseType { get; set; } = OpenIdConnectResponseType.Code;

    public ICollection<string> Scopes { get; set; } = ["openid", "profile", "email"];

    /// <summary>
    /// ⚠️ Must match a redirect URI registered with the provider <b>exactly</b>, including scheme and port.
    /// <para>
    /// Behind a load balancer or reverse proxy that terminates TLS, the handler builds <c>redirect_uri</c> from the
    /// incoming request — which arrives as plain HTTP on an internal host. The provider then rejects it as
    /// unregistered, or accepts it and returns the browser to the wrong origin, where the correlation cookie set on
    /// the original origin is not sent back and the callback fails with "Correlation failed". Configure
    /// <c>UseForwardedHeaders</c> ahead of the authentication middleware.
    /// </para>
    /// </summary>
    public string CallbackPath { get; set; } = "/signin-oidc";

    public string SignedOutCallbackPath { get; set; } = "/signout-callback-oidc";

    /// <summary>Where the provider returns to after sign-out. Must be registered with the provider.</summary>
    public string? SignedOutRedirectUri { get; set; }

    public bool UsePkce { get; set; } = true;

    /// <summary>
    /// Keep the id/access/refresh tokens in the cookie's authentication properties. Required for calling a
    /// downstream API with the user's token — and it makes the cookie considerably larger.
    /// </summary>
    public bool SaveTokens { get; set; }

    /// <summary>
    /// Fetch claims the id_token omits from the provider's userinfo endpoint. On by default because a lean id_token
    /// is common and the missing claim is usually <c>email</c>.
    /// </summary>
    public bool GetClaimsFromUserInfoEndpoint { get; set; } = true;

    public bool RequireHttpsMetadata { get; set; } = true;

    public string NameClaimType { get; set; } = RegiraClaimTypes.Name;
    public string RoleClaimType { get; set; } = RegiraClaimTypes.Role;

    /// <summary>Accepted issuers. Left null, the discovery document's issuer is used.</summary>
    public ICollection<string>? ValidIssuers { get; set; }

    /// <summary>The session half of the pair. Its scheme is what <see cref="SignInScheme"/> defaults to.</summary>
    public CookieAuthOptions Cookie { get; } = new();

    public ClaimNormalizationOptions Claims { get; } = new();

    /// <summary>
    /// Applied before the normalization hook is chained on, so replacing <c>Events</c> wholesale cannot drop it.
    /// </summary>
    public Action<OpenIdConnectOptions>? Configure { get; set; }
}
