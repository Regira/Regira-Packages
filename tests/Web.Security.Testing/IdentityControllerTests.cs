using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Web.Models;
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
// malformed-token guards, the lockout-counter reset, the recover/reset & create/confirm flows, and the origins
// their mailed links may point at.
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
            new { username, siteUrl = "https://app.example.com/reset-password" });
        recover.StatusCode.ShouldBe(HttpStatusCode.OK);

        var email = LastEmail();
        email.ShouldNotBeNull();
        email.Email.ShouldBe(username);
        var token = ExtractToken(email.Body);
        var link = ExtractLink(email.Body);
        link.GetLeftPart(UriPartial.Path).ShouldBe("https://app.example.com/reset-password");
        LinkToken(link).ShouldBe(token);

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
            new { username = newUser, password = "Created1", confirmEmailUrl = "https://app.example.com/confirm-email" });
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FindUser(newUser)).ShouldNotBeNull();

        // the confirmation email carries a token; posting it confirms the address
        var email = LastEmail();
        email.ShouldNotBeNull();
        email.Email.ShouldBe(newUser);
        var token = ExtractToken(email.Body);
        var link = ExtractLink(email.Body);
        link.GetLeftPart(UriPartial.Path).ShouldBe("https://app.example.com/confirm-email");
        LinkToken(link).ShouldBe(token);

        var confirm = await client.PostAsJsonAsync("users/confirm-email", new { token, userName = newUser });
        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);

        var confirmed = await FindUser(newUser);
        confirmed!.EmailConfirmed.ShouldBeTrue();
    }

    // --- emailed links ------------------------------------------------------------------------

    // The caller names the page a link opens, so anyone could have the application mail a genuine reset token
    // inside a link to their own host. Only a page on an origin the application allows may receive one.
    [Theory]
    [InlineData("https://attacker.example/reset-password")]
    [InlineData("https://app.example.com.attacker.example/reset-password")]
    [InlineData("https://app.example.com@attacker.example/reset-password")]
    [InlineData("https://user@app.example.com/reset-password")]
    [InlineData("http://app.example.com/reset-password")]
    [InlineData("https://app.example.com:8443/reset-password")]
    [InlineData("//attacker.example/reset-password")]
    [InlineData("/reset-password")]
    public async Task RecoverPassword_ToDisallowedPage_SendsNoMail(string siteUrl)
    {
        const string username = "recover-foreign@test.local";
        await SeedUser(username, "OldPass1");
        var client = _factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username, siteUrl, siteName = "Example" });

        SentEmails().ShouldBeEmpty("a reset token must not be mailed in a link to a foreign origin");
        recover.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task RecoverPassword_ForUnknownUser_ToDisallowedPage_AnswersAsForAKnownOne()
    {
        var client = _factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username = "nobody@test.local", siteUrl = "https://attacker.example/reset-password" });

        recover.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        SentEmails().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("https://app.example.com/reset-password")]
    [InlineData("https://APP.Example.com/reset-password")]
    [InlineData("https://app.example.com:443/reset-password")]
    public async Task RecoverPassword_ToPageOnAllowedOrigin_MailsTheLink(string siteUrl)
    {
        const string username = "recover-allowed@test.local";
        await SeedUser(username, "OldPass1");
        var client = _factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover", new { username, siteUrl });

        recover.StatusCode.ShouldBe(HttpStatusCode.OK);
        var email = LastEmail();
        email.ShouldNotBeNull();
        var link = ExtractLink(email.Body);
        link.GetLeftPart(UriPartial.Path).ShouldBe("https://app.example.com/reset-password");
        LinkToken(link).ShouldBe(ExtractToken(email.Body));
    }

    // The token is Base64, and a user name that may hold '~' or '?' puts '+' and '/' in it. A client reads the link's
    // query with a parser that takes '+' for a space (URLSearchParams, vue-router, QueryHelpers), so the link carries
    // the token percent-encoded.
    [Fact]
    public async Task RecoverPassword_TokenHoldingPlusAndSlash_SurvivesTheLinksQuery()
    {
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.Configure<IdentityOptions>(o => o.User.AllowedUserNameCharacters += "~?")));
        const string username = "tilde~~~question???@test.local";
        await SeedUser(username, "OldPass1", factory);
        var client = factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username, siteUrl = "https://app.example.com/reset-password" });
        recover.StatusCode.ShouldBe(HttpStatusCode.OK);

        var email = factory.Services.GetRequiredService<SentEmailStore>().Last;
        email.ShouldNotBeNull();
        var token = ExtractToken(email.Body);
        token.ShouldContain("+");
        token.ShouldContain("/");
        var linkToken = LinkToken(ExtractLink(email.Body));
        linkToken.ShouldBe(token);

        // the token as a client reads it from the link resets the password
        (await client.PostAsJsonAsync("auth/password/reset", new { token = linkToken, password = "NewPass2" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "NewPass2" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CreateUser_WithForeignConfirmEmailUrl_SendsNoMailAndCreatesNoUser()
    {
        const string admin = "creator-foreign@test.local";
        await SeedUser(admin, "AdminPass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, admin, "AdminPass1"));

        const string newUser = "created-foreign@test.local";
        var create = await client.PostAsJsonAsync("users",
            new { username = newUser, password = "Created1", confirmEmailUrl = "https://attacker.example/confirm-email" });

        SentEmails().ShouldBeEmpty("a confirmation token must not be mailed in a link to a foreign origin");
        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FindUser(newUser)).ShouldBeNull();
    }

    [Fact]
    public async Task RecoverPassword_Subject_IsNotTheCallers()
    {
        const string username = "recover-subject@test.local";
        await SeedUser(username, "OldPass1");
        var client = _factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username, siteUrl = "https://app.example.com/reset-password", siteName = "Your account is locked, call 555-0100 for" });
        recover.StatusCode.ShouldBe(HttpStatusCode.OK);

        var email = LastEmail();
        email.ShouldNotBeNull();
        email.Subject.ShouldNotContain("555-0100");
        email.Subject.ShouldBe("Example Password Reset");
    }

    [Fact]
    public async Task RecoverPassword_WithoutAllowedOrigins_Throws()
    {
        var factory = WithAccountEmails(o => o.AllowedOrigins.Clear());
        var client = factory.CreateClient();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => client.PostAsJsonAsync("auth/password/recover",
            new { username = "recover-unconfigured@test.local", siteUrl = "https://app.example.com/reset-password" }));

        exception.Message.ShouldContain(nameof(AccountEmailOptions.AllowedOrigins));
        factory.Services.GetRequiredService<SentEmailStore>().Messages.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecoverPassword_WithAllowedOriginThatIsNoUrl_Throws()
    {
        var factory = WithAccountEmails(o => o.AllowedOrigins.Add("app.example.com"));
        var client = factory.CreateClient();

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => client.PostAsJsonAsync("auth/password/recover",
            new { username = "recover-misconfigured@test.local", siteUrl = "https://app.example.com/reset-password" }));

        exception.Message.ShouldContain("'app.example.com'");
    }

    [Fact]
    public async Task CreateUser_WithoutConfirmEmailUrl_NeedsNoAllowedOrigins()
    {
        var factory = WithAccountEmails(o => o.AllowedOrigins.Clear());
        const string admin = "creator-unconfigured@test.local";
        await SeedUser(admin, "AdminPass1", factory);
        var client = factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, admin, "AdminPass1"));

        const string newUser = "created-unconfigured@test.local";
        var create = await client.PostAsJsonAsync("users", new { username = newUser, password = "Created1" });

        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FindUser(newUser, factory)).ShouldNotBeNull();
        factory.Services.GetRequiredService<SentEmailStore>().Messages.ShouldBeEmpty();
    }

    // --- helpers --------------------------------------------------------------------------------

    private async Task<string> Authenticate(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token!;
    }

    // A host whose AccountEmailOptions are changed after the startup configured them.
    private WebApplicationFactory<IdentityStartup> WithAccountEmails(Action<AccountEmailOptions> change)
        => _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure(change)));

    private async Task SeedUser(string username, string password, WebApplicationFactory<IdentityStartup>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
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

    private async Task<TestUser?> FindUser(string username, WebApplicationFactory<IdentityStartup>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        return await userManager.FindByNameAsync(username);
    }

    private SentEmail? LastEmail()
        => _factory.Services.GetRequiredService<SentEmailStore>().Last;

    private IReadOnlyCollection<SentEmail> SentEmails()
        => _factory.Services.GetRequiredService<SentEmailStore>().Messages;

    private static string ExtractToken(string body)
    {
        var match = Regex.Match(body, @"Token:\s*(\S+)");
        match.Success.ShouldBeTrue($"expected a 'Token:' line in the email body but got:\n{body}");
        return match.Groups[1].Value;
    }

    private static Uri ExtractLink(string body)
    {
        var match = Regex.Match(body, @"https?://\S+");
        match.Success.ShouldBeTrue($"expected a link in the email body but got:\n{body}");
        return new Uri(match.Value);
    }

    // The token as a client reads it from the link's query string.
    private static string LinkToken(Uri link) => QueryHelpers.ParseQuery(link.Query)["token"].ToString();

    private record AuthResult
    {
        public bool IsAuthenticated { get; init; }
        public string? Token { get; init; }
    }
}
