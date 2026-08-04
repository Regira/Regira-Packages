namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// One refresh token as the store holds it. Named <c>…Record</c> because it is not the token — it is what is kept
/// <em>about</em> a token, and the token value itself is deliberately not among its fields.
/// </summary>
public class RefreshTokenRecord
{
    /// <summary>
    /// The lookup key for the token. With <see cref="RefreshTokenOptions.HashStoredTokens"/> on — the default — this
    /// is a hash rather than the token, so a leaked store does not hand over usable sessions.
    /// </summary>
    public string TokenKey { get; set; } = null!;

    /// <summary>
    /// Groups a token with every token it was rotated from and into. Revoking the family is the response to a
    /// replayed token: the whole chain is suspect, not just the one that was presented twice.
    /// </summary>
    public string FamilyId { get; set; } = null!;

    public string UserId { get; set; } = null!;

    /// <summary>The audience the access token was minted for, so a refresh keeps the same one.</summary>
    public string? Audience { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>
    /// When the whole family expires, regardless of rotation. Without it a chain that is used often enough never
    /// expires, and a session lives forever.
    /// </summary>
    public DateTimeOffset FamilyExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>
    /// The token this one was rotated into. Set together with <see cref="RevokedAt"/>, and the pair is what
    /// distinguishes an ordinary rotation from a replay: a revoked token that names a successor was <em>used</em>,
    /// so seeing it again means two parties hold it.
    /// </summary>
    public string? ReplacedByTokenKey { get; set; }

    public bool IsActive(DateTimeOffset now) => RevokedAt == null && ExpiresAt > now && FamilyExpiresAt > now;
}
