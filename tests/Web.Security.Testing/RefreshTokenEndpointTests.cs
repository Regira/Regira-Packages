using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Web.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// The HTTP surface, and specifically the promise that a host which has not opted in sees no change at all.
/// <see cref="IdentityStartup"/> registers no refresh-token service; <see cref="RefreshTokenStartup"/> does.
/// </summary>
public class RefreshTokenEndpointTests : IClassFixture<TestingWebApplicationFactory<RefreshTokenStartup>>, IClassFixture<TestingWebApplicationFactory<IdentityStartup>>
{
    private const string Username = "refresh-user";
    private const string Password = "Pass1";

    private readonly WebApplicationFactory<RefreshTokenStartup> _withRefresh;
    private readonly WebApplicationFactory<IdentityStartup> _withoutRefresh;

    public RefreshTokenEndpointTests(TestingWebApplicationFactory<RefreshTokenStartup> withRefresh, TestingWebApplicationFactory<IdentityStartup> withoutRefresh)
    {
        _withRefresh = withRefresh.WithWebHostBuilder(b => b.UseSolutionRelativeContentRoot("tests"));
        _withoutRefresh = withoutRefresh.WithWebHostBuilder(b => b.UseSolutionRelativeContentRoot("tests"));
    }

    /// <summary>
    /// ⚠️ The compatibility guarantee. Without a registered service the endpoint answers <c>404</c> — not <c>500</c>,
    /// and not a working anonymous endpoint that nobody asked for.
    /// </summary>
    [Fact]
    public async Task Test_Endpoint_Is_Absent_When_Refresh_Tokens_Are_Not_Registered()
    {
        var response = await _withoutRefresh.CreateClient()
            .PostAsJsonAsync("auth/refresh-token", new { refreshToken = "anything" });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    /// <summary>And sign-in returns the same body it always did — the field is absent, not null.</summary>
    [Fact]
    public async Task Test_Sign_In_Response_Is_Unchanged_When_Refresh_Tokens_Are_Not_Registered()
    {
        await CreateUser(_withoutRefresh);

        var response = await _withoutRefresh.CreateClient()
            .PostAsJsonAsync("auth?clientApp=test", new { username = Username, password = Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.TryGetProperty("refreshToken", out _).ShouldBeFalse();
        json.RootElement.TryGetProperty("expiresAt", out _).ShouldBeFalse();
        json.RootElement.GetProperty("token").GetString().ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Test_Sign_In_Returns_A_Refresh_Token_When_Registered()
    {
        await CreateUser(_withRefresh);

        var result = await SignIn();

        result.Token.ShouldNotBeNullOrWhiteSpace();
        result.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        result.ExpiresAt!.Value.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// ⚠️ The whole point: a refresh works <b>after the access token has expired</b>. The pre-existing
    /// <c>auth/refresh</c> requires a still-valid bearer, so it cannot serve this — which is the gap this endpoint
    /// closes.
    /// </summary>
    [Fact]
    public async Task Test_Refresh_Works_After_The_Access_Token_Expired()
    {
        await CreateUser(_withRefresh);
        var signedIn = await SignIn();

        // The access token's lifespan is 2 seconds in this host.
        await Task.Delay(2500);

        var httpClient = _withRefresh.CreateClient();
        var response = await httpClient.PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;
        refreshed.Token.ShouldNotBeNullOrWhiteSpace();
        refreshed.Token.ShouldNotBe(signedIn.Token);
        refreshed.RefreshToken.ShouldNotBe(signedIn.RefreshToken);
    }

    [Fact]
    public async Task Test_Replayed_Refresh_Token_Is_Rejected()
    {
        await CreateUser(_withRefresh);
        var signedIn = await SignIn();
        var httpClient = _withRefresh.CreateClient();

        (await httpClient.PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        (await httpClient.PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Unknown_Refresh_Token_Is_Unauthorized()
    {
        var response = await _withRefresh.CreateClient()
            .PostAsJsonAsync("auth/refresh-token", new { refreshToken = "not-a-real-token" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>A missing token is a model-validation failure, not a 500.</summary>
    [Fact]
    public async Task Test_Missing_Refresh_Token_Is_A_Bad_Request()
    {
        var response = await _withRefresh.CreateClient().PostAsJsonAsync("auth/refresh-token", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    /// <summary>The renewed access token authenticates against the API, which is the end-to-end proof.</summary>
    [Fact]
    public async Task Test_Renewed_Access_Token_Is_Accepted()
    {
        await CreateUser(_withRefresh);
        var signedIn = await SignIn();

        var httpClient = _withRefresh.CreateClient();
        var response = await httpClient.PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;

        var authenticated = _withRefresh.CreateClient();
        authenticated.DefaultRequestHeaders.Authorization = new("Bearer", refreshed.Token);
        (await authenticated.PostAsync("auth/validate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<AuthenticateResponseDto> SignIn()
    {
        var response = await _withRefresh.CreateClient()
            .PostAsJsonAsync("auth?clientApp=test", new { username = Username, password = Password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;
    }

    private static async Task CreateUser<TStartup>(WebApplicationFactory<TStartup> factory) where TStartup : class
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TestUser>>();

        if (await userManager.FindByNameAsync(Username) == null)
        {
            await userManager.CreateAsync(new TestUser { UserName = Username, Email = "refresh-user@example.com" }, Password);
        }
    }
}
