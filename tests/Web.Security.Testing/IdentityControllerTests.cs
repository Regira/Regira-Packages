using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Utilities;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

// Exercises the AccountControllerBase / PasswordControllerBase / UserControllerBase endpoints over a
// real Identity + EF-in-memory stack. Covers the guarded-by-default authorization model, the
// malformed-token guards, the lockout-counter reset, and the recover/reset & create/confirm flows.
public class IdentityControllerTests : IClassFixture<TestingWebApplicationFactory<IdentityStartup>>
{
    private readonly WebApplicationFactory<IdentityStartup> _factory;

    public IdentityControllerTests(TestingWebApplicationFactory<IdentityStartup> factory)
    {
        _factory = factory
            // otherwise throws DirectoryNotFound Exception
            .WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    // --- authentication -------------------------------------------------------------------------

    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsToken()
    {
        const string username = "auth-ok@test.local";
        await SeedUser(username, "pass1");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        result!.IsAuthenticated.ShouldBeTrue();
        result.Token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Authenticate_WithWrongPassword_ReturnsUnauthorized()
    {
        const string username = "auth-bad@test.local";
        await SeedUser(username, "pass1");
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "wrong" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticate_OnSuccess_ResetsAccessFailedCount()
    {
        const string username = "reset-count@test.local";
        const string password = "pass1";
        await SeedUser(username, password);
        var client = _factory.CreateClient();

        // a failed attempt bumps the counter
        var bad = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "wrong" });
        bad.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await GetAccessFailedCount(username)).ShouldBe(1);

        // a successful login clears it
        var ok = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password });
        ok.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetAccessFailedCount(username)).ShouldBe(0);
    }

    // --- guarded-by-default ---------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth/password", new { currentPassword = "a", newPassword = "b" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateUser_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("users", new { username = "nope@test.local", password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- change password ------------------------------------------------------------------------

    [Fact]
    public async Task ChangePassword_WithToken_ChangesPassword()
    {
        const string username = "change-pwd@test.local";
        await SeedUser(username, "OldPass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, username, "OldPass1"));

        var response = await client.PostAsJsonAsync("auth/password",
            new { currentPassword = "OldPass1", newPassword = "NewPass2" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // the new password now works and the old one no longer does
        var fresh = _factory.CreateClient();
        (await fresh.PostAsJsonAsync("auth?clientApp=test", new { username, password = "NewPass2" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await fresh.PostAsJsonAsync("auth?clientApp=test", new { username, password = "OldPass1" }))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- reset password -------------------------------------------------------------------------

    [Fact]
    public async Task ResetPassword_WithMalformedBase64Token_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth/password/reset",
            new { token = "not-a-valid-base64-token!!", password = "whatever1" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithValidBase64ButInvalidJson_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = "this is not json".Base64Encode();

        var response = await client.PostAsJsonAsync("auth/password/reset",
            new { token, password = "whatever1" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecoverAndResetPassword_EndToEnd_Succeeds()
    {
        const string username = "recover@test.local";
        await SeedUser(username, "OldPass1");
        var client = _factory.CreateClient();

        // recover always returns 200 (no user enumeration) and sends an email carrying the token
        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username, siteUrl = "https://example.com/reset", siteName = "Example" });
        recover.StatusCode.ShouldBe(HttpStatusCode.OK);

        var email = LastEmail();
        email.ShouldNotBeNull();
        email.Email.ShouldBe(username);
        var token = ExtractToken(email.Body);

        var reset = await client.PostAsJsonAsync("auth/password/reset", new { token, password = "NewPass2" });
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);

        // the new password now authenticates
        (await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "NewPass2" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // --- users / confirm email ------------------------------------------------------------------

    [Fact]
    public async Task ConfirmEmail_WithMalformedToken_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("users/confirm-email",
            new { token = "not-a-valid-base64-token!!", userName = "someone" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateUser_ThenConfirmEmail_EndToEnd_Succeeds()
    {
        const string admin = "creator@test.local";
        await SeedUser(admin, "AdminPass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, admin, "AdminPass1"));

        const string newUser = "created@test.local";
        var create = await client.PostAsJsonAsync("users",
            new { username = newUser, password = "Created1", confirmEmailUrl = "https://example.com/confirm" });
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FindUser(newUser)).ShouldNotBeNull();

        // the confirmation email carries a token; posting it confirms the address
        var email = LastEmail();
        email.ShouldNotBeNull();
        email.Email.ShouldBe(newUser);
        var token = ExtractToken(email.Body);

        var confirm = await client.PostAsJsonAsync("users/confirm-email", new { token, userName = newUser });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);

        var confirmed = await FindUser(newUser);
        confirmed!.EmailConfirmed.ShouldBeTrue();
    }

    // --- helpers --------------------------------------------------------------------------------

    private async Task<string> Authenticate(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token!;
    }

    private async Task SeedUser(string username, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        if (await userManager.FindByNameAsync(username) != null)
        {
            return;
        }
        var user = new TestUser { UserName = username, Email = username };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(result.Errors.FirstOrDefault()?.Description);
    }

    private async Task<int> GetAccessFailedCount(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = await userManager.FindByNameAsync(username);
        return await userManager.GetAccessFailedCountAsync(user!);
    }

    private async Task<TestUser?> FindUser(string username)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        return await userManager.FindByNameAsync(username);
    }

    private SentEmail? LastEmail()
        => _factory.Services.GetRequiredService<SentEmailStore>().Last;

    private static string ExtractToken(string body)
    {
        var match = Regex.Match(body, @"Token:\s*(\S+)");
        match.Success.ShouldBeTrue($"expected a 'Token:' line in the email body but got:\n{body}");
        return match.Groups[1].Value;
    }

    private record AuthResult
    {
        public bool IsAuthenticated { get; init; }
        public string? Token { get; init; }
    }
}
