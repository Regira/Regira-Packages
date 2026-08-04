using Regira.Security.Authentication.Jwt.Models;
using System.Security.Claims;

namespace Regira.Security.Authentication.Jwt.Abstraction;

public interface IRefreshTokenService
{
    /// <summary>Mints an access token and the refresh token that will renew it. Call this on a successful sign-in.</summary>
    Task<TokenPair> Issue(string userId, IEnumerable<Claim> claims, string? audience = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exchanges a refresh token for a new pair, or returns null when it is unknown, expired, revoked, or the user is
    /// no longer valid.
    /// <para>
    /// ⚠️ <c>claimsResolver</c> re-reads the user's claims from the source of truth, given their id. It is a required
    /// parameter rather than something stored precisely because it must not be skippable: replaying the claims captured
    /// at sign-in means a role removed an hour ago is still in force, and a disabled account keeps working until its
    /// refresh token expires. Returning null refuses the refresh — the user is gone, locked out or disabled — and
    /// revokes the rest of the chain with it.
    /// </para>
    /// </summary>
    Task<TokenPair?> Refresh(string refreshToken, Func<string, Task<IEnumerable<Claim>?>> claimsResolver, CancellationToken cancellationToken = default);

    /// <summary>Revokes one refresh token and the rest of its chain. Use for an explicit sign-out.</summary>
    Task Revoke(string refreshToken, CancellationToken cancellationToken = default);

    /// <summary>Revokes every refresh token a user holds — password change, disabled account, "sign out everywhere".</summary>
    Task RevokeAllForUser(string userId, CancellationToken cancellationToken = default);
}
