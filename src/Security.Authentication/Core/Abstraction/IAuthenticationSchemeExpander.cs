namespace Regira.Security.Authentication.Core.Abstraction;

/// <summary>
/// Resolves a scheme name to the schemes that actually authenticate a request.
/// <para>
/// A policy scheme forwards rather than authenticating, so it is a real registered scheme that no security
/// document ever declares. Anything describing the API — an OpenAPI transformer, an analyzer, an audit — needs
/// the schemes behind it, or it emits a requirement naming something the document does not define.
/// </para>
/// </summary>
public interface IAuthenticationSchemeExpander
{
    /// <summary>
    /// The authenticating schemes behind <paramref name="authenticationScheme"/>, or the scheme itself when it
    /// authenticates directly. Never empty.
    /// </summary>
    IReadOnlyList<string> Expand(string authenticationScheme);
}
