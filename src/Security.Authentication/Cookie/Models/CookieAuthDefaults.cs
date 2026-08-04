using Microsoft.AspNetCore.Authentication.Cookies;

namespace Regira.Security.Authentication.Cookie.Models;

/// <summary>
/// Named <c>CookieAuthDefaults</c>, not <c>CookieAuthenticationDefaults</c>: the framework already owns the
/// latter, and a host importing both namespaces would have to disambiguate every use.
/// </summary>
public static class CookieAuthDefaults
{
    /// <summary>
    /// The framework's own scheme name, so <c>SignInAsync</c> / <c>SignOutAsync</c> without an explicit scheme
    /// resolve here and existing tooling that names <c>"Cookies"</c> keeps working.
    /// </summary>
    public const string AuthenticationScheme = CookieAuthenticationDefaults.AuthenticationScheme;

    /// <summary>
    /// A Regira-specific cookie name rather than the framework's <c>.AspNetCore.Cookies</c> — it makes which
    /// application issued a cookie unambiguous, and gives the scheme selector's cookie rule a known default.
    /// </summary>
    public const string CookieName = ".Regira.Auth";

    public const string LoginPath = "/login";
    public const string LogoutPath = "/logout";
    public const string AccessDeniedPath = "/forbidden";
}
