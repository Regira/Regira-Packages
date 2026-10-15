using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Web.Models;
using System.Diagnostics.CodeAnalysis;

namespace Regira.Security.Authentication.Web.Controllers;

/// <summary>
/// The links the account mails carry. The caller names the page a link opens, and only a page on one of
/// <see cref="AccountEmailOptions.AllowedOrigins"/> gets one: whoever controls that page receives the token.
/// The base controllers check every link with it; an action or endpoint of the application's own that mails a
/// token link checks its page the same way.
/// </summary>
public static class AccountLinks
{
    public const string NotAllowed = "The page must be on an origin the application allows.";

    /// <summary>
    /// Resolved per request rather than injected, so the constructors every derived controller calls stay unchanged.
    /// </summary>
    public static AccountEmailOptions GetAccountEmailOptions(this HttpContext httpContext)
        => httpContext.RequestServices.GetService<IOptions<AccountEmailOptions>>()?.Value ?? new AccountEmailOptions();

    /// <summary>
    /// <paramref name="url"/> as an absolute URI when it is on one of <see cref="AccountEmailOptions.AllowedOrigins"/>,
    /// otherwise <c>null</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No origin is allowed, or an allowed one is not an http(s) URL.</exception>
    public static Uri? FindAllowedPage(this AccountEmailOptions options, string? url)
    {
        if (options.AllowedOrigins.Count == 0)
        {
            throw new InvalidOperationException(
                "No origin is allowed for the links in account emails. List the origins the password-reset and " +
                $"email-confirmation pages are served from in {nameof(AccountEmailOptions)}.{nameof(AccountEmailOptions.AllowedOrigins)}, " +
                $"e.g. services.Configure<{nameof(AccountEmailOptions)}>(configuration.GetSection(\"Authentication:AccountEmails\")) " +
                "with \"AllowedOrigins\": [ \"https://app.example.com\" ].");
        }

        var origins = options.AllowedOrigins
            .Select(allowed => TryParseHttpUri(allowed, out var origin)
                ? origin
                : throw new InvalidOperationException(
                    $"'{allowed}' in {nameof(AccountEmailOptions)}.{nameof(AccountEmailOptions.AllowedOrigins)} is not an " +
                    "absolute http(s) URL such as https://app.example.com."))
            .ToList();

        // A link carrying credentials is never one of the application's own pages.
        if (!TryParseHttpUri(url, out var page) || page.UserInfo.Length > 0)
        {
            return null;
        }

        return origins.Any(origin => page.Scheme == origin.Scheme
                                     && page.Port == origin.Port
                                     && string.Equals(page.IdnHost, origin.IdnHost, StringComparison.OrdinalIgnoreCase))
            ? page
            : null;
    }

    /// <summary>
    /// The link to <paramref name="page"/> with <paramref name="token"/> as its query string, percent-encoded: the token
    /// is Base64, and a query parser reads its <c>+</c> as a space.
    /// </summary>
    public static string WithToken(this Uri page, string token)
        => new UriBuilder(page) { Query = $"?token={Uri.EscapeDataString(token)}" }.Uri.AbsoluteUri;

    private static bool TryParseHttpUri(string? value, [NotNullWhen(true)] out Uri? uri)
        => Uri.TryCreate(value, UriKind.Absolute, out uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
