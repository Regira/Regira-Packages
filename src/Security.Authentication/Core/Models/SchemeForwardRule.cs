using Microsoft.AspNetCore.Http;

namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// "Forward to <paramref name="AuthenticationScheme"/> when <paramref name="Match"/> holds." Rules are evaluated
/// in ascending <paramref name="Order"/> and the first match wins, so a rule matching a broad condition belongs
/// after the narrow ones.
/// </summary>
/// <param name="Order">Evaluation order; see <see cref="SchemeSelectorDefaults"/> for the built-in values.</param>
/// <param name="AuthenticationScheme">
/// The scheme to forward to. A rule naming a scheme that is not registered is skipped rather than failing, so a
/// default rule set can describe more schemes than any one host enables.
/// </param>
/// <param name="Match">
/// Whether this request carries the kind of credential the scheme handles. Must not authenticate anything — it
/// decides which handler gets the chance, and a false positive costs the request its other options.
/// </param>
public sealed record SchemeForwardRule(int Order, string AuthenticationScheme, Func<HttpContext, bool> Match);
