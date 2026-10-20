using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.OpenIdConnect.Extensions;
#if NET10_0_OR_GREATER
using Regira.Security.Authentication.Web.OpenApi.Transformers;
#endif
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

#if NET10_0_OR_GREATER
        // AddOpenApi and the package's transformers exist on .NET 10 only; the rest of the host runs on .NET 8 too.
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
            options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
        });
#endif
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers().RequireAuthorization();
#if NET10_0_OR_GREATER
            endpoints.MapOpenApi().AllowAnonymous();
#endif
        });
    }
}
