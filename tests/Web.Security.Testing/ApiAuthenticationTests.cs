using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Regira.Security.Authentication.ApiKey.Models;
using Shouldly;
using System.Net;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.ApiKey;
using Xunit;

namespace Web.Security.Testing;

public class ApiAuthenticationTests : IClassFixture<TestingWebApplicationFactory<ApiKeyStartup>>
{
    private readonly WebApplicationFactory<ApiKeyStartup> _factory;
    public ApiAuthenticationTests(TestingWebApplicationFactory<ApiKeyStartup> factory)
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
    public async Task Test_Protected_Without_ApiKey_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Protected_With_Valid_ApiKey_Returns_Ok()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Protected_With_Empty_ApiKey_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, string.Empty);

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Protected_With_Invalid_ApiKey_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, "Bad Key");

        var response = await httpClient.GetAsync("protected");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Protected_Me_Returns_OwnerId()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await httpClient.GetAsync("protected/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain(ApiKeyOwners.Default.OwnerId);
    }

    [Fact]
    public async Task Test_AdminOnly_Without_ApiKey_Returns_Unauthorized()
    {
        var httpClient = _factory.CreateClient();

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_AdminOnly_With_NonAdmin_ApiKey_Returns_Forbidden()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Test_AdminOnly_With_Admin_ApiKey_Returns_Ok()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.WithAdminRole.Key);

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_AdminOnly_With_MultiRole_ApiKey_Returns_Ok()
    {
        var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.WithRolesAndClaims.Key);

        var response = await httpClient.GetAsync("protected/admin");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}