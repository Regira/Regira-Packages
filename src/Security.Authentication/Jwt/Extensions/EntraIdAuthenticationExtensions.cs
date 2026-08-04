using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;

namespace Regira.Security.Authentication.Jwt.Extensions;

public static class EntraIdAuthenticationExtensions
{
    /// <summary>
    /// Protects this API with Microsoft Entra ID: validates access tokens Entra issued for it.
    /// <para>
    /// A preset over <see cref="BearerAuthenticationExtensions.AddBearerAuthentication(IServiceCollection,Action{BearerValidationOptions})"/>
    /// — no <c>Microsoft.Identity.Web</c>, no MSAL. That is enough to protect an API and (with the OpenID Connect
    /// scheme) to sign users in. It is <b>not</b> enough to call Microsoft Graph or another API *as the signed-in
    /// user*: the on-behalf-of flow, the MSAL token cache, incremental consent and claims challenges all live in
    /// <c>Microsoft.Identity.Web</c>. Take that package directly if you need them.
    /// </para>
    /// </summary>
    public static AuthenticationBuilder AddEntraIdBearer(this IServiceCollection services, Action<EntraIdOptions> configure)
        => services.AddAuthentication().AddEntraIdBearer(configure);

    /// <inheritdoc cref="AddEntraIdBearer(IServiceCollection,Action{EntraIdOptions})"/>
    public static AuthenticationBuilder AddEntraIdBearer(this IServiceCollection services, IConfiguration configuration)
        => services.AddEntraIdBearer(options => configuration.GetSection(AuthenticationSections.EntraId).Bind(options));

    /// <inheritdoc cref="AddEntraIdBearer(IServiceCollection,Action{EntraIdOptions})"/>
    public static AuthenticationBuilder AddEntraIdBearer(this AuthenticationBuilder builder, IConfiguration configuration)
        => builder.AddEntraIdBearer(options => configuration.GetSection(AuthenticationSections.EntraId).Bind(options));

    /// <inheritdoc cref="AddEntraIdBearer(IServiceCollection,Action{EntraIdOptions})"/>
    public static AuthenticationBuilder AddEntraIdBearer(this AuthenticationBuilder builder, Action<EntraIdOptions> configure)
    {
        var entra = new EntraIdOptions();
        configure(entra);

        return builder.AddBearerAuthentication(options => ToBearerOptions(entra, options));
    }

    /// <summary>Expands the app-registration values into the validation options Entra actually needs.</summary>
    internal static void ToBearerOptions(EntraIdOptions entra, BearerValidationOptions options)
    {
        Require(entra.TenantId, nameof(entra.TenantId));
        Require(entra.ClientId, nameof(entra.ClientId));

        options.AuthenticationScheme = entra.AuthenticationScheme;
        options.Authority = entra.Authority;
        options.SaveToken = entra.SaveToken;
        options.RoleClaimType = EntraIdDefaults.RoleClaimType;

        // Both forms, because Entra issues one or the other depending on how the client asked for the scope.
        options.Audiences = entra.Audiences ?? [$"api://{entra.ClientId}", entra.ClientId];

        if (entra.IsSingleTenant)
        {
            // Both spellings: a registration that has not opted into v2 tokens issues under the v1 host, and the
            // mismatch surfaces as IDX10205 rather than anything naming the version.
            options.ValidIssuers = EntraIssuerValidation.ForSingleTenant(entra.Instance, entra.TenantId);
        }

        options.Configure = bearer =>
        {
            entra.Configure?.Invoke(bearer);

            if (!entra.IsSingleTenant)
            {
                // Applied *after* the consumer's delegate, and deliberately so: a security control that customization
                // can overwrite is one that will eventually be overwritten, and this hole is silent — every token it
                // then accepts is genuinely valid, just from a tenant nobody agreed to trust. Setting it last also
                // means it survives a delegate that replaces TokenValidationParameters outright.
                bearer.TokenValidationParameters.ValidateIssuer = true;
                bearer.TokenValidationParameters.IssuerValidator =
                    EntraIssuerValidation.ForMultiTenant(entra.Instance, entra.TenantId);
            }
        };

        // All four lists, not just roles: a caller who customised SubjectClaimTypes and had it silently dropped would
        // get a principal missing the identity they configured, with nothing to indicate why.
        entra.Claims.MergeInto(options.Claims);
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{nameof(EntraIdOptions)}.{name} is required — it comes from the API's app registration.");
        }
    }
}
