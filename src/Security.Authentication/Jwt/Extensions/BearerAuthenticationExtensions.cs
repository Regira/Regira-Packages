using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Core.Services;
using Regira.Security.Authentication.Jwt.Models;
using System.Security.Claims;
using System.Text;

namespace Regira.Security.Authentication.Jwt.Extensions;

public static class BearerAuthenticationExtensions
{
    /// <summary>
    /// Adds a bearer scheme that validates tokens issued by an external authority, discovering its signing keys
    /// from <c>{Authority}/.well-known/openid-configuration</c>.
    /// <para>
    /// Use this for Entra ID, Auth0, Keycloak, Duende or Okta — any OpenID Connect provider needs nothing but an
    /// <see cref="BearerValidationOptions.Authority"/> and an audience. Use
    /// <see cref="JwtAuthenticationServiceCollectionExtensions.AddJwtAuthentication"/> instead when this
    /// application is the one issuing the tokens.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddBearerAuthentication(this IServiceCollection services, Action<BearerValidationOptions> configure)
        => services.AddAuthentication().AddBearerAuthentication(configure);

    /// <inheritdoc cref="AddBearerAuthentication(IServiceCollection,Action{BearerValidationOptions})"/>
    public static AuthenticationBuilder AddBearerAuthentication(this IServiceCollection services, IConfiguration configuration)
        => services.AddBearerAuthentication(options => configuration.GetSection(AuthenticationSections.Bearer).Bind(options));

    /// <inheritdoc cref="AddBearerAuthentication(IServiceCollection,Action{BearerValidationOptions})"/>
    public static AuthenticationBuilder AddBearerAuthentication(this AuthenticationBuilder builder, IConfiguration configuration)
        => builder.AddBearerAuthentication(options => configuration.GetSection(AuthenticationSections.Bearer).Bind(options));

    /// <inheritdoc cref="AddBearerAuthentication(IServiceCollection,Action{BearerValidationOptions})"/>
    public static AuthenticationBuilder AddBearerAuthentication(this AuthenticationBuilder builder, Action<BearerValidationOptions> configure)
    {
        var options = new BearerValidationOptions();
        configure(options);

        builder.Services.AddSingleton<ISecuritySchemeDescriptor>(
            SecuritySchemeDescriptor.Bearer(options.AuthenticationScheme));

        return builder.AddJwtBearer(options.AuthenticationScheme, bearer => Apply(bearer, options));
    }

    /// <summary>Projects <see cref="BearerValidationOptions"/> onto the framework's options.</summary>
    internal static void Apply(JwtBearerOptions bearer, BearerValidationOptions options)
    {
        var hasAuthority = !string.IsNullOrWhiteSpace(options.Authority) || !string.IsNullOrWhiteSpace(options.MetadataAddress);
        var hasSecret = !string.IsNullOrWhiteSpace(options.Secret);

        if (hasAuthority == hasSecret)
        {
            throw new InvalidOperationException(
                $"{nameof(BearerValidationOptions)} needs exactly one source of signing keys: either " +
                $"{nameof(options.Authority)} (or {nameof(options.MetadataAddress)}) to discover the issuer's " +
                $"public keys, or {nameof(options.Secret)} for an issuer that signs with a shared symmetric key. " +
                (hasSecret
                    ? "Both are set — remove the one that does not apply."
                    : "Neither is set."));
        }

        var audiences = options.Audiences?.ToArray()
                        ?? (!string.IsNullOrWhiteSpace(options.Audience) ? [options.Audience] : null);

        bearer.Authority = options.Authority;
        bearer.MetadataAddress = options.MetadataAddress!;
        bearer.RequireHttpsMetadata = options.RequireHttpsMetadata;
        bearer.SaveToken = options.SaveToken;

        // Provider claim names are kept as the token spells them; the canonical spellings are added afterwards by
        // ClaimsNormalizer. Leaving mapping on would rename claims out from under the normalizer's source lists.
        bearer.MapInboundClaims = false;

        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = options.NameClaimType,
            RoleClaimType = options.RoleClaimType,
            ValidAudiences = audiences,
            ValidateAudience = audiences?.Length > 0,
            ValidIssuers = options.ValidIssuers,
            // With no explicit list the issuer from the discovery document is used, which the handler wires up.
            ValidateIssuer = true,
            ValidateLifetime = options.ValidateLifetime,
            ClockSkew = options.ClockSkew,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = hasSecret ? new SymmetricSecurityKey(Encoding.ASCII.GetBytes(options.Secret!)) : null
        };

        // Consumer first, so it can set anything here — including Events — before the normalization hook is
        // chained onto whatever it left behind.
        options.Configure?.Invoke(bearer);

        var configured = bearer.Events?.OnTokenValidated;
        bearer.Events ??= new JwtBearerEvents();
        bearer.Events.OnTokenValidated = async context =>
        {
            if (configured != null)
            {
                await configured(context);
            }

            Normalize(context, options.Claims);
        };
    }

    /// <summary>
    /// Replaces the validated principal with one carrying the canonical claim spellings as well as the provider's.
    /// <para>
    /// This is where an Entra token stops needing special handling: its <c>roles</c> gains a <c>role</c> copy, so
    /// <c>[Authorize(Roles = …)]</c> and <c>RequireClaim("role", …)</c> both resolve, and <c>oid</c> becomes
    /// readable as <c>sub</c>.
    /// </para>
    /// </summary>
    private static void Normalize(TokenValidatedContext context, ClaimNormalizationOptions claimOptions)
    {
        if (context.Principal == null)
        {
            return;
        }

        var identity = ClaimsNormalizer.Normalize(context.Principal.Claims, context.Scheme.Name, claimOptions);
        context.Principal = new ClaimsPrincipal(identity);
    }
}
