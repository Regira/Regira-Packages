using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Cookie.Models;

public class CookieAuthOptions
{
    public string AuthenticationScheme { get; set; } = CookieAuthDefaults.AuthenticationScheme;

    public string CookieName { get; set; } = CookieAuthDefaults.CookieName;

    /// <summary>How long a session lasts. With <see cref="SlidingExpiration"/> on, measured from the last request.</summary>
    public TimeSpan ExpireTimeSpan { get; set; } = TimeSpan.FromHours(8);

    public bool SlidingExpiration { get; set; } = true;

    /// <summary>
    /// Answer <c>401</c> / <c>403</c> instead of redirecting to <see cref="LoginPath"/> /
    /// <see cref="AccessDeniedPath"/>.
    /// <para>
    /// ⚠️ Set this for any endpoint a script calls. The framework's default is a <c>302</c> to an HTML login page,
    /// which <c>fetch</c> follows transparently — so the caller receives <c>200</c> and a page of HTML where it
    /// expected JSON, and cross-origin it surfaces as an opaque CORS error rather than "you are not signed in".
    /// </para>
    /// </summary>
    public bool IsApi { get; set; }

    public string LoginPath { get; set; } = CookieAuthDefaults.LoginPath;
    public string LogoutPath { get; set; } = CookieAuthDefaults.LogoutPath;
    public string AccessDeniedPath { get; set; } = CookieAuthDefaults.AccessDeniedPath;
    public string ReturnUrlParameter { get; set; } = "returnUrl";

    /// <summary>
    /// ⚠️ A cross-site SPA needs <see cref="SameSiteMode.None"/>, which browsers only accept together with
    /// <c>Secure</c> — so it does not work over plain HTTP, including on localhost in some browsers.
    /// </summary>
    public SameSiteMode SameSite { get; set; } = SameSiteMode.Lax;

    public CookieSecurePolicy SecurePolicy { get; set; } = CookieSecurePolicy.Always;

    public string? Domain { get; set; }

    /// <summary>Source claim types folded into the canonical set when signing in. See <see cref="RegiraClaimTypes"/>.</summary>
    public ClaimNormalizationOptions Claims { get; } = new();

    /// <summary>Applied last, for anything this options class does not expose.</summary>
    public Action<CookieAuthenticationOptions>? Configure { get; set; }
}
