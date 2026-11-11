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
using Regira.Security.Authentication.Web.Services;
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

    private static readonly IServiceProvider NoServices = new ServiceCollection().BuildServiceProvider();
    /// <summary>
    /// The sign-in checks and token response, shared with a minimal-API host; built per request from its services, so
    /// the constructor stays unchanged.
    /// </summary>
    private AccountSignInService<TUser> SignInService => new(tokenHelper, userManager, HttpContext?.RequestServices ?? NoServices);

    /// <inheritdoc cref="AccountSignInService{TUser}.CanSignIn"/>
    /// <remarks>A sign-in action of a subclass (a second credential) checks it before it issues a token, as the actions here do.</remarks>
    protected Task<bool> CanSignIn(TUser user) => SignInService.CanSignIn(user);

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
                // checked after the password: only a caller who knows it learns why the account is refused
                if (isAuthenticated && !await CanSignIn(user))
                {
                    return StatusCode(StatusCodes.Status401Unauthorized, CreateFailedResponse(false) with { IsNotAllowed = true });
                }
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

        // false for an unknown name too, so the body does not tell it apart from a wrong password
        return StatusCode(StatusCodes.Status401Unauthorized, CreateFailedResponse(isLockedOut ?? false, lockedOutEnd));
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
        // a locked-out account, or one the sign-in options refuse, renews nothing: Authenticate would refuse it a token
        if (user == null || await userManager.IsLockedOutAsync(user) || !await CanSignIn(user))
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
        // account, or one the sign-in options now refuse, must not keep working until its refresh token happens to expire.
        var pair = await refreshTokenService.Refresh(model.RefreshToken, async userId =>
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null || await userManager.IsLockedOutAsync(user) || !await CanSignIn(user))
            {
                return null;
            }

            return TokenClaims((await claimsFactory.CreateAsync(user)).Claims);
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
        => AccountSignInService<TUser>.CreateFailedResponse(isLockedOut, lockedOutEnd);
    /// <inheritdoc cref="AccountSignInService{TUser}.CreateSuccessResponse"/>
    /// <remarks>
    /// <paramref name="userId"/> is deliberately required — a refresh token has to be tied to a user, and an optional
    /// parameter here would make the two overloads ambiguous at two arguments and let a caller silently take the path
    /// that issues no refresh token.
    /// </remarks>
    protected Task<AuthenticateResponseDto> CreateSuccessResponse(IEnumerable<Claim> claims, string? audience, string userId)
        => SignInService.CreateSuccessResponse(claims, audience, userId);

    /// <summary>
    /// The original signature, kept source-compatible for a subclass that calls it. Issues no refresh token — there is
    /// no user id here to tie one to.
    /// </summary>
    protected AuthenticateResponseDto CreateSuccessResponse(IEnumerable<Claim> claims, string? audience = null)
        => new()
        {
            IsAuthenticated = true,
            Token = tokenHelper.Create(TokenClaims(claims), audience)
        };

    /// <inheritdoc cref="AccountSignInService{TUser}.TokenClaims"/>
    protected IEnumerable<Claim> TokenClaims(IEnumerable<Claim> claims) => SignInService.TokenClaims(claims);
}
