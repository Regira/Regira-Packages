using Regira.Security.Authentication.Core.Abstraction;

namespace Regira.Security.Authentication.Core.Models;

/// <inheritdoc cref="ISecuritySchemeDescriptor"/>
public class SecuritySchemeDescriptor : ISecuritySchemeDescriptor
{
    public required string AuthenticationScheme { get; init; }
    public required SecuritySchemeKind Kind { get; init; }
    public string? ParameterName { get; init; }
    public string? HttpScheme { get; init; }
    public string? OpenIdConnectUrl { get; init; }
    public string? Description { get; init; }

    public static SecuritySchemeDescriptor Bearer(string authenticationScheme) => new()
    {
        AuthenticationScheme = authenticationScheme,
        Kind = SecuritySchemeKind.Http,
        HttpScheme = "bearer",
        Description = "JSON Web Token in the 'Authorization: Bearer …' header"
    };

    public static SecuritySchemeDescriptor ApiKey(string authenticationScheme, string headerName) => new()
    {
        AuthenticationScheme = authenticationScheme,
        Kind = SecuritySchemeKind.ApiKey,
        ParameterName = headerName,
        Description = $"API key required in the '{headerName}' header"
    };

    public static SecuritySchemeDescriptor Cookie(string authenticationScheme, string cookieName) => new()
    {
        AuthenticationScheme = authenticationScheme,
        Kind = SecuritySchemeKind.Cookie,
        ParameterName = cookieName,
        Description = $"Session cookie '{cookieName}', set by signing in"
    };

    public static SecuritySchemeDescriptor OpenIdConnect(string authenticationScheme, string authority) => new()
    {
        AuthenticationScheme = authenticationScheme,
        Kind = SecuritySchemeKind.OpenIdConnect,
        // Derived rather than configured: it is what the handler itself fetches when MetadataAddress is unset.
        OpenIdConnectUrl = $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
        Description = "Interactive sign-in through the OpenID Connect provider"
    };
}
