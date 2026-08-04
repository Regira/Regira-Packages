using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Web.Extensions;
using Regira.Security.Authentication.Web.Models;
using Regira.Utilities;

namespace Regira.Security.Authentication.Web.Controllers;

[ApiController]
[Route("auth/password")]
public abstract class PasswordControllerBase<TUser>(UserManager<TUser> userManager) : ControllerBase
    where TUser : IdentityUser
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
            return Ok();
        }

        ModelState.AddIdentityErrors(result.Errors);
        return BadRequest(ModelState);
    }

    [AllowAnonymous]
    [HttpPost("recover")]
    public virtual async Task<IActionResult> RecoverPassword([FromBody] RecoverPasswordInput model, [FromServices] IEmailSender mailer)
    {
        var user = await userManager.FindByNameAsync(model.Username);

        if (user != null)
        {
            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            var token = System.Text.Json.JsonSerializer.Serialize(new UserTokenModel { Token = resetToken, Username = user.UserName! }).Base64Encode();
            var resetUri = new UriBuilder(model.SiteUrl)
            {
                Query = $"?token={token}"
            };
            var subject = $"{model.SiteName} password reset".Trim().Capitalize()!;
            var body = $@"Follow link below to reset password
{resetUri.Uri}

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
        if (tokenModel == null)
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
        }

        return Ok();
    }
}