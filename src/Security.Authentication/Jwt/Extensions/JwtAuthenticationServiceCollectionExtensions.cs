using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Core.Abstraction;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Regira.Security.Authentication.Jwt.Extensions;

public static class JwtAuthenticationServiceCollectionExtensions
{
    public static AuthenticationBuilder AddJwtAuthentication(this IServiceCollection services, Action<JwtTokenOptions> configureOptions)
    {
        var options = new JwtTokenOptions();
        configureOptions(options);

        if (string.IsNullOrEmpty(options.Secret))
        {
            throw new NullReferenceException($"Missing {nameof(options.Secret)} in {typeof(JwtTokenOptions).FullName}");
        }

        if (options.ValidateSecretLength)
        {
            JwtSigningKeyRules.EnsureSecretLength(options.Secret, options.Algorithm);
        }

        if (options.UseJwtClaimTypes)
        {
            UseJwtClaimTypes();
        }

        return services
            // Registered so anything chaining off this scheme can read its configuration — AddRefreshTokens needs
            // LifeSpan to report when the access token it mints expires.
            .AddSingleton(options)
            // Describes itself once, so a security document does not need a transformer class per scheme.
            .AddSingleton<ISecuritySchemeDescriptor>(SecuritySchemeDescriptor.Bearer(options.AuthenticationScheme))
            .AddTransient<ITokenHelper>(_ => new JwtTokenHelper(options))
            .AddAuthentication(options.AuthenticationScheme)
            .AddJwtBearer(options.AuthenticationScheme, x =>
            {
                var secretKeyBytes = Encoding.ASCII.GetBytes(options.Secret);
                var symmetricKey = new SymmetricSecurityKey(secretKeyBytes);

                var audiences = options.Audiences ??
                                (!string.IsNullOrWhiteSpace(options.Audience) ? new[] { options.Audience } : null);


                if (options.UseJwtClaimTypes)
                {
                    // Belt and braces, not the load-bearing part: this delegate runs when the options system
                    // materialises JwtBearerOptions (first authentication), by which time the handler has
                    // already copied the static set above. Pinning the map on the scheme's own handler keeps
                    // it correct even if something resets that static after startup.
                    foreach (var handler in x.TokenHandlers.OfType<JsonWebTokenHandler>())
                    {
                        handler.InboundClaimTypeMap = InboundClaimTypeMap();
                    }
                }

                x.RequireHttpsMetadata = true;
                x.SaveToken = false;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = options.NameClaimType,
                    RoleClaimType = options.RoleClaimType,
                    ValidIssuer = options.Authority,
                    ValidAudiences = audiences,
                    IssuerSigningKey = symmetricKey,
                    ValidateIssuerSigningKey = true,
                    ValidateIssuer = options.Authority != null,
                    ValidateAudience = audiences?.Any() ?? false,
                    ValidateLifetime = options.LifeSpan > 0,
                    ClockSkew = TimeSpan.Zero
                };
            });
    }

    /// <summary>
    /// Keeps JWT claim names as they are written in the token, mapping only the three identity claims that
    /// .NET code addresses through <see cref="ClaimTypes"/>. Notably <c>role</c> stays <c>role</c> — the
    /// spelling <see cref="JwtTokenOptions.RoleClaimType"/> validates against.
    /// <para>
    /// ⚠️ Both maps matter. ASP.NET Core 8 and later validate bearer tokens with
    /// <see cref="JsonWebTokenHandler"/>, which carries its own inbound map; a setup that configures only the
    /// legacy <see cref="JwtSecurityTokenHandler"/> one leaves the default WS-2008 mapping in force, so an
    /// inbound <c>role</c> claim arrives as <see cref="ClaimTypes.Role"/> and resolves against nothing. That
    /// failure is quiet: <c>[Authorize(Roles=…)]</c> answers 403, and a row-security filter reading the claim
    /// returns 200 with the rows a role-less user may see.
    /// </para>
    /// </summary>
    public static void UseJwtClaimTypes()
    {
        // Assigned, never mutated in place: these are process-global statics, and clearing one before
        // refilling it lets a second host starting concurrently observe — or collide with — a half-built map.
        // Each target gets its own instance so a later edit of one cannot reach the others.
        JwtSecurityTokenHandler.DefaultOutboundClaimTypeMap = OutboundClaimTypeMap();
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap = InboundClaimTypeMap();
        JsonWebTokenHandler.DefaultInboundClaimTypeMap = InboundClaimTypeMap();
    }

    /// <summary>Writes the three identity claims under their JWT names; every other claim keeps the type it was created with.</summary>
    private static Dictionary<string, string> OutboundClaimTypeMap() => new()
    {
        [ClaimTypes.NameIdentifier] = JwtClaimTypes.Subject,
        [ClaimTypes.Name] = JwtClaimTypes.Name,
        [ClaimTypes.Email] = JwtClaimTypes.Email
    };

    /// <summary>
    /// The inbound mappings <see cref="UseJwtClaimTypes"/> keeps; every other claim passes through unrenamed.
    /// <para>
    /// Deliberately absent: <c>name</c> and <c>role</c>, the two claims a
    /// <see cref="TokenValidationParameters"/> addresses by name. Renaming either one inbound contradicts
    /// <see cref="JwtTokenOptions.NameClaimType"/> / <see cref="JwtTokenOptions.RoleClaimType"/>, which hold
    /// the JWT spelling — <see cref="System.Security.Principal.IIdentity.Name"/> and
    /// <see cref="ClaimsPrincipal.IsInRole"/> would then resolve against a claim type nothing carries.
    /// <c>sub</c> has no such knob and is what Regira's own account controllers read.
    /// </para>
    /// </summary>
    private static Dictionary<string, string> InboundClaimTypeMap() => new()
    {
        [JwtClaimTypes.Subject] = ClaimTypes.NameIdentifier,
        [JwtClaimTypes.Email] = ClaimTypes.Email
    };
}