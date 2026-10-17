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
using System.Text.Json;
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

    // the body must not tell which user names exist
    [Fact]
    public async Task Authenticate_UnknownUser_AnswersAsAWrongPasswordDoes()
    {
        const string username = "auth-known@test.local";
        await SeedUser(username, "pass1");
        var client = _factory.CreateClient();

        var wrongPassword = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "wrong" });
        var unknownUser = await client.PostAsJsonAsync("auth?clientApp=test", new { username = "auth-unknown@test.local", password = "wrong" });

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        unknownUser.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await unknownUser.Content.ReadAsStringAsync()).ShouldBe(await wrongPassword.Content.ReadAsStringAsync());
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

    // An app that sets SignIn.RequireConfirmedEmail must not hand a token to an address nobody confirmed. The check
    // comes after the password, so only a caller who knows it learns why it was refused.
    [Fact]
    public async Task Authenticate_UnconfirmedAddress_WhenRequired_AnswersNotAllowed()
    {
        const string username = "unconfirmed@test.local";
        var factory = RequiringConfirmedEmail();
        await SeedUser(username, "pass1", factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isNotAllowed").GetBoolean().ShouldBeTrue();
        body.RootElement.TryGetProperty("token", out var token).ShouldBeTrue();
        token.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Authenticate_UnconfirmedAddress_WithWrongPassword_CountsTheFailureAndSaysNothingMore()
    {
        const string username = "unconfirmed-wrong@test.local";
        var factory = RequiringConfirmedEmail();
        await SeedUser(username, "pass1", factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "wrong" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("isNotAllowed", out _).ShouldBeFalse();
        (await GetAccessFailedCount(username, factory)).ShouldBe(1);
    }

    [Fact]
    public async Task Authenticate_ConfirmedAddress_WhenRequired_ReturnsToken()
    {
        const string username = "confirmed@test.local";
        var factory = RequiringConfirmedEmail();
        await SeedUser(username, "pass1", factory, emailConfirmed: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AuthResult>())!.Token.ShouldNotBeNullOrWhiteSpace();
    }

    // SignIn.RequireConfirmedAccount, which the Identity templates set, asks the registered IUserConfirmation; the
    // default one checks the address.
    [Fact]
    public async Task Authenticate_UnconfirmedAccount_WhenRequired_AnswersNotAllowed()
    {
        const string username = "unconfirmed-account@test.local";
        var factory = RequiringConfirmed(o => o.RequireConfirmedAccount = true);
        await SeedUser(username, "pass1", factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isNotAllowed").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task Authenticate_ConfirmedAccount_WhenRequired_ReturnsToken()
    {
        const string username = "confirmed-account@test.local";
        var factory = RequiringConfirmed(o => o.RequireConfirmedAccount = true);
        await SeedUser(username, "pass1", factory, emailConfirmed: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticate_RequireConfirmedAccount_AsksTheRegisteredConfirmation()
    {
        const string username = "custom-confirmation@test.local";
        var factory = RequiringConfirmed(o => o.RequireConfirmedAccount = true)
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<IUserConfirmation<TestUser>, ConfirmsEveryone>()));
        await SeedUser(username, "pass1", factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticate_UnconfirmedPhoneNumber_WhenRequired_AnswersNotAllowed()
    {
        const string username = "unconfirmed-phone@test.local";
        var factory = RequiringConfirmed(o => o.RequireConfirmedPhoneNumber = true);
        await SeedUser(username, "pass1", factory, emailConfirmed: true);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password = "pass1" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("isNotAllowed").GetBoolean().ShouldBeTrue();
    }

    private class ConfirmsEveryone : IUserConfirmation<TestUser>
    {
        public Task<bool> IsConfirmedAsync(UserManager<TestUser> manager, TestUser user) => Task.FromResult(true);
    }

    // --- refresh --------------------------------------------------------------------------------

    // a client that renews its still-valid token before each expiry must not outlive the lockout of its account
    [Fact]
    public async Task Refresh_LockedOutUser_ReturnsUnauthorized()
    {
        const string username = "refresh-locked@test.local";
        await SeedUser(username, "pass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, username, "pass1"));
        (await client.PostAsync("auth/refresh", null)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using (var scope = _factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var user = await userManager.FindByNameAsync(username);
            (await userManager.SetLockoutEndDateAsync(user!, DateTimeOffset.UtcNow.AddHours(1))).Succeeded.ShouldBeTrue();
        }
        var response = await client.PostAsync("auth/refresh", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.Content.ReadFromJsonAsync<AuthResult>())!.IsAuthenticated.ShouldBeFalse();
    }

    // an address changed since sign-in is unconfirmed again, and SignIn.RequireConfirmedEmail refuses it a renewal too
    [Fact]
    public async Task Refresh_UnconfirmedAddress_WhenRequired_ReturnsUnauthorized()
    {
        const string username = "refresh-unconfirmed@test.local";
        var factory = RequiringConfirmedEmail();
        await SeedUser(username, "pass1", factory, emailConfirmed: true);
        var client = factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, username, "pass1"));

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
            var user = await userManager.FindByNameAsync(username);
            (await userManager.SetEmailAsync(user!, "refresh-changed@test.local")).Succeeded.ShouldBeTrue();
        }
        var response = await client.PostAsync("auth/refresh", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
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
    public async Task ResetPassword_WithTokenWithoutUserName_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = """{"Token":"x"}""".Base64Encode();

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
    public async Task ConfirmEmail_WithTokenWithoutUserName_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();
        var token = """{"Token":"x"}""".Base64Encode();

        var response = await client.PostAsJsonAsync("users/confirm-email", new { token });

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

    // The user comes from the token, so the confirm page posts the link's token alone.
    [Fact]
    public async Task ConfirmEmail_WithoutUserName_Confirms()
    {
        const string admin = "creator-tokenonly@test.local";
        await SeedUser(admin, "AdminPass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, admin, "AdminPass1"));

        const string newUser = "created-tokenonly@test.local";
        var create = await client.PostAsJsonAsync("users",
            new { username = newUser, password = "Created1", confirmEmailUrl = "https://app.example.com/confirm-email" });
        create.StatusCode.ShouldBe(HttpStatusCode.OK);
        var token = LinkToken(ExtractLink(SentEmails().Last(x => x.Email == newUser).Body));

        var confirm = await _factory.CreateClient().PostAsJsonAsync("users/confirm-email", new { token });

        confirm.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await FindUser(newUser))!.EmailConfirmed.ShouldBeTrue();
    }

    // a taken name is refused, so the caller does not take the existing account for one it made
    [Fact]
    public async Task CreateUser_WithTakenName_AnswersBadRequestAndKeepsTheExistingUser()
    {
        const string admin = "creator-taken@test.local";
        await SeedUser(admin, "AdminPass1");
        const string existing = "taken@test.local";
        await SeedUser(existing, "Existing1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, admin, "AdminPass1"));

        var create = await client.PostAsJsonAsync("users", new { username = existing, password = "Created1" });

        create.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var body = System.Text.Json.JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        body.RootElement.TryGetProperty("DuplicateUserName", out _).ShouldBeTrue();
        (await client.PostAsJsonAsync("auth?clientApp=test", new { username = existing, password = "Existing1" }))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
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

    // the three 400 bodies the guide describes
    [Fact]
    public async Task BadRequest_Bodies_Take_Three_Shapes()
    {
        var client = _factory.CreateClient();

        var invalidInput = await client.PostAsJsonAsync("auth/password/reset", new { });
        var foreignPage = await client.PostAsJsonAsync("auth/password/recover",
            new { username = "nobody@test.local", siteUrl = "https://attacker.example/reset" });
        var malformedToken = await client.PostAsJsonAsync("auth/password/reset",
            new { token = "not-a-valid-base64-token!!", password = "whatever1" });

        using var validation = System.Text.Json.JsonDocument.Parse(await invalidInput.Content.ReadAsStringAsync());
        validation.RootElement.TryGetProperty("errors", out _).ShouldBeTrue("model validation answers ValidationProblemDetails");
        using var fieldErrors = System.Text.Json.JsonDocument.Parse(await foreignPage.Content.ReadAsStringAsync());
        fieldErrors.RootElement.TryGetProperty("SiteUrl", out _).ShouldBeTrue("BadRequest(ModelState) answers the field errors as the body");
        using var problem = System.Text.Json.JsonDocument.Parse(await malformedToken.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("status").GetInt32().ShouldBe(400);
        problem.RootElement.TryGetProperty("errors", out _).ShouldBeFalse("BadRequest() answers a ProblemDetails without field errors");
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
    public async Task RecoverPassword_Subject_KeepsTheSiteNameAsWritten()
    {
        var factory = WithAccountEmails(o => o.SiteName = "ExampleHub");
        const string username = "recover-subject-casing@test.local";
        await SeedUser(username, "OldPass1", factory);
        var client = factory.CreateClient();

        var recover = await client.PostAsJsonAsync("auth/password/recover",
            new { username, siteUrl = "https://app.example.com/reset-password" });
        recover.StatusCode.ShouldBe(HttpStatusCode.OK);

        var email = factory.Services.GetRequiredService<SentEmailStore>().Last;
        email.ShouldNotBeNull();
        email.Subject.ShouldBe("ExampleHub Password Reset");
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

    // A host whose Identity options require a confirmed address before sign-in.
    private WebApplicationFactory<IdentityStartup> RequiringConfirmedEmail()
        => RequiringConfirmed(o => o.RequireConfirmedEmail = true);

    // A host whose Identity sign-in options require what `require` sets.
    private WebApplicationFactory<IdentityStartup> RequiringConfirmed(Action<SignInOptions> require)
        => _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<IdentityOptions>(o => require(o.SignIn))));

    private async Task SeedUser(string username, string password, WebApplicationFactory<IdentityStartup>? factory = null, bool emailConfirmed = false)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        if (await userManager.FindByNameAsync(username) != null)
        {
            return;
        }
        var user = new TestUser { UserName = username, Email = username, EmailConfirmed = emailConfirmed };
        var result = await userManager.CreateAsync(user, password);
        result.Succeeded.ShouldBeTrue(result.Errors.FirstOrDefault()?.Description);
    }

    private async Task<int> GetAccessFailedCount(string username, WebApplicationFactory<IdentityStartup>? factory = null)
    {
        using var scope = (factory ?? _factory).Services.CreateScope();
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
