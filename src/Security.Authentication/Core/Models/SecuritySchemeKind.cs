namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// The shape of a credential, mirroring OpenAPI's security-scheme types without depending on them.
/// </summary>
public enum SecuritySchemeKind
{
    /// <summary>An <c>Authorization</c> header carrying a named scheme — <c>bearer</c>, <c>basic</c>, <c>negotiate</c>.</summary>
    Http,

    /// <summary>A credential in a header or query parameter.</summary>
    ApiKey,

    /// <summary>
    /// A session cookie. OpenAPI has no dedicated type for it; the accepted convention is an API-key scheme located
    /// in the cookie, which is what the transformer emits.
    /// </summary>
    Cookie,

    /// <summary>An OpenID Connect provider, described by its discovery document.</summary>
    OpenIdConnect
}
