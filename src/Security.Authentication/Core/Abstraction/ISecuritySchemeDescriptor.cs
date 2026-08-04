using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Core.Abstraction;

/// <summary>
/// How one authentication scheme should be described in a security document (OpenAPI, and anything else that needs
/// to tell a caller what credential to send).
/// <para>
/// Contributed to DI by each <c>Add…Authentication</c>, so a scheme describes itself once instead of every document
/// generator growing a class per scheme. Deliberately free of any OpenAPI type — this lives in the core package,
/// which does not reference <c>Microsoft.OpenApi</c>; the Web package maps it onto the document format.
/// </para>
/// </summary>
public interface ISecuritySchemeDescriptor
{
    /// <summary>Must equal the registered ASP.NET Core scheme name — it is the key the document is written under.</summary>
    string AuthenticationScheme { get; }

    SecuritySchemeKind Kind { get; }

    /// <summary>Header, query or cookie name for <see cref="SecuritySchemeKind.ApiKey"/> and <see cref="SecuritySchemeKind.Cookie"/>.</summary>
    string? ParameterName { get; }

    /// <summary>The HTTP authentication scheme token (<c>bearer</c>, <c>basic</c>) for <see cref="SecuritySchemeKind.Http"/>.</summary>
    string? HttpScheme { get; }

    /// <summary>Discovery document URL for <see cref="SecuritySchemeKind.OpenIdConnect"/>.</summary>
    string? OpenIdConnectUrl { get; }

    string? Description { get; }
}
