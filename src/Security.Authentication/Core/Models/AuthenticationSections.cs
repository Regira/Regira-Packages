namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// Configuration paths the Regira authentication schemes bind from, so a host names a section once rather than
/// spelling the path at every call site.
/// </summary>
public static class AuthenticationSections
{
    public const string Root = "Authentication";

    public const string Jwt = $"{Root}:Jwt";

    /// <summary>Nested under <see cref="Jwt"/> — refresh tokens renew the token that scheme issues.</summary>
    public const string RefreshTokens = $"{Jwt}:RefreshTokens";

    public const string Bearer = $"{Root}:Bearer";
    public const string ApiKeys = $"{Root}:ApiKeys";
    public const string Cookie = $"{Root}:Cookie";
    public const string Oidc = $"{Root}:Oidc";
    public const string EntraId = $"{Root}:EntraId";
}
