using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Web.Models;
using Regira.Utilities;
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

        // the access token past its lifespan no longer authenticates
        var expired = ExpiredTokens.Of(signedIn.Token!, _withRefresh.Services);
        var bearer = _withRefresh.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new("Bearer", expired);
        var validate = await bearer.PostAsync("auth/validate", null);
        validate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        validate.Headers.WwwAuthenticate.ToString().ShouldContain("The token expired");

        // the expired bearer comes along, as a client that attaches it to every call sends it
        var response = await bearer.PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var refreshed = (await response.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;
        refreshed.Token.ShouldNotBeNullOrWhiteSpace();
        refreshed.RefreshToken.ShouldNotBe(signedIn.RefreshToken);
        var renewed = _withRefresh.CreateClient();
        renewed.DefaultRequestHeaders.Authorization = new("Bearer", refreshed.Token);
        (await renewed.PostAsync("auth/validate", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent, "the renewed access token authenticates");
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

    /// <summary>
    /// The exchange re-reads the user as sign-in does: an address changed since sign-in is unconfirmed again, and
    /// <c>SignIn.RequireConfirmedEmail</c> refuses it a renewal too.
    /// </summary>
    [Fact]
    public async Task Test_Unconfirmed_Address_Is_Refused_When_Required()
    {
        const string username = "refresh-unconfirmed";
        var requiring = _withRefresh.WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.PostConfigure<Microsoft.AspNetCore.Identity.IdentityOptions>(o => o.SignIn.RequireConfirmedEmail = true)));
        using (var scope = requiring.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TestUser>>();
            if (await userManager.FindByNameAsync(username) == null)
            {
                (await userManager.CreateAsync(new TestUser { UserName = username, Email = "refresh-unconfirmed@example.com", EmailConfirmed = true }, Password))
                    .Succeeded.ShouldBeTrue();
            }
        }
        var signIn = await requiring.CreateClient().PostAsJsonAsync("auth?clientApp=test", new { username, password = Password });
        signIn.StatusCode.ShouldBe(HttpStatusCode.OK);
        var signedIn = (await signIn.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;

        // while the address is confirmed, the renewal works
        var renewal = await requiring.CreateClient().PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });
        renewal.StatusCode.ShouldBe(HttpStatusCode.OK);
        var renewed = (await renewal.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;

        using (var scope = requiring.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TestUser>>();
            var user = await userManager.FindByNameAsync(username);
            (await userManager.SetEmailAsync(user!, "refresh-changed@example.com")).Succeeded.ShouldBeTrue();
        }
        var response = await requiring.CreateClient().PostAsJsonAsync("auth/refresh-token", new { refreshToken = renewed.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// A new password ends the sessions the old one opened: a refresh token stolen before the change, or before an
    /// account recovery, renews nothing after it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Test_A_New_Password_Revokes_The_Refresh_Tokens(bool byReset)
    {
        var username = $"refresh-password-{Guid.NewGuid():N}";
        using (var scope = _withRefresh.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TestUser>>();
            (await userManager.CreateAsync(new TestUser { UserName = username, Email = $"{username}@example.com" }, Password)).Succeeded.ShouldBeTrue();
        }
        var signIn = await _withRefresh.CreateClient().PostAsJsonAsync("auth?clientApp=test", new { username, password = Password });
        var signedIn = (await signIn.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!;

        var client = _withRefresh.CreateClient();
        if (byReset)
        {
            using var scope = _withRefresh.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<TestUser>>();
            var resetToken = await userManager.GeneratePasswordResetTokenAsync((await userManager.FindByNameAsync(username))!);
            var token = JsonSerializer.Serialize(new UserTokenModel { Token = resetToken, Username = username }).Base64Encode();
            (await client.PostAsJsonAsync("auth/password/reset", new { token, password = "Pass2" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        else
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", signedIn.Token);
            (await client.PostAsJsonAsync("auth/password", new { currentPassword = Password, newPassword = "Pass2" })).StatusCode.ShouldBe(HttpStatusCode.OK);
        }
        var response = await _withRefresh.CreateClient().PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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

    /// <summary>
    /// ⚠️ A JWT is readable by whoever holds it, and Identity's security stamp is the secret its email and phone token
    /// providers derive their one-time codes from — so no minted token may carry it, on any path that mints one.
    /// </summary>
    [Fact]
    public async Task Test_Sign_In_Token_Carries_No_Security_Stamp()
    {
        await CreateUser(_withoutRefresh);
        var response = await _withoutRefresh.CreateClient()
            .PostAsJsonAsync("auth?clientApp=test", new { username = Username, password = Password });
        var token = (await response.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!.Token!;

        ShouldCarryNoSecurityStamp(token);
    }

    [Fact]
    public async Task Test_Renewed_Tokens_Carry_No_Security_Stamp()
    {
        await CreateUser(_withRefresh);
        var signedIn = await SignIn();
        ShouldCarryNoSecurityStamp(signedIn.Token!);

        // auth/refresh-token: the anonymous exchange of a refresh token
        var exchanged = await _withRefresh.CreateClient()
            .PostAsJsonAsync("auth/refresh-token", new { refreshToken = signedIn.RefreshToken });
        ShouldCarryNoSecurityStamp((await exchanged.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!.Token!);

        // auth/refresh: the renewal of a still-valid bearer
        var bearer = _withRefresh.CreateClient();
        bearer.DefaultRequestHeaders.Authorization = new("Bearer", signedIn.Token);
        var renewed = await bearer.PostAsync("auth/refresh", null);
        renewed.StatusCode.ShouldBe(HttpStatusCode.OK);
        ShouldCarryNoSecurityStamp((await renewed.Content.ReadFromJsonAsync<AuthenticateResponseDto>())!.Token!);
    }

    private static void ShouldCarryNoSecurityStamp(string jwt)
    {
        var payload = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var claims = JsonDocument.Parse(Convert.FromBase64String(payload)).RootElement;

        claims.TryGetProperty(new Microsoft.AspNetCore.Identity.ClaimsIdentityOptions().SecurityStampClaimType, out _).ShouldBeFalse();
        // the user's identity is still there: only the stamp is left out
        claims.TryGetProperty("sub", out _).ShouldBeTrue();
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
