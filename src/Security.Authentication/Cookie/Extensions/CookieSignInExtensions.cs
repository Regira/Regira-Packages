using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Cookie.Models;
using Regira.Security.Authentication.Core.Services;
using System.Security.Claims;

namespace Regira.Security.Authentication.Cookie.Extensions;

public static class CookieSignInExtensions
{
    /// <summary>
    /// Signs the caller in, normalizing <paramref name="claims"/> first so the cookie principal carries the
    /// canonical <see cref="Core.Models.RegiraClaimTypes"/> spellings as well as its own.
    /// <para>
    /// Normalizing here rather than per request is deliberate: a cookie principal is deserialized from the ticket
    /// on every request, so anything that adds claims per request has to be idempotent or it accumulates them.
    /// Doing it once, at sign-in, puts the canonical claims *into* the ticket.
    /// </para>
    /// <para>
    /// <c>expiresIn</c> overrides the scheme's <c>ExpireTimeSpan</c> for this session only, and only matters when
    /// <c>isPersistent</c> is set — a non-persistent cookie lives as long as the browser session regardless.
    /// </para>
    /// </summary>
    public static Task SignInWithClaimsAsync(this HttpContext context, IEnumerable<Claim> claims, string? authenticationScheme = null, bool isPersistent = false, TimeSpan? expiresIn = null)
    {
        var scheme = ResolveScheme(context, authenticationScheme);
        var options = context.RequestServices.GetKeyedService<CookieAuthOptions>(scheme);

        var identity = ClaimsNormalizer.Normalize(claims, scheme, options?.Claims);
        var properties = new AuthenticationProperties { IsPersistent = isPersistent };
        if (expiresIn.HasValue)
        {
            properties.ExpiresUtc = DateTimeOffset.UtcNow.Add(expiresIn.Value);
        }

        return context.SignInAsync(scheme, new ClaimsPrincipal(identity), properties);
    }

    /// <summary>Signs the caller out of the cookie scheme, which expires the cookie in the response.</summary>
    public static Task SignOutCookieAsync(this HttpContext context, string? authenticationScheme = null)
        => context.SignOutAsync(ResolveScheme(context, authenticationScheme));

    /// <summary>
    /// The scheme to sign into: the caller's, else the host's default <b>sign-in</b> scheme.
    /// <para>
    /// Read from <see cref="AuthenticationOptions"/> rather than from whichever <see cref="CookieAuthOptions"/>
    /// happens to be registered — a host can have several cookie schemes, and "the one registered last" is not a
    /// contract anyone could rely on. This is the same scheme <c>SignInAsync(null, …)</c> picks, so the two cannot
    /// disagree. <c>AddCookieAuthentication</c> sets it, so it is present whenever a cookie scheme exists.
    /// </para>
    /// <para>
    /// ⚠️ Deliberately does <b>not</b> fall back to <see cref="AuthenticationOptions.DefaultScheme"/>. In a host
    /// running <c>AddSchemeSelector</c> that is the policy scheme, which forwards by credential and therefore cannot
    /// sign anyone in — falling back to it trades a clear "no cookie scheme is registered" for an obscure "the
    /// handler for 'Bearer' cannot be used for SignInAsync".
    /// </para>
    /// </summary>
    private static string ResolveScheme(HttpContext context, string? authenticationScheme)
    {
        if (authenticationScheme != null)
        {
            return authenticationScheme;
        }

        var authentication = context.RequestServices.GetService<IOptions<AuthenticationOptions>>()?.Value;

        return authentication?.DefaultSignInScheme ?? CookieAuthDefaults.AuthenticationScheme;
    }
}
