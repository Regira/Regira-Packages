using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.Cookie.Models;

namespace Regira.Security.Authentication.Core.Models;

/// <summary>Factories for the rules <see cref="SchemeSelectorOptions"/> uses by default.</summary>
public static class SchemeForwardRules
{
    /// <summary>An <c>Authorization: Bearer …</c> header. The scheme token is matched case-insensitively, per RFC 7235.</summary>
    public static SchemeForwardRule Bearer(string authenticationScheme = JwtBearerDefaults.AuthenticationScheme)
        => new(SchemeSelectorDefaults.BearerOrder, authenticationScheme,
            context => HasAuthorizationScheme(context, "Bearer"));

    /// <summary>An <c>Authorization: Basic …</c> header.</summary>
    public static SchemeForwardRule Basic(string authenticationScheme)
        => new(SchemeSelectorDefaults.BasicOrder, authenticationScheme,
            context => HasAuthorizationScheme(context, "Basic"));

    /// <summary>
    /// A non-empty API-key header. The header name is read from the scheme's own
    /// <see cref="ApiKeyAuthenticationOptions"/> at request time, so a custom <c>ApiKeyHeaderName</c> is honoured.
    /// </summary>
    public static SchemeForwardRule ApiKey(string? authenticationScheme = null)
    {
        var scheme = authenticationScheme ?? ApiKeyDefaults.AuthenticationScheme;
        return new SchemeForwardRule(SchemeSelectorDefaults.ApiKeyOrder, scheme, context =>
        {
            var headerName = context.RequestServices
                .GetService<IOptionsMonitor<ApiKeyAuthenticationOptions>>()?.Get(scheme).ApiKeyHeaderName
                ?? ApiKeyDefaults.HeaderName;

            // Emptiness matters: ApiKeyAuthenticationHandler answers NoResult for a blank key, so forwarding a
            // blank header would spend the request's one choice of handler and 401 without trying anything else.
            return context.Request.Headers.TryGetValue(headerName, out var values)
                   && values.Any(value => !string.IsNullOrWhiteSpace(value));
        });
    }

    /// <summary>
    /// A request carrying the scheme's authentication cookie. The cookie name is read from the scheme's registered
    /// <see cref="CookieAuthenticationOptions"/> at request time, so a custom name is honoured without having to
    /// be repeated here.
    /// </summary>
    public static SchemeForwardRule Cookie(string authenticationScheme = CookieAuthDefaults.AuthenticationScheme)
    {
        return new SchemeForwardRule(SchemeSelectorDefaults.CookieOrder, authenticationScheme, context =>
        {
            var cookieName = context.RequestServices
                .GetService<IOptionsMonitor<CookieAuthenticationOptions>>()?.Get(authenticationScheme).Cookie.Name;

            return cookieName != null && context.Request.Cookies.ContainsKey(cookieName);
        });
    }

    /// <summary>A request carrying a cookie of a known name, for a cookie scheme this package did not register.</summary>
    public static SchemeForwardRule Cookie(string authenticationScheme, string cookieName)
        => new(SchemeSelectorDefaults.CookieOrder, authenticationScheme,
            context => context.Request.Cookies.ContainsKey(cookieName));

    private static bool HasAuthorizationScheme(HttpContext context, string scheme)
    {
        var header = context.Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        return header != null
               && header.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)
               && header.Length > scheme.Length
               && header[scheme.Length] == ' ';
    }
}
