namespace Regira.Security.Authentication.Core.Models;

public class SchemeSelectorOptions
{
    /// <summary>Name of the policy scheme itself. Becomes the host's default authenticate and challenge scheme.</summary>
    public string AuthenticationScheme { get; set; } = SchemeSelectorDefaults.AuthenticationScheme;

    public string DisplayName { get; set; } = SchemeSelectorDefaults.DisplayName;

    /// <summary>
    /// Where a request carrying no recognised credential is sent. It decides what an unauthenticated caller gets
    /// back — the cookie scheme redirects to a login page, a bearer scheme answers <c>401</c> with
    /// <c>WWW-Authenticate</c>.
    /// <para>
    /// Left null, the lowest-ordered rule whose scheme is registered is used, which for an API is the bearer
    /// scheme.
    /// </para>
    /// </summary>
    public string? FallbackScheme { get; set; }

    /// <summary>
    /// The scheme that answers a challenge, when it should not be decided by the forwarding rules.
    /// <para>
    /// ⚠️ The rules key on the credential a request <b>carries</b>, and a browser arriving at a guarded page carries
    /// none — so left to them, a challenge resolves to the lowest-ordered rule and answers <c>401</c>. In an app with
    /// interactive sign-in that makes login unreachable. Left null and
    /// <see cref="ForwardChallengeToSignInScheme"/> on, a registered sign-in scheme (OpenID Connect) is used;
    /// otherwise the rules decide, which is correct for an API.
    /// </para>
    /// <para>
    /// A host serving <em>both</em> browsers and API clients should set this deliberately: whichever it names, the
    /// other kind of caller gets the wrong answer to a missing credential (a redirect where it wanted <c>401</c>, or
    /// the reverse). Name the schemes per endpoint with
    /// <c>[Authorize(AuthenticationSchemes = …)]</c> where the two must differ.
    /// </para>
    /// </summary>
    public string? ChallengeScheme { get; set; }

    /// <summary>
    /// Whether a registered interactive sign-in scheme answers challenges when <see cref="ChallengeScheme"/> is unset
    /// (default <c>true</c>). Turn off to let the forwarding rules decide, i.e. to keep a bearer <c>401</c>.
    /// </summary>
    public bool ForwardChallengeToSignInScheme { get; set; } = true;

    /// <summary>
    /// Rules in addition to the built-in ones, or — with <see cref="UseDefaultRules"/> off — instead of them.
    /// </summary>
    public IList<SchemeForwardRule> Rules { get; } = [];

    /// <summary>
    /// Whether to include rules for the bearer and API-key schemes (default <c>true</c>). Rules naming an
    /// unregistered scheme are skipped, so leaving this on costs nothing when only one scheme is enabled.
    /// </summary>
    public bool UseDefaultRules { get; set; } = true;
}
