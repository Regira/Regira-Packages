using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Web.Services;
using Shouldly;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

// What AccountControllerBase decides, for a host that maps its own account endpoints: the service is built by DI from
// the request's scope, as `services.AddScoped<AccountSignInService<AppUser>>()` would build it.
public class AccountSignInServiceTests : IClassFixture<TestingWebApplicationFactory<IdentityStartup>>
{
    private readonly WebApplicationFactory<IdentityStartup> _factory;

    public AccountSignInServiceTests(TestingWebApplicationFactory<IdentityStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder
            .UseSolutionRelativeContentRoot("tests")
            .ConfigureTestServices(services => services.PostConfigure<IdentityOptions>(o => o.SignIn.RequireConfirmedEmail = true)));
    }

    [Fact]
    public async Task CanSignIn_FollowsTheSignInOptions()
    {
        using var scope = _factory.Services.CreateScope();
        var (service, userManager) = Resolve(scope);

        var unconfirmed = await Seed(userManager, "signin-unconfirmed@test.local", emailConfirmed: false);
        var confirmed = await Seed(userManager, "signin-confirmed@test.local", emailConfirmed: true);

        (await service.CanSignIn(unconfirmed)).ShouldBeFalse();
        (await service.CanSignIn(confirmed)).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateSuccessResponse_IssuesAToken_WithoutTheSecurityStamp()
    {
        using var scope = _factory.Services.CreateScope();
        var (service, userManager) = Resolve(scope);
        var user = await Seed(userManager, "signin-token@test.local", emailConfirmed: true);
        var claims = (await scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>().CreateAsync(user)).Claims.ToArray();
        var stampType = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.ClaimsIdentity.SecurityStampClaimType;

        var response = await service.CreateSuccessResponse(claims, "test", user.Id);

        response.IsAuthenticated.ShouldBeTrue();
        response.Token.ShouldNotBeNullOrWhiteSpace();
        claims.ShouldContain(x => x.Type == stampType);
        service.TokenClaims(claims).ShouldNotContain(x => x.Type == stampType);
    }

    // The audience a sign-in may name is one the registered JWT scheme accepts: the token helper writes whatever
    // audience it is given, and applications that share a signing secret are told apart by nothing else.
    [Fact]
    public void IsAllowedAudience_FollowsTheConfiguredAudiences()
    {
        var none = WithJwtSchemes(_ => { });
        var single = WithJwtSchemes(o => o.Audience = "spa");
        var list = WithJwtSchemes(o =>
        {
            o.Audience = "ignored";
            o.Audiences = ["spa", "mobile"];
        });
        var twoSchemes = WithJwtSchemes(o => o.Audience = "spa", o =>
        {
            o.AuthenticationScheme = "Admin";
            o.Audience = "admin";
        });

        none.IsAllowedAudience("anything").ShouldBeTrue();
        single.IsAllowedAudience("spa").ShouldBeTrue();
        single.IsAllowedAudience("SPA").ShouldBeFalse();
        single.IsAllowedAudience("admin").ShouldBeFalse();
        single.IsAllowedAudience("").ShouldBeFalse();
        single.IsAllowedAudience(null).ShouldBeTrue("the token then carries the configured audience");
        list.IsAllowedAudience("mobile").ShouldBeTrue();
        list.IsAllowedAudience("ignored").ShouldBeFalse("an Audiences list replaces the single Audience");
        twoSchemes.IsAllowedAudience("spa").ShouldBeTrue();
        twoSchemes.IsAllowedAudience("admin").ShouldBeTrue();
        twoSchemes.IsAllowedAudience("mobile").ShouldBeFalse();
    }

    // The service over a host that registered these JWT schemes; the audience check reads nothing else.
    private static AccountSignInService<TestUser> WithJwtSchemes(params Action<JwtTokenOptions>[] schemes)
    {
        var services = new ServiceCollection();
        foreach (var configure in schemes)
        {
            services.AddJwtAuthentication(o =>
            {
                o.Secret = "sign-in-service-tests-secret-long-enough-for-the-hs512-default-0123";
                configure(o);
            });
        }
        return new AccountSignInService<TestUser>(null!, null!, services.BuildServiceProvider());
    }

    [Fact]
    public void CreateFailedResponse_CarriesTheLockout()
    {
        var response = AccountSignInService<TestUser>.CreateFailedResponse(true, new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero));

        response.IsAuthenticated.ShouldBeFalse();
        response.IsLockedOut.ShouldBe(true);
        response.LockedOutEnd.ShouldBe(new DateTime(2026, 1, 2, 3, 4, 5));
    }

    private static (AccountSignInService<TestUser> Service, UserManager<TestUser> UserManager) Resolve(IServiceScope scope)
        => (ActivatorUtilities.CreateInstance<AccountSignInService<TestUser>>(scope.ServiceProvider),
            scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>());

    private static async Task<TestUser> Seed(UserManager<TestUser> userManager, string username, bool emailConfirmed)
    {
        var user = await userManager.FindByNameAsync(username);
        if (user != null)
        {
            return user;
        }
        user = new TestUser { UserName = username, Email = username, EmailConfirmed = emailConfirmed };
        var result = await userManager.CreateAsync(user, "pass1");
        result.Succeeded.ShouldBeTrue(result.Errors.FirstOrDefault()?.Description);
        return user;
    }
}
