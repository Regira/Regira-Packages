using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Cookie.Extensions;
using System.Security.Claims;

namespace Web.Security.Testing.Infrastructure.Cookie;

[ApiController]
[Route("cookie-auth")]
public class CookieAuthController : ControllerBase
{
    public const string UserId = "COOKIE_USER_ID";
    public const string UserName = "CookieUser";
    public const string AdminRole = "admin";

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromQuery] bool withRole = true, [FromQuery] bool isPersistent = false)
    {
        // The .NET claim URIs on purpose: it is what IUserClaimsPrincipalFactory produces, so it exercises the
        // normalization rather than handing the canonical spellings straight in.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, UserId),
            new(ClaimTypes.Name, UserName),
            new(ClaimTypes.Email, "cookie-user@example.com")
        };
        if (withRole)
        {
            claims.Add(new Claim(ClaimTypes.Role, AdminRole));
        }

        await HttpContext.SignInWithClaimsAsync(claims, isPersistent: isPersistent);
        return NoContent();
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutCookieAsync();
        return NoContent();
    }
}
