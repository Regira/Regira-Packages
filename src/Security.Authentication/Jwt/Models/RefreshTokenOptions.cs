namespace Regira.Security.Authentication.Jwt.Models;

public class RefreshTokenOptions
{
    /// <summary>How long one refresh token is valid, in seconds. Default 14 days.</summary>
    public int LifeSpan { get; set; } = 60 * 60 * 24 * 14;

    /// <summary>
    /// A ceiling on the whole rotation chain, in seconds. Default 90 days.
    /// <para>
    /// Without it a session that is used regularly never ends, because each rotation issues a fresh expiry. This is
    /// what makes "signed in since last year" impossible.
    /// </para>
    /// </summary>
    public int AbsoluteLifeSpan { get; set; } = 60 * 60 * 24 * 90;

    /// <summary>
    /// Issue a new refresh token on every use and revoke the one presented (default <c>true</c>). Rotation is what
    /// makes a stolen token detectable — see <see cref="RevokeFamilyOnReuse"/>.
    /// </summary>
    public bool Rotate { get; set; } = true;

    /// <summary>
    /// On seeing an already-rotated token again, revoke its whole family (default <c>true</c>).
    /// <para>
    /// A rotated token should never be presented twice, so a replay means two parties hold it — the legitimate client
    /// and someone else — and there is no way to tell which one is asking. Ending the session is the only safe answer.
    /// </para>
    /// </summary>
    public bool RevokeFamilyOnReuse { get; set; } = true;

    /// <summary>Entropy of the generated token, in bytes. Default 32 (256 bits).</summary>
    public int TokenByteLength { get; set; } = 32;

    /// <summary>
    /// Store a hash of the token rather than the token (default <c>true</c>), so a leaked store yields nothing usable.
    /// <para>
    /// The hash is a plain SHA-256 and is deliberately <b>unsalted</b>: the store is looked up <em>by</em> it, which a
    /// per-token random salt would make impossible. That is safe here and would not be for a password — the token is
    /// 256 bits of cryptographic randomness, so there is no guessable input to brute-force, which is the only thing a
    /// slow salted KDF buys.
    /// </para>
    /// </summary>
    public bool HashStoredTokens { get; set; } = true;
}
