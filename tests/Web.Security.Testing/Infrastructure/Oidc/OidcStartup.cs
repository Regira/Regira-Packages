using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Regira.Security.Authentication.OpenIdConnect.Extensions;
using Web.Security.Testing.Infrastructure.Bearer;

namespace Web.Security.Testing.Infrastructure.Oidc;

/// <summary>
/// Interactive sign-in, tested to the <b>challenge</b> boundary: an unauthenticated request must redirect to the
/// provider's authorize endpoint with the right parameters. The code exchange that follows needs a live provider,
/// so it is not covered here — see the note in the guide.
/// <para>
/// The discovery document is supplied directly rather than fetched, which is what lets the handler build an
/// authorize URL without a network round trip.
/// </para>
/// </summary>
public class OidcStartup
{
    public const string AuthorizeEndpoint = "https://login.microsoftonline.com/fake/oauth2/v2.0/authorize";
    public const string EndSessionEndpoint = "https://login.microsoftonline.com/fake/oauth2/v2.0/logout";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddEntraIdSignIn(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "fake-client-secret";
            o.Configure = oidc => oidc.Configure = handler =>
            {
                handler.Configuration = new OpenIdConnectConfiguration
                {
                    Issuer = FakeAuthority.V2Issuer(),
                    AuthorizationEndpoint = AuthorizeEndpoint,
                    EndSessionEndpoint = EndSessionEndpoint,
                    TokenEndpoint = "https://login.microsoftonline.com/fake/oauth2/v2.0/token"
                };
                handler.Configuration.SigningKeys.Add(FakeAuthority.SigningKey);
            };
        });

        services.AddControllersFor(typeof(TestController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
    }
}
