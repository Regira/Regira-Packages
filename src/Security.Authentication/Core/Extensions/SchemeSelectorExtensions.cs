using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Core.Services;

namespace Regira.Security.Authentication.Core.Extensions;

public static class SchemeSelectorExtensions
{
    /// <summary>
    /// Adds a policy scheme that forwards each request to the scheme matching the credential it carries, and makes
    /// it the host's default.
    /// <para>
    /// ⚠️ <b>Call this last</b>, after every scheme it should be able to forward to. Each
    /// <c>Add…Authentication</c> method sets its own default scheme, so without this the *registration order*
    /// silently decides which handler an unattributed <c>[Authorize]</c> uses. This method takes that decision
    /// over: rules naming an unregistered scheme are skipped, so the order they were added in stops mattering.
    /// </para>
    /// <example>
    /// <code>
    /// services.AddApiKeyAuthentication()
    ///         .AddInMemoryApiKeyAuthentication(configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners());
    /// services.AddJwtAuthentication(o => configuration.GetSection("Authentication:Jwt").Bind(o))
    ///         .AddSchemeSelector();
    /// </code>
    /// </example>
    /// </summary>
    public static AuthenticationBuilder AddSchemeSelector(this AuthenticationBuilder builder, Action<SchemeSelectorOptions>? configure = null)
    {
        var options = new SchemeSelectorOptions();
        configure?.Invoke(options);

        // Rules a scheme registered for itself — the only way a scheme under a non-default name can be forwarded to,
        // since the built-in rules below can only name the defaults. Added after the caller's and before those, so at
        // equal order an explicit rule still wins and a contributed one still beats a guess.
        foreach (var contributed in FindContributedRules(builder.Services))
        {
            options.Rules.Add(contributed);
        }

        if (options.UseDefaultRules)
        {
            options.Rules.Add(SchemeForwardRules.Bearer());
            options.Rules.Add(SchemeForwardRules.ApiKey());
            // Covers a cookie scheme this package did not register (the framework's own AddCookie). One registered by
            // AddCookieAuthentication has already contributed its own rule above; a duplicate at the same order is
            // harmless, and this one is skipped outright when the default-named scheme is not registered.
            options.Rules.Add(SchemeForwardRules.Cookie());
        }

        // OrderBy is stable, so equal orders keep the insertion sequence above.
        var rules = options.Rules.OrderBy(rule => rule.Order).ToArray();

        // A challenge is not a credential, so the rules cannot decide it — see SchemeSelectorOptions.ChallengeScheme.
        // Read at registration rather than per request, which is sound because this method is documented to run last,
        // after every scheme it can forward to.
        var challengeScheme = options.ChallengeScheme
                              ?? (options.ForwardChallengeToSignInScheme ? FindSignInScheme(builder.Services) : null);

        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(SchemeSelectorOptions)))
        {
            throw new InvalidOperationException(
                $"{nameof(AddSchemeSelector)} was called more than once. A second call would leave two " +
                $"{nameof(SchemeSelectorOptions)} registered and the expander reading whichever won, so the schemes " +
                "described in a security document would not be the ones the selector forwards to. Configure every " +
                "rule in a single call.");
        }

        builder.Services.AddSingleton(options);
        builder.Services.TryAddSingleton<IAuthenticationSchemeExpander, SchemeSelectorExpander>();

        // Registered after any AddAuthentication(scheme) call, and Configure delegates run in registration
        // order — so this is what makes "call last" the rule that fixes the default, rather than advice.
        builder.Services.Configure<AuthenticationOptions>(authentication =>
        {
            authentication.DefaultScheme = options.AuthenticationScheme;
            authentication.DefaultAuthenticateScheme = options.AuthenticationScheme;
            authentication.DefaultChallengeScheme = options.AuthenticationScheme;
        });

        return builder.AddPolicyScheme(options.AuthenticationScheme, options.DisplayName, policy =>
        {
            policy.ForwardDefaultSelector = context => Resolve(context, rules, options.FallbackScheme);
            // Takes precedence over the selector for challenges only; authenticate, forbid, sign-in and sign-out
            // still route by credential. Forbid deliberately stays with the rules — a forbidden caller *is*
            // authenticated, so their credential identifies the scheme that should render the refusal.
            policy.ForwardChallenge = challengeScheme;
        });
    }

    /// <summary>
    /// The interactive sign-in scheme a previous <c>Add…Authentication</c> registered, if any. Read straight from the
    /// service collection because registration-time code cannot resolve from a provider that does not exist yet.
    /// </summary>
    /// <summary>
    /// Forward rules registered by the schemes themselves. Read straight from the service collection for the same
    /// reason as <see cref="FindSignInScheme"/> — registration-time code has no provider to resolve from — which is
    /// sound because this method is documented to run last.
    /// </summary>
    private static IEnumerable<SchemeForwardRule> FindContributedRules(IServiceCollection services)
        => services
            .Where(descriptor => descriptor.ServiceType == typeof(SchemeForwardRule))
            .Select(descriptor => descriptor.ImplementationInstance)
            .OfType<SchemeForwardRule>();

    private static string? FindSignInScheme(IServiceCollection services)
        => services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(InteractiveSignInScheme))
            ?.ImplementationInstance is InteractiveSignInScheme signIn
            ? signIn.AuthenticationScheme
            : null;

    /// <summary>
    /// The first rule that both names a registered scheme and matches the request; otherwise the fallback.
    /// <para>
    /// Registration is read from <see cref="AuthenticationOptions.SchemeMap"/> rather than
    /// <see cref="IAuthenticationSchemeProvider"/> because the selector is synchronous — and the map is the same
    /// source the provider is built from.
    /// </para>
    /// </summary>
    private static string? Resolve(HttpContext context, SchemeForwardRule[] rules, string? fallbackScheme)
    {
        var registered = context.RequestServices.GetRequiredService<IOptions<AuthenticationOptions>>().Value.SchemeMap;

        foreach (var rule in rules)
        {
            if (registered.ContainsKey(rule.AuthenticationScheme) && rule.Match(context))
            {
                return rule.AuthenticationScheme;
            }
        }

        if (fallbackScheme != null && registered.ContainsKey(fallbackScheme))
        {
            return fallbackScheme;
        }

        // No credential to go on. The lowest-ordered registered scheme still shapes the challenge the caller
        // gets — a 401 with WWW-Authenticate for an API, a login redirect for a cookie app — which is more use
        // than a bare 401 from the policy scheme itself.
        return rules.FirstOrDefault(rule => registered.ContainsKey(rule.AuthenticationScheme))?.AuthenticationScheme;
    }
}
