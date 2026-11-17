using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Controllers;
using Regira.Security.Authentication.Web.Models;
using Shouldly;
using System.Security.Claims;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

// The protected steps a subclass of the account, password and user controllers overrides or calls, and what the
// signed-in actions answer in a host that requires no user. The subclasses are nested, so MVC maps none of them beside
// the routes of the test controllers; the tests call their actions directly.
public class AccountControllerHookTests : IClassFixture<TestingWebApplicationFactory<IdentityStartup>>, IClassFixture<TestingWebApplicationFactory<RefreshTokenStartup>>
{
    private readonly WebApplicationFactory<IdentityStartup> _factory;
    private readonly WebApplicationFactory<RefreshTokenStartup> _withRefresh;

    public AccountControllerHookTests(TestingWebApplicationFactory<IdentityStartup> factory, TestingWebApplicationFactory<RefreshTokenStartup> withRefresh)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
        _withRefresh = withRefresh.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    // Slows sign-ins down instead of locking the account: a wrong password is recorded, never counted by Identity.
    private sealed class ThrottledAccountController(ITokenHelper tokenHelper, UserManager<TestUser> userManager,
        IUserClaimsPrincipalFactory<TestUser> claimsFactory, ISet<string> throttled)
        : AccountControllerBase<TestUser>(tokenHelper, userManager, claimsFactory)
    {
        protected override Task<bool> IsBlocked(TestUser user) => Task.FromResult(throttled.Contains(user.UserName!));
        protected override Task OnPasswordRejected(TestUser user)
        {
            throttled.Add(user.UserName!);
            return Task.CompletedTask;
        }
    }

    // The same policy on the password controller: a wrong current password is recorded, never counted by Identity.
    private sealed class ThrottledPasswordController(UserManager<TestUser> userManager, ISet<string> throttled)
        : PasswordControllerBase<TestUser>(userManager)
    {
        protected override Task<bool> IsBlocked(TestUser user) => Task.FromResult(throttled.Contains(user.UserName!));
        protected override Task OnPasswordRejected(TestUser user)
        {
            throttled.Add(user.UserName!);
            return Task.CompletedTask;
        }
    }

    // A sign-up action that fills a column of the user type before it is saved.
    private sealed class SignUpController(UserManager<TestUser> userManager) : UserControllerBase<TestUser>(userManager)
    {
        public Task<IActionResult> SignUp(string email, string password, string phoneNumber, IEmailSender mailer)
            => Create(new TestUser { UserName = email, Email = email, PhoneNumber = phoneNumber }, password, null, mailer);
    }

    private static T WithContext<T>(T controller, IServiceProvider services) where T : ControllerBase
    {
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
        return controller;
    }

    // The principal a validated token gives: authenticated, named by the "name" claim when it has one.
    private static ClaimsPrincipal SignedIn(params Claim[] claims) => new(new ClaimsIdentity(claims, "test", "name", "role"));

    private static async Task<TestUser> Seed(UserManager<TestUser> userManager, string username, string password)
    {
        var user = new TestUser { UserName = username, Email = username };
        (await userManager.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
        return user;
    }

    [Fact]
    public async Task A_Rejected_Password_Runs_The_Override_And_A_Blocked_Account_Is_Refused_Before_Its_Password()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<TestUser>>();
        const string username = "throttled@test.local";
        var user = await Seed(userManager, username, "pass1");
        var throttled = new HashSet<string>();
        var controller = WithContext(new ThrottledAccountController(services.GetRequiredService<ITokenHelper>(), userManager,
            services.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>(), throttled), services);

        var wrong = (ObjectResult)await controller.Authenticate(new AuthenticateInput { Username = username, Password = "wrong" }, "test");
        throttled.ShouldContain(username);
        (await userManager.GetAccessFailedCountAsync(user)).ShouldBe(0);

        var blocked = (ObjectResult)await controller.Authenticate(new AuthenticateInput { Username = username, Password = "pass1" }, "test");

        wrong.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        ((AuthenticateResponseDto)wrong.Value!).IsLockedOut.ShouldBe(false);
        blocked.StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        ((AuthenticateResponseDto)blocked.Value!).IsLockedOut.ShouldBe(true);
    }

    [Fact]
    public async Task A_Rejected_Current_Password_Runs_The_Override_And_A_Blocked_Account_Is_Refused_Before_Its_Password()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<TestUser>>();
        const string username = "throttled-change@test.local";
        var user = await Seed(userManager, username, "pass1");
        var throttled = new HashSet<string>();
        // a controller per request, as MVC makes one: each has its own model state
        ThrottledPasswordController Controller()
        {
            var controller = WithContext(new ThrottledPasswordController(userManager, throttled), services);
            controller.ControllerContext.HttpContext.User = SignedIn(new Claim("name", username));
            return controller;
        }

        var wrong = await Controller().UpdatePassword(new ChangePasswordInput { CurrentPassword = "wrong", NewPassword = "pass2" });
        throttled.ShouldContain(username);
        (await userManager.GetAccessFailedCountAsync(user)).ShouldBe(0);

        var blocked = await Controller().UpdatePassword(new ChangePasswordInput { CurrentPassword = "pass1", NewPassword = "pass2" });

        ((SerializableError)wrong.ShouldBeOfType<BadRequestObjectResult>().Value!).Keys.ShouldBe(["PasswordMismatch"]);
        ((SerializableError)blocked.ShouldBeOfType<BadRequestObjectResult>().Value!).Keys.ShouldBe([PasswordControllerBase<TestUser>.UserLockedOut]);
        (await userManager.CheckPasswordAsync(user, "pass1")).ShouldBeTrue();
    }

    // The bases carry no [Authorize], so a host without a global requirement lets a request without a user reach the
    // signed-in actions: they answer 401, as the requirement would.
    [Fact]
    public async Task The_SignedIn_Actions_Answer_Unauthorized_Without_A_User()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<TestUser>>();
        var password = WithContext(new TestPasswordController(userManager), services);
        var account = WithContext(new TestAccountController(services.GetRequiredService<ITokenHelper>(), userManager,
            services.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>(), NullLogger<TestAccountController>.Instance), services);
        var input = new ChangePasswordInput { CurrentPassword = "pass1", NewPassword = "pass2" };

        var anonymousChange = await password.UpdatePassword(input);
        var anonymousData = await account.GetPersonalData();

        // a signed-in principal that carries neither a name nor an id
        password.ControllerContext.HttpContext.User = SignedIn();
        account.ControllerContext.HttpContext.User = SignedIn();
        var namelessChange = await password.UpdatePassword(input);
        var namelessData = await account.GetPersonalData();

        anonymousChange.ShouldBeOfType<UnauthorizedResult>();
        anonymousData.ShouldBeOfType<UnauthorizedResult>();
        namelessChange.ShouldBeOfType<UnauthorizedResult>();
        namelessData.ShouldBeOfType<UnauthorizedResult>();
    }

    // the refresh routes renew nothing for an account the override blocks, whatever Identity's lockout says
    [Fact]
    public async Task A_Blocked_Account_Renews_Nothing()
    {
        using var scope = _withRefresh.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<TestUser>>();
        const string username = "blocked-refresh@test.local";
        var user = await Seed(userManager, username, "pass1");
        var claims = (await services.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>().CreateAsync(user)).Claims.ToList();
        var pair = await services.GetRequiredService<IRefreshTokenService>().Issue(user.Id, claims, "test");
        var throttled = new HashSet<string>();
        var controller = WithContext(new ThrottledAccountController(services.GetRequiredService<ITokenHelper>(), userManager,
            services.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>(), throttled), services);
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(claims.Append(new System.Security.Claims.Claim("aud", "test")), "test"));

        var allowed = await controller.Refresh();
        throttled.Add(username);
        var refresh = await controller.Refresh();
        var refreshToken = await controller.RefreshToken(new RefreshTokenInput { RefreshToken = pair.RefreshToken });

        allowed.ShouldBeOfType<OkObjectResult>();
        refresh.ShouldBeOfType<UnauthorizedObjectResult>();
        ((ObjectResult)refreshToken).StatusCode.ShouldBe(StatusCodes.Status401Unauthorized);
        (await userManager.IsLockedOutAsync(user)).ShouldBeFalse();
    }

    [Fact]
    public async Task A_Prepared_User_Is_Saved_With_Its_Own_Columns()
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var userManager = services.GetRequiredService<UserManager<TestUser>>();
        const string email = "prepared@test.local";
        var controller = WithContext(new SignUpController(userManager), services);

        var created = await controller.SignUp(email, "Created1", "+32 470 00 00 00", services.GetRequiredService<IEmailSender>());
        var taken = await controller.SignUp(email, "Created1", "+32 470 00 00 01", services.GetRequiredService<IEmailSender>());

        created.ShouldBeOfType<OkResult>();
        taken.ShouldBeOfType<BadRequestObjectResult>();
        (await userManager.FindByNameAsync(email))!.PhoneNumber.ShouldBe("+32 470 00 00 00");
    }
}
