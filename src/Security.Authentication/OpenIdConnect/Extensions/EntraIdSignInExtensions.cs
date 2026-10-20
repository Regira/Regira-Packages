using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Core.Services;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;
using Regira.Security.Authentication.OpenIdConnect.Models;

namespace Regira.Security.Authentication.OpenIdConnect.Extensions;

public static class EntraIdSignInExtensions
{
    /// <summary>
    /// Signs users in with Microsoft Entra ID: authorization code + PKCE into a cookie session.
    /// <para>
    /// A preset over <see cref="OidcAuthenticationExtensions.AddOidcAuthentication(IServiceCollection,Action{OidcAuthOptions})"/>
    /// — no <c>Microsoft.Identity.Web</c>, no MSAL. Enough to sign a user in and read who they are; not enough to call
    /// Microsoft Graph or another API *as* them, which needs the on-behalf-of flow and a token cache.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddEntraIdSignIn(this IServiceCollection services, Action<EntraIdSignInOptions> configure)
    {
        var entra = new EntraIdSignInOptions();
        configure(entra);

        return services.AddOidcAuthentication(options => ToOidcOptions(entra, options));
    }

    /// <inheritdoc cref="AddEntraIdSignIn(IServiceCollection,Action{EntraIdSignInOptions})"/>
    public static AuthenticationBuilder AddEntraIdSignIn(this IServiceCollection services, IConfiguration configuration)
        => services.AddEntraIdSignIn(options => configuration.GetSection(AuthenticationSections.EntraId).Bind(options));

    internal static void ToOidcOptions(EntraIdSignInOptions entra, OidcAuthOptions options)
    {
        const string source = "the app registration";
        RequiredOptions.Require(entra.TenantId, $"{nameof(EntraIdSignInOptions)}.{nameof(entra.TenantId)}", source);
        RequiredOptions.Require(entra.ClientId, $"{nameof(EntraIdSignInOptions)}.{nameof(entra.ClientId)}", source);
        RequiredOptions.Require(entra.ClientSecret, $"{nameof(EntraIdSignInOptions)}.{nameof(entra.ClientSecret)}", source);

        options.Authority = entra.Authority;
        options.ClientId = entra.ClientId;
        options.ClientSecret = entra.ClientSecret;
        options.Scopes = entra.Scopes;
        options.CallbackPath = entra.CallbackPath;
        options.SignedOutCallbackPath = entra.SignedOutCallbackPath;
        options.SignedOutRedirectUri = entra.SignedOutRedirectUri;
        options.SaveTokens = entra.SaveTokens;

        // App roles can ride along in the id_token, and Entra spells them plural here too.
        options.RoleClaimType = EntraIdDefaults.RoleClaimType;

        if (entra.IsSingleTenant)
        {
            options.ValidIssuers = EntraIssuerValidation.ForSingleTenant(entra.Instance, entra.TenantId);
        }

        entra.Configure?.Invoke(options);

        // the same hole as on the API side: accepting the discovery document's templated issuer would let any
        // directory sign in
        var configured = options.Configure;
        options.Configure = oidc =>
        {
            configured?.Invoke(oidc);
            entra.ValidateWildcardIssuer(oidc.TokenValidationParameters);
        };
    }
}
