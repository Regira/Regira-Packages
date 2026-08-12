using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Jwt.Extensions;

namespace Web.Security.Testing.Infrastructure.Identity;

// The roles-end-to-end recipe as a running host: Identity with .AddRoles<TRole>() and
// ClaimsIdentity.RoleClaimType aligned to the JWT scheme's "role", AccountControllerBase minting the
// tokens, and a role-gated controller proving [Authorize(Roles=…)] against them.
public class RolesStartup
{
    // Must be >= 64 bytes: HMAC-SHA512 (the JwtTokenHelper default) requires a >= 512-bit key.
    private const string JwtSecret = "regira-web-security-testing-roles-signing-secret-key-0123456789-abcdefghijklmnop";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase("Web.Security.Testing.Roles"));

        services
            .AddIdentityCore<TestUser>(o =>
            {
                // relax password rules so tests can use simple passwords
                o.Password.RequireDigit = false;
                o.Password.RequireLowercase = false;
                o.Password.RequireUppercase = false;
                o.Password.RequireNonAlphanumeric = false;
                o.Password.RequiredLength = 4;
                // the recipe's key line: emit role claims under the spelling the JWT scheme validates
                // against ("role") instead of the default ClaimTypes.Role URI
                o.ClaimsIdentity.RoleClaimType = RegiraClaimTypes.Role;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TestDbContext>()
            .AddDefaultTokenProviders();

        services.AddDataProtection();

        services.AddJwtAuthentication(o =>
        {
            o.Secret = JwtSecret;
            o.LifeSpan = 3600;
        });

        services.AddControllersFor(
            typeof(TestAccountController),
            typeof(RoleGatedController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints =>
        {
            endpoints
                .MapControllers()
                .RequireAuthorization();
        });
    }
}

[ApiController]
[Route("reports")]
[Authorize(Roles = "Manager")]
public class RoleGatedController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { ok = true });
}
