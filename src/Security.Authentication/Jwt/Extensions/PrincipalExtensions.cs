using Duende.IdentityModel;
using System.Security.Claims;

namespace Regira.Security.Authentication.Jwt.Extensions;

public static class PrincipalExtensions
{
    extension(ClaimsPrincipal principal)
    {
        public string? FindUserId()
        {
            return principal.FindClaim(ClaimTypes.NameIdentifier, JwtClaimTypes.Subject);
        }

        /// <summary>
        /// <see cref="System.Security.Principal.IIdentity.Name"/> first: it resolves through the identity's
        /// configured name claim type, which on a JWT principal is the JWT spelling (<c>name</c>) — nothing
        /// maps that to <see cref="ClaimTypes.Name"/> inbound, so reading the URI alone returns null.
        /// </summary>
        public string? FindUserName()
        {
            return principal.Identity?.Name ?? principal.FindClaim(ClaimTypes.Name, JwtClaimTypes.Name);
        }

        public string? FindEmail()
        {
            return principal.FindClaim(ClaimTypes.Email, JwtClaimTypes.Email);
        }

        /// <summary>
        /// Every distinct role on the principal, across all three spellings a Regira scheme can emit — the plain
        /// JWT <c>role</c>, Entra's <c>roles</c>, and the <see cref="ClaimTypes.Role"/> URI the API-key handler and
        /// ASP.NET Identity use. Reading one spelling answers empty for the schemes using another.
        /// </summary>
        public IReadOnlyList<string> FindRoles()
        {
            return principal
                .FindAll(claim => claim.Type.Equals(JwtClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                                  || claim.Type.Equals("roles", StringComparison.OrdinalIgnoreCase)
                                  || claim.Type.Equals(ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
                .Select(claim => claim.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        /// <summary>
        /// Whether the token grants <paramref name="scope"/>.
        /// <para>
        /// ⚠️ Scopes arrive as <em>one space-delimited string</em>, not one claim each — Entra spells the claim
        /// <c>scp</c>, most other providers <c>scope</c>. So <c>HasClaim("scp", "api.read")</c> is false against a
        /// token that plainly grants <c>api.read</c>, because the claim's value is the whole list.
        /// </para>
        /// </summary>
        public bool HasScope(string scope)
        {
            return principal
                .FindAll(claim => claim.Type.Equals("scp", StringComparison.OrdinalIgnoreCase)
                                  || claim.Type.Equals(JwtClaimTypes.Scope, StringComparison.OrdinalIgnoreCase))
                .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Any(granted => string.Equals(granted, scope, StringComparison.Ordinal));
        }

        /// <summary>
        /// The value of the first claim matching any of <paramref name="types"/>, in order. Both spellings are
        /// tried because whether a claim reaches the principal under its .NET URI or its JWT name depends on the
        /// inbound claim-type map — a detail no caller of these helpers should have to know.
        /// </summary>
        private string? FindClaim(params string[] types)
        {
            return types
                .Select(type => principal.FindFirst(type)?.Value)
                .FirstOrDefault(value => value != null);
        }
    }
}
