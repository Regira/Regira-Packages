using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Cookie.Extensions;
using Regira.Security.Authentication.Cookie.Models;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Core.Services;
using Regira.Security.Authentication.OpenIdConnect.Models;
using System.Security.Claims;

namespace Regira.Security.Authentication.OpenIdConnect.Extensions;

public static class OidcAuthenticationExtensions
{
    /// <summary>
    /// Adds interactive sign-in through an OpenID Connect provider — authorization code + PKCE into a cookie session.
    /// <para>
    /// Registers <b>both</b> schemes the flow needs and wires their defaults: the cookie authenticates requests, the
    /// OIDC handler answers challenges. Getting that pairing wrong is the usual reason a hand-rolled setup completes
    /// the round trip and leaves the user anonymous.
    /// </para>
    /// <para>
    /// ⚠️ Sign-out needs <b>both</b> halves — <c>SignOutAsync</c> on the cookie scheme and on the OIDC scheme. Clearing
    /// only the cookie leaves the session at the provider intact, so the next challenge signs the user straight back in
    /// without a prompt and "log out" appears to do nothing.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddOidcAuthentication(this IServiceCollection services, Action<OidcAuthOptions> configure)
    {
        var options = new OidcAuthOptions();
        configure(options);

        RequiredOptions.Require(options.Authority, $"{nameof(OidcAuthOptions)}.{nameof(options.Authority)}");
        RequiredOptions.Require(options.ClientId, $"{nameof(OidcAuthOptions)}.{nameof(options.ClientId)}");

        var signInScheme = options.SignInScheme ?? options.Cookie.AuthenticationScheme;

        var builder = services.AddAuthentication(authentication =>
        {
            // The cookie authenticates; the OIDC handler challenges. Both, or an [Authorize] endpoint either tries to
            // validate an id_token it does not have or redirects to the provider on every single request.
            authentication.DefaultScheme = signInScheme;
            authentication.DefaultAuthenticateScheme = signInScheme;
            authentication.DefaultSignInScheme = signInScheme;
            authentication.DefaultSignOutScheme = options.AuthenticationScheme;
            authentication.DefaultChallengeScheme = options.AuthenticationScheme;
        });

        options.Cookie.AuthenticationScheme = signInScheme;
        // the cookie scheme is also signed into directly (SignInWithClaimsAsync), and its claim types put the app's own
        // ahead of the defaults, as every preset's do, so they win the first-match search
        options.Cookie.Claims.LayOverDefaults();
        builder.AddCookieScheme(options.Cookie);

        services.AddSingleton<ISecuritySchemeDescriptor>(
            SecuritySchemeDescriptor.OpenIdConnect(options.AuthenticationScheme, options.Authority));

        // Lets AddSchemeSelector send challenges here. Its forwarding rules key on the credential a request carries,
        // and a browser arriving at a guarded page carries none — so without this the challenge would resolve to a
        // bearer 401 and sign-in would be unreachable.
        services.AddSingleton(new InteractiveSignInScheme(options.AuthenticationScheme));

        return builder.AddOpenIdConnect(options.AuthenticationScheme, oidc => Apply(oidc, options, signInScheme));
    }

    /// <inheritdoc cref="AddOidcAuthentication(IServiceCollection,Action{OidcAuthOptions})"/>
    public static AuthenticationBuilder AddOidcAuthentication(this IServiceCollection services, IConfiguration configuration)
        => services.AddOidcAuthentication(options => configuration.GetSection(AuthenticationSections.Oidc).Bind(options));

    internal static void Apply(OpenIdConnectOptions oidc, OidcAuthOptions options, string signInScheme)
    {
        oidc.Authority = options.Authority;
        oidc.ClientId = options.ClientId;
        oidc.ClientSecret = options.ClientSecret;
        oidc.ResponseType = options.ResponseType;
        oidc.CallbackPath = options.CallbackPath;
        oidc.SignedOutCallbackPath = options.SignedOutCallbackPath;
        oidc.SignedOutRedirectUri = options.SignedOutRedirectUri ?? oidc.SignedOutRedirectUri;
        oidc.UsePkce = options.UsePkce;
        oidc.SaveTokens = options.SaveTokens;
        oidc.GetClaimsFromUserInfoEndpoint = options.GetClaimsFromUserInfoEndpoint;
        oidc.RequireHttpsMetadata = options.RequireHttpsMetadata;
        oidc.SignInScheme = signInScheme;

        oidc.Scope.Clear();
        foreach (var scope in options.Scopes)
        {
            oidc.Scope.Add(scope);
        }

        // Provider claim names are kept as the token spells them; the canonical spellings are added afterwards by
        // ClaimsNormalizer. Leaving mapping on would rename claims out from under the normalizer's source lists.
        oidc.MapInboundClaims = false;
        oidc.TokenValidationParameters.NameClaimType = options.NameClaimType;
        oidc.TokenValidationParameters.RoleClaimType = options.RoleClaimType;
        if (options.ValidIssuers != null)
        {
            oidc.TokenValidationParameters.ValidIssuers = options.ValidIssuers;
        }

        // Consumer first, so it can set anything here — including Events — before the normalization hook is chained
        // onto whatever it left behind.
        options.Configure?.Invoke(oidc);

        var configured = oidc.Events?.OnTokenValidated;
        oidc.Events ??= new OpenIdConnectEvents();
        oidc.Events.OnTokenValidated = async context =>
        {
            if (configured != null)
            {
                await configured(context);
            }

            Normalize(context, options.Claims);
        };
    }

    /// <summary>
    /// Normalizes before the principal is handed to the cookie scheme, so the canonical claims are written <b>into
    /// the ticket</b> rather than recomputed on every request from a principal that is already deserialized.
    /// </summary>
    private static void Normalize(TokenValidatedContext context, ClaimNormalizationOptions claimOptions)
    {
        if (context.Principal == null)
        {
            return;
        }

        var authenticationType = context.Principal.Identity?.AuthenticationType ?? context.Scheme.Name;
        var identity = ClaimsNormalizer.Normalize(context.Principal.Claims, authenticationType, claimOptions);
        context.Principal = new ClaimsPrincipal(identity);
    }

}
