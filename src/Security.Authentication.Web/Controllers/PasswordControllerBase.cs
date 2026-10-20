using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Web.Extensions;
using Regira.Security.Authentication.Web.Models;
using Regira.Utilities;

namespace Regira.Security.Authentication.Web.Controllers;

[ApiController]
[Route("auth/password")]
public abstract class PasswordControllerBase<TUser>(UserManager<TUser> userManager) : ControllerBase
    where TUser : IdentityUser<string>
{
    [HttpPost]
    public virtual async Task<IActionResult> UpdatePassword([FromBody] ChangePasswordInput model)
    {
        var username = User.Identity!.Name;
        var user = await userManager.FindByNameAsync(username!);
        if (user == null)
        {
            return NotFound();
        }

        var result = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (result.Succeeded)
        {
            await RevokeRefreshTokens(user);
            return Ok();
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