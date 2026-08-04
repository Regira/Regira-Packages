using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Jwt.Models;

namespace Web.Security.Testing.Infrastructure.Bearer;

/// <summary>
/// Single-tenant Entra API protection, with the signing key handed over directly instead of discovered.
/// <para>
/// Setting <c>Configuration</c> makes the handler use a <c>StaticConfigurationManager</c>, so no metadata request
/// is attempted — which is the only way to test the asymmetric validation path in-process. What that leaves
/// uncovered is the metadata *fetch* itself; the authority string it would fetch from is asserted separately, by
/// unit test, so nothing about the derivation goes unverified.
/// </para>
/// </summary>
public class EntraBearerStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddEntraIdBearer(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.Configure = bearer => bearer.Configuration = StaticConfiguration(FakeAuthority.V2Issuer());
        });

        services.AddControllersFor(typeof(TestController), typeof(EntraClaimsController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
    }

    internal static OpenIdConnectConfiguration StaticConfiguration(string issuer)
    {
        var configuration = new OpenIdConnectConfiguration { Issuer = issuer };
        configuration.SigningKeys.Add(FakeAuthority.SigningKey);
        return configuration;
    }
}

/// <summary>Multi-tenant: no fixed issuer, so the issuer is checked against the token's own <c>tid</c>.</summary>
public class EntraMultiTenantStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddEntraIdBearer(o =>
        {
            o.TenantId = EntraIdDefaults.OrganizationsTenant;
            o.ClientId = FakeAuthority.ClientId;
            // The wildcard authority's discovery document advertises a templated issuer; a real multi-tenant host
            // gets the same effect from metadata. Issuer validation is the IssuerValidator's job either way.
            o.Configure = bearer =>
            {
                bearer.Configuration = EntraBearerStartup.StaticConfiguration($"{EntraIdDefaults.Instance}/{{tenantid}}/v2.0");
                bearer.TokenValidationParameters.ValidateIssuer = true;
            };
        });

        services.AddControllersFor(typeof(TestController), typeof(EntraClaimsController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
    }
}
