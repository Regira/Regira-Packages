using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.Core.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.ApiKey;
using Web.Security.Testing.Infrastructure.Composition;
using Web.Security.Testing.Infrastructure.Jwt;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// The selector's job is to route a request to the one handler that can read its credential. Two things make that
/// worth testing rather than reading: the choice is made once per request, so a wrong guess costs the request its
/// other options and 401s a caller who had a perfectly good credential; and the default-scheme decision it takes
/// over used to belong to whichever <c>Add…Authentication</c> ran last.
/// </summary>
public class SchemeSelectorTests : IClassFixture<TestingWebApplicationFactory<SchemeSelectorStartup>>
{
    private readonly WebApplicationFactory<SchemeSelectorStartup> _factory;

    public SchemeSelectorTests(TestingWebApplicationFactory<SchemeSelectorStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    /// <summary>
    /// The policy scheme owns the default, even though it was registered after two schemes that each set their own.
    /// This is the assertion that makes registration order stop mattering.
    /// </summary>
    [Fact]
    public async Task Test_Selector_Owns_The_Default_Scheme()
    {
        using var scope = _factory.Services.CreateScope();
        var schemeProvider = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemeProvider.GetDefaultAuthenticateSchemeAsync())!.Name
            .ShouldBe(SchemeSelectorDefaults.AuthenticationScheme);
        (await schemeProvider.GetDefaultChallengeSchemeAsync())!.Name
            .ShouldBe(SchemeSelectorDefaults.AuthenticationScheme);
    }

    [Fact]
    public async Task Test_Bearer_Token_Reaches_The_Jwt_Scheme()
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(await CreateToken());

        var response = await httpClient.GetAsync("protected/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(JwtUsers.Admin.UserId);
    }

    [Fact]
    public async Task Test_ApiKey_Header_Reaches_The_ApiKey_Scheme()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await httpClient.GetAsync("protected/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(ApiKeyOwners.Default.OwnerId);
    }

    /// <summary>A role carried by either scheme satisfies the same <c>[Authorize(Roles = …)]</c>.</summary>
    [Fact]
    public async Task Test_Admin_Role_From_Either_Scheme_Passes_The_Same_Endpoint()
    {
        var withToken = _factory.CreateClient();
        withToken.SetBearerToken(await CreateToken());
        (await withToken.GetAsync("protected/admin")).StatusCode.ShouldBe(HttpStatusCode.OK);

        var withKey = _factory.CreateClient();
        withKey.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.WithAdminRole.Key);
        (await withKey.GetAsync("protected/admin")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>A credential-less request gets one 401 from the fallback scheme, not a redirect and not a 500.</summary>
    [Fact]
    public async Task Test_No_Credential_Is_Unauthorized()
    {
        var response = await _factory.CreateClient().GetAsync("protected");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A blank API-key header must not count as an API-key request. <c>ApiKeyAuthenticationHandler</c> answers
    /// <c>NoResult</c> for a blank key, so forwarding there would spend the request's one choice of handler and
    /// 401 without the bearer scheme ever being offered — which is why the rule tests for a non-empty value
    /// rather than for the header's presence.
    /// </summary>
    [Fact]
    public async Task Test_Blank_ApiKey_Header_Does_Not_Claim_The_Request()
    {
        var httpClient = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "protected");
        request.Headers.TryAddWithoutValidation(ApiKeyDefaults.HeaderName, string.Empty);

        var response = await httpClient.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        // The challenge came from the bearer scheme, proving the blank key did not capture the request.
        response.Headers.WwwAuthenticate.Select(header => header.Scheme).ShouldContain("Bearer");
    }

    /// <summary>
    /// A bearer token still wins when an API key is also present: the bearer rule is ordered ahead of it, so the
    /// caller's stronger credential is the one that is read.
    /// </summary>
    [Fact]
    public async Task Test_Bearer_Wins_When_Both_Credentials_Are_Present()
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(await CreateToken());
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await httpClient.GetAsync("protected/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(JwtUsers.Admin.UserId);
    }

    /// <summary>
    /// ⚠️ The regression test for the selector's effect on the generated document. The default scheme is now the
    /// policy scheme, which authenticates nothing and is declared by no document transformer — so a requirement
    /// naming it directly would reference a <c>securitySchemes</c> entry that does not exist, and the auth prompt
    /// would vanish while every operation still claimed to need a credential.
    /// </summary>
    [Fact]
    public async Task Test_Operations_Name_The_Real_Schemes_Not_The_Policy_Scheme()
    {
        var document = JsonDocument.Parse(await _factory.CreateClient().GetStringAsync("openapi/v1.json"));

        var declared = document.RootElement
            .GetProperty("components").GetProperty("securitySchemes")
            .EnumerateObject().Select(scheme => scheme.Name).ToArray();
        declared.ShouldContain("Bearer");
        declared.ShouldContain(ApiKeyDefaults.AuthenticationScheme);

        var required = document.RootElement
            .GetProperty("paths").GetProperty("/protected").GetProperty("get").GetProperty("security")
            .EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))
            .ToArray();

        required.ShouldContain("Bearer");
        required.ShouldContain(ApiKeyDefaults.AuthenticationScheme);
        required.ShouldNotContain(SchemeSelectorDefaults.AuthenticationScheme);

        // Every requirement must resolve to something the document declares.
        required.ShouldAllBe(scheme => declared.Contains(scheme));
    }

    private async Task<string> CreateToken()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("auth", new { username = JwtUsers.Admin.Name });
        var result = await response.Content.ReadFromJsonAsync<TokenResult>();
        return result!.Token!;
    }
}
