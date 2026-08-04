using Duende.IdentityModel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Web.Constants;
using Regira.Security.Authentication.Web.Models;
using Regira.Web.Utilities;
using System.Security.Claims;

namespace Regira.Security.Authentication.Web.Controllers;

[ApiController]
[Route("auth")]
public abstract class AccountControllerBase<TUser>(ITokenHelper tokenHelper, UserManager<TUser> userManager, IUserClaimsPrincipalFactory<TUser> claimsFactory, ILogger? logger = null) : ControllerBase
    where TUser : IdentityUser<string>
{
    /// <summary>
    /// Resolved per request rather than injected, so the constructor signature every consumer already calls stays
    /// unchanged — and so a host that never registered refresh tokens is indistinguishable from before.
    /// </summary>
    private IRefreshTokenService? RefreshTokenService => HttpContext.RequestServices.GetService<IRefreshTokenService>();

    [AllowAnonymous]
    [HttpPost]
    [Route("", Name = RouteNames.Authenticate)]
    public virtual async Task<IActionResult> Authenticate([FromBody] AuthenticateInput model, [FromQuery] string clientApp)
    {
        bool? isLockedOut = null;
        DateTimeOffset? lockedOutEnd = null;

        var user = await userManager.FindByNameAsync(model.Username);
        if (user != null)
        {
            isLockedOut = await userManager.IsLockedOutAsync(user);
            if (isLockedOut == false)
            {
                bool isAuthenticated = await userManager.CheckPasswordAsync(user, model.Password);
                if (isAuthenticated)
                {
                    // clear any previously accumulated failed-attempt count (no-op when already 0)
                    await userManager.ResetAccessFailedCountAsync(user);
                    var principal = await claimsFactory.CreateAsync(user);
                    return Ok(await CreateSuccessResponse(principal.Claims, clientApp, user.Id));
                }
                // authentication failed
                await userManager.AccessFailedAsync(user);
            }
            else
            {
                lockedOutEnd = await userManager.GetLockoutEndDateAsync(user);
                logger?.LogWarning("User {UserId} {IpAddress} locked out until {LockoutEnd:HH:mm:ss}", user.Id, Request.GetIPAddress(), lockedOutEnd);
            }
        }

        return StatusCode(StatusCodes.Status401Unauthorized, CreateFailedResponse(isLockedOut, lockedOutEnd));
    }

    [HttpPost("validate")]
    public virtual async Task<IActionResult> Validate()
    {
        if (User.Identity?.IsAuthenticated ?? false)
        {
            // check if user is valid
            var exists = await userManager.FindByIdAsync(User.FindUserId()!) != null;
            return exists ? NoContent() : Forbid();
        }

        return Unauthorized();
    }
    [HttpPost("refresh")]
    public virtual async Task<IActionResult> Refresh()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(new
            {
                isAuthenticated = false
            });
        }
        var user = await userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized(new
            {
                isAuthenticated = false
            });
        }
        var principal = await claimsFactory.CreateAsync(user);
        return Ok(await CreateSuccessResponse(principal.Claims, User.FindFirstValue("aud"), user.Id));
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token. Distinct from <see cref="Refresh"/>, which renews a token that
    /// is <em>still valid</em> and so cannot help once the access token has expired — the one moment a refresh is
    /// actually needed.
    /// <para>
    /// Anonymous by necessity: the refresh token <b>is</b> the credential, and the expired access token that came with
    /// it would fail authentication. Rate-limit this endpoint.
    /// </para>
    /// <para>
    /// Answers <c>404</c> when no <c>IRefreshTokenService</c> is registered, so a host that has not opted in exposes
    /// nothing new rather than a route that 500s.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public virtual async Task<IActionResult> RefreshToken([FromBody] RefreshTokenInput model)
    {
        var refreshTokenService = RefreshTokenService;
        if (refreshTokenService == null)
        {
            return NotFound();
        }

        // Re-read the user on every refresh: a role removed an hour ago must not still be in force, and a disabled
        // account must not keep working until its refresh token happens to expire.
        var pair = await refreshTokenService.Refresh(model.RefreshToken, async userId =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null || await userManager.IsLockedOutAsync(user))
            {
                return null;
            }

            return (await claimsFactory.CreateAsync(user)).Claims;
        });

        if (pair == null)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, CreateFailedResponse());
        }

        return Ok(new AuthenticateResponseDto
        {
            IsAuthenticated = true,
            Token = pair.AccessToken,
            ExpiresAt = pair.AccessTokenExpiresAt,
            RefreshToken = pair.RefreshToken
        });
    }


    [HttpGet("personal-data")]
    public virtual async Task<IActionResult> GetPersonalData()
    {
        var user = await userManager.FindByIdAsync(User.FindUserId()!);
        if (user == null)
        {
            return Unauthorized();
        }
        var principal = await claimsFactory.CreateAsync(user);
        var personalDataClaimTypes = new[]
        {
            JwtClaimTypes.GivenName, JwtClaimTypes.FamilyName
        };
        var personalData = principal.Claims.Where(c => personalDataClaimTypes.Contains(c.Type))
            .ToDictionary(x => x.Type, x => x.Value);
        return Ok(personalData);
    }


    protected AuthenticateResponseDto CreateFailedResponse(bool? isLockedOut = null, DateTimeOffset? lockedOutEnd = null)
    {
        return new AuthenticateResponseDto
        {
            IsLockedOut = isLockedOut,
            // datetime without timezone
            LockedOutEnd = lockedOutEnd.HasValue ? new DateTime(lockedOutEnd.Value.Ticks) : null
        };
    }
    /// <summary>
    /// Mints the sign-in response, including a refresh token when an <c>IRefreshTokenService</c> is registered.
    /// Without one, the body is byte-identical to what it was before refresh tokens existed.
    /// <para>
    /// <paramref name="userId"/> is deliberately required — a refresh token has to be tied to a user, and an optional
    /// parameter here would make the two overloads ambiguous at two arguments and let a caller silently take the path
    /// that issues no refresh token.
    /// </para>
    /// </summary>
    protected async Task<AuthenticateResponseDto> CreateSuccessResponse(IEnumerable<Claim> claims, string? audience, string userId)
    {
        var claimList = claims as IReadOnlyCollection<Claim> ?? claims.ToArray();

        var refreshTokenService = RefreshTokenService;
        if (refreshTokenService != null)
        {
            var pair = await refreshTokenService.Issue(userId, claimList, audience);
            return new AuthenticateResponseDto
            {
                IsAuthenticated = true,
                Token = pair.AccessToken,
                ExpiresAt = pair.AccessTokenExpiresAt,
                RefreshToken = pair.RefreshToken
            };
        }

        return new AuthenticateResponseDto
        {
            IsAuthenticated = true,
            Token = tokenHelper.Create(claimList, audience)
        };
    }

    /// <summary>
    /// The original signature, kept source-compatible for a subclass that calls it. Issues no refresh token — there is
    /// no user id here to tie one to.
    /// </summary>
    protected AuthenticateResponseDto CreateSuccessResponse(IEnumerable<Claim> claims, string? audience = null)
        => new()
        {
            IsAuthenticated = true,
            Token = tokenHelper.Create(claims, audience)
        };
}
