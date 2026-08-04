using Regira.Security.Authentication.Jwt.Models;

namespace Regira.Security.Authentication.Jwt.Abstraction;

/// <summary>
/// Persistence for refresh tokens. Deals in opaque lookup keys — whether a key is the token or a hash of it is
/// <see cref="Services.RefreshTokenService"/>'s decision, not the store's.
/// <para>
/// Refresh tokens are the one part of this scheme that <b>needs server-side state</b>: revocation, rotation and
/// replay detection are all impossible against a stateless token. The shipped
/// <see cref="Services.InMemoryRefreshTokenStore"/> is for development and tests; a real deployment implements this
/// over its own <c>DbContext</c>.
/// </para>
/// </summary>
public interface IRefreshTokenStore
{
    Task<RefreshTokenRecord?> Find(string tokenKey, CancellationToken cancellationToken = default);

    Task Store(RefreshTokenRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a token <b>only if it is not already revoked</b>, returning whether this caller was the one that did
    /// it. The single point at which a rotation is claimed.
    /// <para>
    /// ⚠️ This must be atomic, and it is the whole reason the method is shaped as a test-and-set rather than a plain
    /// update. Two concurrent refreshes of the same token — a SPA firing two calls after one 401, as much as an
    /// attacker racing the legitimate client — would otherwise both read <c>RevokedAt == null</c>, both succeed, and
    /// split the family into two live chains with no replay detected. Returning <c>false</c> is what lets the service
    /// treat the loser as a replay.
    /// </para>
    /// <para>
    /// Implement it as a conditional write, not a read-then-write: in EF, an
    /// <c>ExecuteUpdate</c> filtered on <c>RevokedAt == null</c> with a row-count check; in SQL,
    /// <c>UPDATE … WHERE TokenKey = @k AND RevokedAt IS NULL</c> and return whether one row changed.
    /// </para>
    /// </summary>
    Task<bool> TryRevoke(string tokenKey, string? replacedByTokenKey, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes every token in one rotation chain. The response to a replayed token, so it must reach tokens that are
    /// already revoked as well as active ones — the chain is what is being ended, not one token.
    /// </summary>
    Task RevokeFamily(string familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);

    /// <summary>Revokes every token belonging to a user — a password change, a disabled account, "sign out everywhere".</summary>
    Task RevokeAllForUser(string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default);
}
