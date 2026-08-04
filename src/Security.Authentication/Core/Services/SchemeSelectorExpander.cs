using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Core.Services;

/// <summary>
/// Expands the scheme selector's policy scheme to the schemes its rules can forward to, restricted to those
/// actually registered.
/// </summary>
public class SchemeSelectorExpander(SchemeSelectorOptions selectorOptions, IOptions<AuthenticationOptions> authenticationOptions) : IAuthenticationSchemeExpander
{
    public IReadOnlyList<string> Expand(string authenticationScheme)
    {
        if (!string.Equals(authenticationScheme, selectorOptions.AuthenticationScheme, StringComparison.Ordinal))
        {
            return [authenticationScheme];
        }

        var registered = authenticationOptions.Value.SchemeMap;
        var expanded = selectorOptions.Rules
            .OrderBy(rule => rule.Order)
            .Select(rule => rule.AuthenticationScheme)
            .Concat(selectorOptions.FallbackScheme != null ? [selectorOptions.FallbackScheme] : Array.Empty<string>())
            .Where(registered.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // Describing the policy scheme itself beats describing nothing: an empty list would read as "this
        // operation needs no credential", which is the failure the caller is trying to avoid.
        return expanded.Length > 0 ? expanded : [authenticationScheme];
    }
}
