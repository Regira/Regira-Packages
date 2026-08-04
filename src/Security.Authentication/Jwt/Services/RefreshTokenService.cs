using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Regira.Security.Authentication.Jwt.Services;

/// <summary>
/// Rotating refresh tokens with replay detection.
/// <para>
/// The security properties, in the order they matter: a refresh token is <b>opaque and random</b>, so it carries no
/// claims to go stale; it is <b>rotated</b> on every use, so a captured token is good for one call at most; and a
/// rotated token presented <b>twice</b> ends the whole chain, because a token that should have been discarded showing
/// up again means two parties hold it and there is no way to tell which one is asking.
/// </para>
/// </summary>
public class RefreshTokenService(ITokenHelper tokenHelper, IRefreshTokenStore store, JwtTokenOptions jwtOptions, RefreshTokenOptions options) : IRefreshTokenService
{
    public async Task<TokenPair> Issue(string userId, IEnumerable<Claim> claims, string? audience = null, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await IssuePair(userId, claims, audience, Guid.NewGuid().ToString("N"),
            now.AddSeconds(options.AbsoluteLifeSpan), now, CreateToken(), cancellationToken);
    }

    public async Task<TokenPair?> Refresh(string refreshToken, Func<string, Task<IEnumerable<Claim>?>> claimsResolver, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var record = await store.Find(ToKey(refreshToken), cancellationToken);
        if (record == null)
        {
            return null;
        }

        if (record.RevokedAt != null)
        {
            // A revoked token that names a successor was *used*, so seeing it again is a replay rather than a stale
            // client. Both holders are indistinguishable from here, so the chain ends.
            if (record.ReplacedByTokenKey != null && options.RevokeFamilyOnReuse)
            {
                await store.RevokeFamily(record.FamilyId, now, cancellationToken);
            }

            return null;
        }

        if (!record.IsActive(now))
        {
            return null;
        }

        // The whole reason a resolver is required: claims come from the user as they are now, not as they were.
        var claims = await claimsResolver(record.UserId);
        if (claims == null)
        {
            // The user is gone, locked out or disabled. Their outstanding tokens should not outlive that.
            await store.RevokeFamily(record.FamilyId, now, cancellationToken);
            return null;
        }

        if (!options.Rotate)
        {
            var accessToken = tokenHelper.Create(claims, record.Audience);
            return new TokenPair(accessToken, now.AddSeconds(jwtOptions.LifeSpan), refreshToken, record.ExpiresAt);
        }

        // Claim the rotation *before* issuing anything. The successor's key has to exist first so the revocation can
        // name it, but nothing is stored until this test-and-set succeeds — so of two concurrent refreshes exactly one
        // proceeds, and the loser is indistinguishable from a replay because that is precisely what it looks like from
        // here. Revoking after issuing would let both win and split the family into two live chains, defeating the
        // detection this whole design exists for.
        var successor = CreateToken();
        if (!await store.TryRevoke(ToKey(refreshToken), ToKey(successor), now, cancellationToken))
        {
            if (options.RevokeFamilyOnReuse)
            {
                await store.RevokeFamily(record.FamilyId, now, cancellationToken);
            }

            return null;
        }

        return await IssuePair(record.UserId, claims, record.Audience, record.FamilyId, record.FamilyExpiresAt, now, successor, cancellationToken);
    }

    public async Task Revoke(string refreshToken, CancellationToken cancellationToken = default)
    {
        var record = await store.Find(ToKey(refreshToken), cancellationToken);
        if (record != null)
        {
            await store.RevokeFamily(record.FamilyId, DateTimeOffset.UtcNow, cancellationToken);
        }
    }

    public Task RevokeAllForUser(string userId, CancellationToken cancellationToken = default)
        => store.RevokeAllForUser(userId, DateTimeOffset.UtcNow, cancellationToken);

    private async Task<TokenPair> IssuePair(string userId, IEnumerable<Claim> claims, string? audience, string familyId, DateTimeOffset familyExpiresAt, DateTimeOffset now, string refreshToken, CancellationToken cancellationToken)
    {
        var claimList = claims.ToArray();
        var accessToken = tokenHelper.Create(claimList, audience);

        // Never past the family's ceiling, or rotation would extend a chain indefinitely.
        var expiresAt = Min(now.AddSeconds(options.LifeSpan), familyExpiresAt);

        await store.Store(new RefreshTokenRecord
        {
            TokenKey = ToKey(refreshToken),
            FamilyId = familyId,
            UserId = userId,
            Audience = audience,
            CreatedAt = now,
            ExpiresAt = expiresAt,
            FamilyExpiresAt = familyExpiresAt
        }, cancellationToken);

        return new TokenPair(accessToken, now.AddSeconds(jwtOptions.LifeSpan), refreshToken, expiresAt);
    }

    private string CreateToken() => Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(options.TokenByteLength));

    /// <summary>
    /// The store's lookup key for a token. Unsalted SHA-256 by design: the store is looked up <em>by</em> this, which a
    /// per-token salt would make impossible, and the token is 256 bits of randomness so there is no guessable input for
    /// a slow KDF to protect. That reasoning does not transfer to passwords.
    /// </summary>
    private string ToKey(string refreshToken)
    {
        if (!options.HashStoredTokens)
        {
            return refreshToken;
        }

        // ToHexString, not ToHexStringLower — the latter is .NET 9+ and this package also targets net8.0.
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))).ToLowerInvariant();
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;
}
