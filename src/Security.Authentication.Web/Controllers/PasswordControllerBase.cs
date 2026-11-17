using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Constants;
using Regira.Security.Authentication.Web.Extensions;
using Regira.Security.Authentication.Web.Models;
using Regira.Utilities;

namespace Regira.Security.Authentication.Web.Controllers;

[ApiController]
[Route("auth/password")]
public abstract class PasswordControllerBase<TUser>(UserManager<TUser> userManager) : ControllerBase
    where TUser : IdentityUser<string>
{
    /// <summary>The key of the model error <see cref="UpdatePassword"/> answers for an account that <see cref="IsBlocked"/> refuses.</summary>
    public const string UserLockedOut = nameof(UserLockedOut);

    /// <summary>
    /// Whether the account is refused before its current password is checked, as <c>AccountControllerBase.IsBlocked</c>
    /// refuses a sign-in: <see cref="UpdatePassword"/> answers it <c>400</c> with the error <see cref="UserLockedOut"/>.
    /// Identity's lockout by default (<c>UserManager.IsLockedOutAsync</c>, where the user store supports lockout).
    /// Override it, with <see cref="OnPasswordRejected"/>, for the policy the account controller overrides.
    /// </summary>
    protected virtual Task<bool> IsBlocked(TUser user)
        => userManager.SupportsUserLockout ? userManager.IsLockedOutAsync(user) : Task.FromResult(false);

    /// <summary>
    /// Runs when <see cref="UpdatePassword"/> receives a wrong current password for an account that is not blocked.
    /// Counts a failed attempt toward Identity's lockout by default (<c>UserManager.AccessFailedAsync</c>, where the
    /// user store supports lockout), as a wrong password at sign-in does.
    /// </summary>
    protected virtual Task OnPasswordRejected(TUser user)
        => userManager.SupportsUserLockout ? userManager.AccessFailedAsync(user) : Task.CompletedTask;

    /// <summary>
    /// Changes the signed-in user's password and revokes the user's refresh tokens. The current password is a
    /// credential, so it is guarded as a sign-in is: a blocked account is refused before the password is checked
    /// (<c>400</c>, error <see cref="UserLockedOut"/>), a wrong current password counts as a failed attempt, and a
    /// change that succeeds resets the count. A new password Identity refuses is no guess and counts for nothing.
    /// Answers <c>401</c> for a request without a signed-in user.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(AccountInputLimits.RequestBody)]
    public virtual async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordInput model)
    {
        // a host without a global authorization requirement lets a request in that carries no user
        var username = User.Identity?.IsAuthenticated == true ? User.Identity.Name : null;
        if (string.IsNullOrEmpty(username))
        {
            return Unauthorized();
        }
        var user = await userManager.FindByNameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        // 400, not 401: a client reads a 401 on a signed-in call as the end of its session
        if (await IsBlocked(user))
        {
            ModelState.AddModelError(UserLockedOut, "The account is locked out.");
            return BadRequest(ModelState);
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            if (userManager.SupportsUserLockout)
            {
                // clear any previously accumulated failed-attempt count (no-op when already 0)
                await userManager.ResetAccessFailedCountAsync(user);
            }
            await RevokeRefreshTokens(user);
            return Ok();
        }

        // only a wrong current password is a guess: Identity checks it first, and refuses the new one after
        if (result.Errors.Any(error => error.Code == nameof(IdentityErrorDescriber.PasswordMismatch)))
        {
            await OnPasswordRejected(user);
        }

        ModelState.AddIdentityErrors(result.Errors);
        return BadRequest(ModelState);
    }

    /// <summary>
    /// Mails a reset link to the user: <see cref="RecoverPasswordInput.SiteUrl"/> with its query string set to
    /// <c>?token=…</c>, the percent-encoded Base64 of the reset token and user name, under a subject named after
    /// <see cref="AccountEmailOptions.SiteName"/>.
    /// <para>
    /// The page must be on one of <see cref="AccountEmailOptions.AllowedOrigins"/>, or the answer is <c>400</c> and no
    /// mail is sent — whoever controls the page receives the token. Otherwise the answer is <c>200</c> whether or not the
    /// user exists. Throws <see cref="InvalidOperationException"/> when no origin is configured.
    /// </para>
    /// </summary>
    [AllowAnonymous]
    [HttpPost("recover")]
    [RequestSizeLimit(AccountInputLimits.RequestBody)]
    public virtual async Task<IActionResult> RecoverPassword([FromBody] RecoverPasswordInput model, [FromServices] IEmailSender mailer)
    {
        // checked before the user is looked up, so the answer does not tell whether the account exists
        var options = HttpContext.GetAccountEmailOptions();
        var resetPage = options.FindAllowedPage(model.SiteUrl);
        if (resetPage == null)
        {
            ModelState.AddModelError(nameof(model.SiteUrl), AccountLinks.NotAllowed);
            return BadRequest(ModelState);
        }

        var user = await userManager.FindByNameAsync(model.Username);

        if (user != null)
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var token = System.Text.Json.JsonSerializer.Serialize(new UserTokenModel { Token = resetToken, Username = user.UserName! }).Base64Encode();
            var subject = $"{options.SiteName} Password Reset".Trim();
            var body = $@"Follow link below to reset password
{resetPage.WithToken(token)}

Token: {token}
";
            await mailer.SendEmailAsync(user.Email!, subject, body);
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("reset")]
    [RequestSizeLimit(AccountInputLimits.RequestBody)]
    public virtual async Task<IActionResult> ResetPassword([FromBody] ResetPasswordInput input)
    {
        UserTokenModel? tokenModel;
        try
        {
            tokenModel = System.Text.Json.JsonSerializer.Deserialize<UserTokenModel>(input.Token.Base64Decode());
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            // malformed token (invalid Base64 or unparseable payload)
            tokenModel = null;
        }
        // a payload without a user name or a token is malformed too
        if (tokenModel?.Username == null || tokenModel.Token == null)
        {
            return BadRequest();
        }

        var user = await userManager.FindByNameAsync(tokenModel.Username);
        if (user != null)
        {
            var response = await userManager.ResetPasswordAsync(user, tokenModel.Token, input.Password);
            if (!response.Succeeded)
            {
                ModelState.AddIdentityErrors(response.Errors);
                return BadRequest(ModelState);
            }
            await RevokeRefreshTokens(user);
        }

        return Ok();
    }

    /// <summary>
    /// Ends the sessions the old password opened, when refresh tokens are registered: a refresh token stolen before the
    /// change or the reset renews nothing after it. Resolved per request, as <c>AccountControllerBase</c> resolves it.
    /// </summary>
    private Task RevokeRefreshTokens(TUser user)
        => HttpContext.RequestServices.GetService<IRefreshTokenService>()?.RevokeAllForUser(user.Id) ?? Task.CompletedTask;
}