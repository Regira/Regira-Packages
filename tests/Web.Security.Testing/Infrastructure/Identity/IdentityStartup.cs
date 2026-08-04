using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Jwt.Extensions;

namespace Web.Security.Testing.Infrastructure.Identity;

// Hosts the three Security.Authentication.Web base controllers over a real ASP.NET Core Identity
// stack (EF Core in-memory store) so the endpoints are exercised end-to-end.
public class IdentityStartup
{
    // Fixed secret so tokens issued by /auth validate on subsequent requests within the same app.
    // Must be >= 64 bytes: HMAC-SHA512 (the JwtTokenHelper default) requires a >= 512-bit key.
    private const string JwtSecret = "regira-web-security-testing-identity-signing-secret-key-0123456789-abcdefghijklmnop";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("Web.Security.Testing.Identity"));

        services
            .AddIdentityCore<TestUser>(o =>
            {
                // relax password rules so tests can use simple passwords
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 4;
                // enable lockout tracking so AccessFailedCount is persisted
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.User.RequireUniqueEmail = false;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        // Password reset / email confirmation tokens are protected with data protection.
        services.AddDataProtection();

        // JWT bearer is the only authentication scheme; /auth issues the tokens.
        services.AddJwtAuthentication(o =>
        {
            o.Secret = JwtSecret;
            o.LifeSpan = 3600;
        });

        services.AddSingleton<SentEmailStore>();
        services.AddTransient<IEmailSender, FakeEmailSender>();

        services.AddControllersFor(
            typeof(TestAccountController),
            typeof(TestPasswordController),
            typeof(TestUsersController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapControllers()
                // Guarded-by-default: every endpoint requires an authenticated user unless the action
                // opts out with [AllowAnonymous] (recover, reset, confirm-email, and authenticate itself).
                .RequireAuthorization();
        });
    }
}
