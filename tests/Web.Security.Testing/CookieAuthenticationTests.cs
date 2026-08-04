using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Regira.Security.Authentication.Cookie.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Cookie;
using Xunit;

namespace Web.Security.Testing;

public class CookieAuthenticationTests : IClassFixture<TestingWebApplicationFactory<CookieStartup>>
{
    private readonly WebApplicationFactory<CookieStartup> _factory;

    public CookieAuthenticationTests(TestingWebApplicationFactory<CookieStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_AllowAnonymous_Is_Reachable_Without_A_Cookie()
    {
        (await CreateClient().GetAsync("")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// ⚠️ The reason <c>IsApi</c> exists. The framework's default is a <c>302</c> to a login page, which
    /// <c>HttpClient</c> and <c>fetch</c> both follow — so a script asking for JSON gets <c>200</c> and a page of
    /// HTML, and cross-origin it surfaces as an opaque CORS error rather than "you are not signed in". Redirects
    /// are disabled on the client so the raw status is observable.
    /// </summary>
    [Fact]
    public async Task Test_Protected_Without_Cookie_Is_401_Not_A_Redirect()
    {
        var response = await CreateClient().GetAsync("protected");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Test_Sign_In_Then_Protected_Returns_Ok()
    {
        var httpClient = CreateClient();
        await SignIn(httpClient);

        var response = await httpClient.GetAsync("protected");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>The sign-in cookie carries the configured name and the security attributes the options set.</summary>
    [Fact]
    public async Task Test_Cookie_Is_HttpOnly_Secure_And_Named()
    {
        var response = await CreateClient().PostAsync("cookie-auth/login", null);

        var setCookie = response.Headers.GetValues("Set-Cookie").Single();
        setCookie.ShouldStartWith(CookieAuthDefaults.CookieName + "=");
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("secure", Case.Insensitive);
        setCookie.ShouldContain("samesite=lax", Case.Insensitive);
    }

    /// <summary>
    /// Claims were signed in under the <see cref="System.Security.Claims.ClaimTypes"/> URIs, the way
    /// <c>IUserClaimsPrincipalFactory</c> produces them. Normalization means the canonical spellings are on the
    /// principal too, so a hand-written <c>role</c> check works — the thing that fails without it.
    /// </summary>
    [Fact]
    public async Task Test_Cookie_Principal_Carries_The_Canonical_Claims()
    {
        var httpClient = CreateClient();
        await SignIn(httpClient);

        var report = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        report!.Role.ShouldBe(CookieAuthController.AdminRole);
        report.MappedRole.ShouldBe(CookieAuthController.AdminRole);
        report.IsInAdminRole.ShouldBeTrue();
        report.UserId.ShouldBe(CookieAuthController.UserId);
        report.Name.ShouldBe(CookieAuthController.UserName);
        report.FoundUserId.ShouldBe(CookieAuthController.UserId);
        report.FoundUserName.ShouldBe(CookieAuthController.UserName);
        report.FoundEmail.ShouldBe("cookie-user@example.com");
    }

    [Fact]
    public async Task Test_Role_Protected_Endpoint_Honours_The_Cookie_Role()
    {
        var withRole = CreateClient();
        await SignIn(withRole);
        (await withRole.GetAsync("protected/admin")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withoutRole = CreateClient();
        await SignIn(withoutRole, withRole: false);
        (await withoutRole.GetAsync("protected/admin")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>In API mode a role failure is a 403, not a redirect to an access-denied page.</summary>
    [Fact]
    public async Task Test_Forbidden_Is_403_Not_A_Redirect()
    {
        var httpClient = CreateClient();
        await SignIn(httpClient, withRole: false);

        var response = await httpClient.GetAsync("protected/admin");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Test_Sign_Out_Ends_The_Session()
    {
        var httpClient = CreateClient();
        await SignIn(httpClient);

        (await httpClient.PostAsync("cookie-auth/logout", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The handler cleared the cookie, so the next request is anonymous again.
        (await httpClient.GetAsync("protected")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// Normalization runs at sign-in, so the canonical claims live in the ticket. Re-reading the same cookie must
    /// not add more of them — a cookie principal is deserialized on every request, so a per-request normalizer
    /// would accumulate duplicates.
    /// </summary>
    [Fact]
    public async Task Test_Repeated_Requests_Do_Not_Accumulate_Role_Claims()
    {
        var httpClient = CreateClient();
        await SignIn(httpClient);

        var first = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");
        var second = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");
        var third = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        second!.Role.ShouldBe(first!.Role);
        third!.Role.ShouldBe(first.Role);
        third.IsInAdminRole.ShouldBeTrue();
    }

    private static async Task SignIn(HttpClient httpClient, bool withRole = true)
    {
        var response = await httpClient.PostAsync($"cookie-auth/login?withRole={withRole}", null);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    /// <summary>
    /// Redirects off, so a <c>302</c> the handler should not have emitted is visible instead of being followed into
    /// a misleading <c>200</c>. Cookies stay on, since the whole scheme depends on them round-tripping.
    /// <para>
    /// ⚠️ An <b>https</b> base address, and it is load-bearing. <c>CookieSecurePolicy.Always</c> — the Regira
    /// default — marks the cookie <c>secure</c>, and no cookie jar returns a secure cookie over plain
    /// <c>http://</c>. Over <c>http</c> every request after sign-in is anonymous: the cookie is issued, silently
    /// never sent back, and the endpoint 401s as if the sign-in had failed. <c>TestServer</c> does no real TLS but
    /// honours the scheme, so this reproduces the production shape rather than weakening the policy to suit the test.
    /// </para>
    /// </summary>
    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
        BaseAddress = new Uri("https://localhost")
    });
}
