using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Models;
using System.Security.Claims;

namespace Regira.Security.Authentication.Web.Services;

/// <summary>
/// The sign-in checks and the token response of <c>AccountControllerBase</c>, for a host that maps its own account
/// endpoints (minimal APIs) or a second credential of its own. The controller delegates to it, so both answer alike.
/// <para>
/// Constructible by DI as it is (<c>services.AddScoped&lt;AccountSignInService&lt;AppUser&gt;&gt;()</c>): the optional
/// services — refresh tokens, a custom <see cref="IUserConfirmation{TUser}"/>, the Identity options — are read from
/// <paramref name="services"/>, the request's scope.
/// </para>
/// </summary>
public class AccountSignInService<TUser>(ITokenHelper tokenHelper, UserManager<TUser> userManager, IServiceProvider services)
    where TUser : IdentityUser<string>
{
    /// <summary>
    /// Whether <see cref="SignInOptions"/> let the user sign in, as <c>SignInManager.CanSignInAsync</c> decides: a
    /// confirmed email, phone number or account, where the options require one. The account is confirmed when the
    /// registered <see cref="IUserConfirmation{TUser}"/> says so, and by its address otherwise. Check it after the
    /// credential, before a token is issued.
    /// </summary>
    public async Task<bool> CanSignIn(TUser user)
    {
        var options = userManager.Options.SignIn;
        if (options.RequireConfirmedEmail && !await userManager.IsEmailConfirmedAsync(user))
        {
            return false;
        }
        if (options.RequireConfirmedPhoneNumber && !await userManager.IsPhoneNumberConfirmedAsync(user))
        {
            return false;
        }
        if (options.RequireConfirmedAccount)
        {
            var confirmation = services.GetService<IUserConfirmation<TUser>>() ?? new DefaultUserConfirmation<TUser>();
            return await confirmation.IsConfirmedAsync(userManager, user);
        }
        return true;
    }

    /// <summary>
    /// Mints the sign-in response, including a refresh token when an <c>IRefreshTokenService</c> is registered.
    /// Without one, the body is byte-identical to what it was before refresh tokens existed.
    /// </summary>
    public async Task<AuthenticateResponseDto> CreateSuccessResponse(IEnumerable<Claim> claims, string? audience, string userId)
    {
        var claimList = TokenClaims(claims).ToArray();

        var refreshTokenService = services.GetService<IRefreshTokenService>();
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

    /// <summary>The refused sign-in's response; <c>with { IsNotAllowed = true }</c> when <see cref="CanSignIn"/> refused it.</summary>
    public static AuthenticateResponseDto CreateFailedResponse(bool? isLockedOut = null, DateTimeOffset? lockedOutEnd = null)
        => new()
        {
            IsLockedOut = isLockedOut,
            // datetime without timezone
            LockedOutEnd = lockedOutEnd.HasValue ? new DateTime(lockedOutEnd.Value.Ticks) : null
        };

    /// <summary>
    /// The claims a token may carry: the principal's, less Identity's security stamp. A JWT is signed, not encrypted, so
    /// whoever holds one can read it — and the stamp is the secret Identity's email and phone token providers derive
    /// their one-time codes from. Nothing validates it on a bearer request either, so leaving it out costs nothing.
    /// </summary>
    public IEnumerable<Claim> TokenClaims(IEnumerable<Claim> claims)
    {
        var stampClaimType = services.GetService<IOptions<IdentityOptions>>()?.Value.ClaimsIdentity.SecurityStampClaimType
            ?? new ClaimsIdentityOptions().SecurityStampClaimType;
        return claims.Where(claim => claim.Type != stampClaimType);
    }
}
