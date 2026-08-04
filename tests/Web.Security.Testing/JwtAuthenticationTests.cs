using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Jwt;
using Xunit;

namespace Web.Security.Testing;

public class JwtAuthenticationTests : IClassFixture<TestingWebApplicationFactory<JwtStartup>>
{
    private readonly WebApplicationFactory<JwtStartup> _factory;
    public JwtAuthenticationTests(TestingWebApplicationFactory<JwtStartup> factory)
    {
        _factory = factory
            // otherwise throws DirectoryNotFound Exception
            .WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_AllowAnonymous_Welcome()
    {
        var httpClient = _factory.CreateClient();

        var response = await httpClient.GetAsync("");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
    [Fact]
    public async Task Test_Protected_Without_Token_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
    [Fact]
    public async Task Test_Protected_With_Valid_Token_Returns_Ok()
    {
        var httpClient = _factory.CreateClient();
        var token = await CreateToken();
        httpClient.SetBearerToken(token);

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
    [Fact]
    public async Task Test_Protected_With_Invalid_Token_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();
        var token = await CreateToken();
        httpClient.SetBearerToken(token);

        // wait 2.5 sec to invalidate the token
        await Task.Delay(2500);

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// The claim-type map is the one part of the JWT setup that lives outside the token: a token can carry a
    /// perfectly good <c>role</c> claim that the principal no longer has, because the inbound map renamed it.
    /// Nothing errors when that happens — <c>[Authorize(Roles=…)]</c> 403s and a hand-written row-security
    /// check silently narrows its result set — so it is asserted against the validated principal, not the token.
    /// </summary>
    [Fact]
    public async Task Test_Role_Claim_Survives_Token_Validation()
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(await CreateToken());

        var report = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        report!.Role.ShouldBe(JwtUsers.AdminRole);
        report.IsInAdminRole.ShouldBeTrue();
        report.MappedRole.ShouldBeNull();
    }

    [Fact]
    public async Task Test_Role_Protected_With_Admin_Token_Returns_Ok()
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(await CreateToken());

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Role_Protected_Without_Role_Returns_Forbidden()
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(await CreateToken(JwtUsers.WithoutRole));

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>Identity claims stay mapped to their .NET types — <c>AccountControllerBase</c> reads them that way.</summary>
    [Fact]
    public async Task Test_Identity_Claims_Stay_Mapped()
    {
        var httpClient = _factory.CreateClient();
        var user = JwtUsers.Admin;
        httpClient.SetBearerToken(await CreateToken(user));

        var report = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        report!.UserId.ShouldBe(user.UserId);
        report.Name.ShouldBe(user.Name);
    }

    /// <summary>
    /// The <c>ClaimsPrincipal</c> helpers the guides tell consumers to use. They read the .NET claim URIs, which
    /// only some claims arrive under — <c>name</c> reaches the principal spelled as the token spells it, so a
    /// helper that looks only at <see cref="ClaimTypes.Name"/> answers null for every JWT principal.
    /// </summary>
    [Fact]
    public async Task Test_Principal_Extensions_Resolve_Every_Identity_Claim()
    {
        var httpClient = _factory.CreateClient();
        var user = JwtUsers.Admin;
        httpClient.SetBearerToken(await CreateToken(user));

        var report = await httpClient.GetFromJsonAsync<ClaimsReport>("protected/claims");

        report!.FoundUserId.ShouldBe(user.UserId);
        report.FoundUserName.ShouldBe(user.Name);
        report.FoundEmail.ShouldBe(user.Email);
    }

    async Task<string> CreateToken(JwtUser? user = null)
    {
        var httpClient = _factory.CreateClient();
        user ??= JwtUsers.Admin;
        var response = await httpClient.PostAsJsonAsync("auth", new { username = user.Name });
        var tokenResult = await response.Content.ReadFromJsonAsync<TokenResult>();
        return tokenResult!.Token!;
    }
}