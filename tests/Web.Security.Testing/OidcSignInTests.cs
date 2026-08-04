using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Cookie.Models;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.OpenIdConnect.Extensions;
using Regira.Security.Authentication.OpenIdConnect.Models;
using Shouldly;
using System.Net;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Bearer;
using Web.Security.Testing.Infrastructure.Oidc;
using Xunit;

namespace Web.Security.Testing;

public class OidcSignInTests : IClassFixture<TestingWebApplicationFactory<OidcStartup>>
{
    private readonly WebApplicationFactory<OidcStartup> _factory;

    public OidcSignInTests(TestingWebApplicationFactory<OidcStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    /// <summary>
    /// The scheme pairing, which is the thing a hand-rolled setup usually gets wrong. The cookie authenticates
    /// requests and the OIDC handler answers challenges; swap them and an <c>[Authorize]</c> endpoint either tries to
    /// validate an id_token it does not have, or redirects to the provider on every single request.
    /// </summary>
    [Fact]
    public async Task Test_Cookie_Authenticates_And_Oidc_Challenges()
    {
        using var scope = _factory.Services.CreateScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name.ShouldBe(CookieAuthenticationDefaults.AuthenticationScheme);
        (await schemes.GetDefaultSignInSchemeAsync())!.Name.ShouldBe(CookieAuthenticationDefaults.AuthenticationScheme);
        (await schemes.GetDefaultChallengeSchemeAsync())!.Name.ShouldBe(OpenIdConnectDefaults.AuthenticationScheme);
        (await schemes.GetDefaultSignOutSchemeAsync())!.Name.ShouldBe(OpenIdConnectDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// An unauthenticated request starts the authorization-code flow: a redirect to the provider's authorize endpoint
    /// carrying <c>code</c>, this client's id, the callback, and — because PKCE is on — a <c>code_challenge</c>.
    /// </summary>
    [Fact]
    public async Task Test_Unauthenticated_Request_Challenges_With_Pkce()
    {
        var response = await CreateClient().GetAsync("protected");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith(OidcStartup.AuthorizeEndpoint);

        var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
        query["response_type"].ToString().ShouldBe("code");
        query["client_id"].ToString().ShouldBe(FakeAuthority.ClientId);
        query["code_challenge_method"].ToString().ShouldBe("S256");
        query.ShouldContainKey("code_challenge");
        query.ShouldContainKey("state");
        query["redirect_uri"].ToString().ShouldEndWith("/signin-oidc");
        query["scope"].ToString().ShouldBe("openid profile email");
    }

    /// <summary>
    /// The correlation cookie is set alongside the redirect and must come back on the callback. It is the piece that
    /// fails behind a reverse proxy without forwarded headers: issued on one origin, returned to another, and the
    /// callback reports "Correlation failed" rather than anything about proxies.
    /// </summary>
    [Fact]
    public async Task Test_Challenge_Sets_A_Correlation_Cookie()
    {
        var response = await CreateClient().GetAsync("protected");

        response.Headers.GetValues("Set-Cookie")
            .ShouldContain(cookie => cookie.StartsWith(".AspNetCore.Correlation."));
    }

    /// <summary>An anonymous endpoint is served without any of that.</summary>
    [Fact]
    public async Task Test_Anonymous_Endpoint_Is_Not_Challenged()
    {
        var response = await CreateClient().GetAsync("");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });
}

/// <summary>Option shaping for the sign-in presets, asserted without a host.</summary>
public class OidcOptionsTests
{
    [Fact]
    public void Test_Entra_SignIn_Derives_Authority_And_Plural_Role_Claim()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "secret";
        });

        options.Authority.ShouldBe($"https://login.microsoftonline.com/{FakeAuthority.TenantId}/v2.0");
        options.RoleClaimType.ShouldBe("roles");
        options.ValidIssuers.ShouldBe(
        [
            $"https://login.microsoftonline.com/{FakeAuthority.TenantId}/v2.0",
            $"https://sts.windows.net/{FakeAuthority.TenantId}/"
        ], ignoreOrder: true);
    }

    [Theory]
    [InlineData("common")]
    [InlineData("organizations")]
    public void Test_Multi_Tenant_SignIn_Has_No_Fixed_Issuer_List(string tenantId)
    {
        var options = Expand(o =>
        {
            o.TenantId = tenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "secret";
        });

        options.ValidIssuers.ShouldBeNull();
    }

    /// <summary>
    /// ⚠️ The consumer's escape hatch must not be able to drop the multi-tenant issuer check. The validator is chained
    /// after whatever <c>Configure</c> leaves behind, so a delegate that replaces <c>TokenValidationParameters</c>
    /// outright still ends up with it — a security control customization can overwrite is one that eventually is.
    /// </summary>
    [Fact]
    public void Test_Consumer_Configure_Cannot_Drop_The_Multi_Tenant_Issuer_Validator()
    {
        var options = Expand(o =>
        {
            o.TenantId = EntraIdDefaults.OrganizationsTenant;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "secret";
            o.Configure = oidc => oidc.Configure = handler =>
                handler.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters();
        });

        var handler = new OpenIdConnectOptions();
        OidcAuthenticationExtensions.Apply(handler, options, CookieAuthDefaults.AuthenticationScheme);

        handler.TokenValidationParameters.IssuerValidator.ShouldNotBeNull();
        handler.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", FakeAuthority.ClientId, "secret", nameof(EntraIdSignInOptions.TenantId))]
    [InlineData(FakeAuthority.TenantId, "", "secret", nameof(EntraIdSignInOptions.ClientId))]
    [InlineData(FakeAuthority.TenantId, FakeAuthority.ClientId, "", nameof(EntraIdSignInOptions.ClientSecret))]
    public void Test_Missing_Registration_Value_Throws_Naming_It(string tenantId, string clientId, string secret, string expected)
    {
        var exception = Should.Throw<InvalidOperationException>(() => Expand(o =>
        {
            o.TenantId = tenantId;
            o.ClientId = clientId;
            o.ClientSecret = secret;
        }));

        exception.Message.ShouldContain(expected);
    }

    /// <summary>Scopes replace the handler's defaults rather than being appended to them.</summary>
    [Fact]
    public void Test_Configured_Scopes_Replace_The_Defaults()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.ClientSecret = "secret";
            o.Scopes = ["openid", "offline_access"];
        });

        var handler = new OpenIdConnectOptions();
        OidcAuthenticationExtensions.Apply(handler, options, CookieAuthDefaults.AuthenticationScheme);

        handler.Scope.ShouldBe(["openid", "offline_access"], ignoreOrder: true);
    }

    private static OidcAuthOptions Expand(Action<EntraIdSignInOptions> configure)
    {
        var entra = new EntraIdSignInOptions();
        configure(entra);

        var oidc = new OidcAuthOptions();
        EntraIdSignInExtensions.ToOidcOptions(entra, oidc);
        return oidc;
    }
}
