using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Cookie;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// ⚠️ Which scheme <c>SignInWithClaimsAsync</c> signs into when the caller names none.
/// <para>
/// Two things make this worth its own fixture, and the default cookie host has neither: the scheme is
/// <b>not</b> called <c>"Cookies"</c>, so a resolver falling back to that constant is caught; and the scheme selector
/// is registered, so <c>DefaultScheme</c> is a policy scheme that <em>cannot sign anyone in</em> — reading it would
/// throw rather than pick the wrong scheme.
/// </para>
/// </summary>
public class CookieSignInSchemeTests : IClassFixture<TestingWebApplicationFactory<CustomCookieSchemeStartup>>
{
    private readonly WebApplicationFactory<CustomCookieSchemeStartup> _factory;

    public CookieSignInSchemeTests(TestingWebApplicationFactory<CustomCookieSchemeStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_Sign_In_Without_An_Explicit_Scheme_Uses_The_Registered_Cookie_Scheme()
    {
        var response = await CreateClient().PostAsync("cookie-auth/login", null);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single()
            .ShouldStartWith(CustomCookieSchemeStartup.CookieName + "=");
    }

    [Fact]
    public async Task Test_The_Issued_Cookie_Authenticates()
    {
        var httpClient = CreateClient();
        (await httpClient.PostAsync("cookie-auth/login", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await httpClient.GetAsync("protected")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>Normalization still applies — it is read off the scheme's own options, which are keyed by name.</summary>
    [Fact]
    public async Task Test_Claims_Are_Normalized_Under_The_Custom_Scheme()
    {
        var httpClient = CreateClient();
        await httpClient.PostAsync("cookie-auth/login", null);

        var report = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        report!.Role.ShouldBe(CookieAuthController.AdminRole);
        report.IsInAdminRole.ShouldBeTrue();
        report.FoundUserId.ShouldBe(CookieAuthController.UserId);
    }

    [Fact]
    public async Task Test_Sign_Out_Without_An_Explicit_Scheme_Ends_The_Session()
    {
        var httpClient = CreateClient();
        await httpClient.PostAsync("cookie-auth/login", null);

        (await httpClient.PostAsync("cookie-auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await httpClient.GetAsync("protected")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });
}
