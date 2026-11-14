using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Controllers;
using Regira.Security.Authentication.Web.Models;
using Shouldly;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

// The protected steps a subclass of the account and user controllers overrides or calls. The subclasses are nested, so
// MVC maps none of them beside the routes of the test controllers; the tests call their actions directly.
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
