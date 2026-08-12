using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.OpenIdConnect.Extensions;
using Regira.Security.Authentication.Web.OpenApi.Transformers;
using Web.Security.Testing.Infrastructure.ApiKey;
using Web.Security.Testing.Infrastructure.Bearer;

namespace Web.Security.Testing.Infrastructure.Composition;

/// <summary>
/// Every scheme kind at once — HTTP bearer, API key, cookie, and OpenID Connect — so the single document
/// transformer has to describe all four shapes from their contributed descriptors.
/// </summary>
public class AllSchemesStartup
{
    public const string OidcScheme = "EntraSignIn";

    public void ConfigureServices(IServiceCollection services)
    {
        services
            .AddApiKeyAuthentication()
            .AddInMemoryApiKeyAuthentication(ApiKeyOwners.Value);

        // Registers the cookie + OIDC pair.
        services.AddEntraIdSignIn(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "fake-client-secret";
            o.Configure = oidc =>
            {
                oidc.AuthenticationScheme = OidcScheme;
                oidc.Configure = handler => handler.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = FakeAuthority.V2Issuer(),
                    AuthorizationEndpoint = "https://login.microsoftonline.com/fake/oauth2/v2.0/authorize"
                };
            };
        });

        services
            .AddJwtAuthentication(o =>
                o.Secret = string.Join(":", Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N"))))
            .AddSchemeSelector();

        services.AddControllersFor(typeof(TestController));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
            options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
        });
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers().RequireAuthorization();
            endpoints.MapOpenApi().AllowAnonymous();
        });
    }
}
