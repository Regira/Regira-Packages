using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;
using System.Collections.Concurrent;

namespace Regira.Security.Authentication.Jwt.Services;

/// <summary>
/// ⚠️ <b>Development and tests only.</b> Two properties make it unusable in production, and neither shows up on one
/// developer machine: it loses every session on restart, and it is per-process — so behind a load balancer a refresh
/// lands on an instance that has never heard of the token and the user is signed out at random.
/// <para>
/// A real deployment implements <see cref="IRefreshTokenStore"/> over its own <c>DbContext</c> — five methods, of
/// which only <see cref="TryRevoke"/> needs care: it must be a conditional write, not a read-then-write.
/// </para>
/// </summary>
public class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _records = new(StringComparer.Ordinal);
    // object, not System.Threading.Lock — the latter is .NET 9+ and this package also targets net8.0.
    private readonly object _revocationLock = new();

    public Task<RefreshTokenRecord?> Find(string tokenKey, CancellationToken cancellationToken = default)
        => Task.FromResult(_records.GetValueOrDefault(tokenKey));

    public Task Store(RefreshTokenRecord record, CancellationToken cancellationToken = default)
    {
        _records[record.TokenKey] = record;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Test-and-set under a lock, which is what makes rotation atomic here. A `ConcurrentDictionary` alone is not
    /// enough — the check and the write must not interleave, or two concurrent refreshes both claim the same token.
    /// </summary>
    public Task<bool> TryRevoke(string tokenKey, string? replacedByTokenKey, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        lock (_revocationLock)
        {
            if (!_records.TryGetValue(tokenKey, out var record) || record.RevokedAt != null)
            {
                return Task.FromResult(false);
            }

            record.RevokedAt = revokedAt;
            record.ReplacedByTokenKey = replacedByTokenKey;
            return Task.FromResult(true);
        }
    }

    /// <summary>
    /// Reaches already-revoked tokens too, deliberately: this ends a chain, and a replayed token is by definition one
    /// that was already revoked.
    /// </summary>
    public Task RevokeFamily(string familyId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        foreach (var record in _records.Values.Where(record => record.FamilyId == familyId))
        {
            record.RevokedAt ??= revokedAt;
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllForUser(string userId, DateTimeOffset revokedAt, CancellationToken cancellationToken = default)
    {
        foreach (var record in _records.Values.Where(record => record.UserId == userId))
        {
            record.RevokedAt ??= revokedAt;
        }

        return Task.CompletedTask;
    }
}
