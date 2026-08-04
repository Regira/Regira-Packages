using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Jwt.Extensions;

namespace Web.Security.Testing.Infrastructure.Identity;

/// <summary>
/// The same Identity stack as <see cref="IdentityStartup"/> but with refresh tokens registered, so the pair can be
/// compared: <see cref="IdentityStartup"/> covers the opted-out shape and this one the opted-in shape.
/// </summary>
public class RefreshTokenStartup
{
    private const string JwtSecret = "regira-web-security-testing-refresh-signing-secret-key-0123456789-abcdefghijklmnop";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("Web.Security.Testing.RefreshTokens"));

        services
            .AddIdentityCore<TestUser>(o =>
            {
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 4;
                o.Lockout.AllowedForNewUsers = true;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.User.RequireUniqueEmail = false;
                o.SignIn.RequireConfirmedAccount = false;
            })
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection();

        services
            .AddJwtAuthentication(o =>
            {
                o.Secret = JwtSecret;
                // Short, so a test can prove a refresh works after the access token has expired — the one moment
                // the pre-existing authenticated `auth/refresh` cannot help.
                o.LifeSpan = 2;
            })
            .AddRefreshTokens();

        services.AddSingleton<SentEmailStore>();
        services.AddTransient<IEmailSender, FakeEmailSender>();

        services.AddControllersFor(typeof(TestAccountController), typeof(TestUsersController));
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
        app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
    }
}
