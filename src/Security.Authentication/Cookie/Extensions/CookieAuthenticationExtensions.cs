using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Cookie.Models;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Cookie.Extensions;

public static class CookieAuthenticationExtensions
{
    /// <summary>
    /// Adds cookie authentication. Sign a user in with
    /// <see cref="CookieSignInExtensions.SignInWithClaimsAsync"/>, which normalizes the claims first.
    /// <para>
    /// ⚠️ Two things a library cannot do for you. The cookie is encrypted with ASP.NET Core Data Protection, so a
    /// multi-instance or containerised host needs a <b>shared, persisted key ring</b> plus
    /// <c>SetApplicationName</c> — without it every restart and every scale-out event invalidates every cookie,
    /// and the symptom is "users get logged out at random", never an error. And a cookie authenticates
    /// <b>ambiently</b>, so state-changing endpoints need antiforgery protection that a bearer token did not
    /// require.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddCookieAuthentication(this IServiceCollection services, Action<CookieAuthOptions>? configure = null)
        => services.AddAuthentication().AddCookieAuthentication(configure);

    /// <inheritdoc cref="AddCookieAuthentication(IServiceCollection,Action{CookieAuthOptions})"/>
    public static AuthenticationBuilder AddCookieAuthentication(this IServiceCollection services, IConfiguration configuration)
        => services.AddCookieAuthentication(options => configuration.GetSection(AuthenticationSections.Cookie).Bind(options));

    /// <inheritdoc cref="AddCookieAuthentication(IServiceCollection,Action{CookieAuthOptions})"/>
    public static AuthenticationBuilder AddCookieAuthentication(this AuthenticationBuilder builder, IConfiguration configuration)
        => builder.AddCookieAuthentication(options => configuration.GetSection(AuthenticationSections.Cookie).Bind(options));

    /// <inheritdoc cref="AddCookieAuthentication(IServiceCollection,Action{CookieAuthOptions})"/>
    public static AuthenticationBuilder AddCookieAuthentication(this AuthenticationBuilder builder, Action<CookieAuthOptions>? configure = null)
    {
        var options = new CookieAuthOptions();
        configure?.Invoke(options);

        // Keyed by scheme, because a host can register more than one cookie scheme — AddOidcAuthentication registers
        // one internally. An unkeyed singleton would leave SignInWithClaimsAsync resolving whichever was registered
        // last and signing users into the wrong scheme with the wrong normalization, silently.
        builder.Services.AddKeyedSingleton(options.AuthenticationScheme, options);
        builder.Services.AddSingleton<ISecuritySchemeDescriptor>(
            SecuritySchemeDescriptor.Cookie(options.AuthenticationScheme, options.CookieName));

        // A cookie scheme is the only kind that can sign anyone in, so name it as the default for that — otherwise
        // sign-in falls back to DefaultScheme, which in a host running AddSchemeSelector is a policy scheme that
        // cannot sign in at all, and in any host is whatever happened to register last. `??=`, so an explicit choice
        // and AddOidcAuthentication's own assignment (it runs first, and pairs sign-out with the provider) both win.
        builder.Services.Configure<AuthenticationOptions>(authentication =>
        {
            authentication.DefaultSignInScheme ??= options.AuthenticationScheme;
            authentication.DefaultSignOutScheme ??= options.AuthenticationScheme;
        });

        // Contributes the rule that recognises *this* scheme's cookie. AddSchemeSelector's built-in rules can only
        // name the default scheme, so without this a cookie scheme registered under any other name is never
        // forwarded to — the cookie is issued, and every request carrying it authenticates as nobody.
        builder.Services.AddSingleton(SchemeForwardRules.Cookie(options.AuthenticationScheme));

        return builder.AddCookie(options.AuthenticationScheme, cookie =>
        {
            cookie.Cookie.Name = options.CookieName;
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = options.SameSite;
            cookie.Cookie.SecurePolicy = options.SecurePolicy;
            if (options.Domain != null)
            {
                cookie.Cookie.Domain = options.Domain;
            }

            cookie.ExpireTimeSpan = options.ExpireTimeSpan;
            cookie.SlidingExpiration = options.SlidingExpiration;
            cookie.LoginPath = options.LoginPath;
            cookie.LogoutPath = options.LogoutPath;
            cookie.AccessDeniedPath = options.AccessDeniedPath;
            cookie.ReturnUrlParameter = options.ReturnUrlParameter;

            if (options.IsApi)
            {
                // The events, not the paths: clearing LoginPath makes the handler redirect to its own default
                // rather than not redirecting, so status codes have to be written explicitly.
                cookie.Events.OnRedirectToLogin = context => WriteStatusCode(context, StatusCodes.Status401Unauthorized);
                cookie.Events.OnRedirectToAccessDenied = context => WriteStatusCode(context, StatusCodes.Status403Forbidden);
            }

            options.Configure?.Invoke(cookie);
        });
    }

    private static Task WriteStatusCode(RedirectContext<CookieAuthenticationOptions> context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return Task.CompletedTask;
    }
}
