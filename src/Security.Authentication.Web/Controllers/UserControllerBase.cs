using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Regira.Security.Authentication.Web.Constants;
using Regira.Security.Authentication.Web.Extensions;
using Regira.Security.Authentication.Web.Models;
using Regira.Utilities;

namespace Regira.Security.Authentication.Web.Controllers;

[ApiController]
[Route("users")]
public abstract class UserControllerBase<TUser>(UserManager<TUser> userManager) : ControllerBase
  where TUser : IdentityUser<string>, new()
{
    /// <summary>
    /// Creates the user, unless one by that name exists, and with <see cref="UserInput.ConfirmEmailUrl"/> mails it a
    /// confirmation link: that page with the token as its query string.
    /// <para>
    /// The page must be on one of <see cref="AccountEmailOptions.AllowedOrigins"/>, or the answer is <c>400</c> and
    /// nothing is created or sent. Throws <see cref="InvalidOperationException"/> when a page is given and no origin is
    /// configured.
    /// </para>
    /// </summary>
    [HttpPost]
    public virtual async Task<IActionResult> Create(UserInput model, [FromServices] IEmailSender mailer)
    {
        Uri? confirmPage = null;
        if (!string.IsNullOrWhiteSpace(model.ConfirmEmailUrl))
        {
            confirmPage = HttpContext.GetAccountEmailOptions().FindAllowedPage(model.ConfirmEmailUrl);
            if (confirmPage == null)
            {
                ModelState.AddModelError(nameof(model.ConfirmEmailUrl), AccountLinks.NotAllowed);
                return BadRequest(ModelState);
            }
        }

        var user = await userManager.FindByNameAsync(model.Username);
        if (user == null)
        {
            user = new TUser { UserName = model.Username, Email = model.Username };
            var response = await userManager.CreateAsync(user, model.Password);
            if (!response.Succeeded)
            {
                // error
                ModelState.AddIdentityErrors(response.Errors);
                return BadRequest(ModelState);
            }
            if (confirmPage != null)
            {
                var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
                var token = System.Text.Json.JsonSerializer.Serialize(new UserTokenModel { Token = confirmToken, Username = user.UserName! }).Base64Encode();
                var body = $@"Welcome {model.Username},
Please follow link below to confirm email
{confirmPage.WithToken(token)}

Token: {token}
";
                await mailer.SendEmailAsync(user.Email, "Welcome", body);
            }
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("confirm-email", Name = RouteNames.ConfirmEmail)]
    public virtual async Task<IActionResult> ConfirmEmail([FromBody] ConfirmEmailInput model)
    {
        UserTokenModel? tokenModel;
        try
        {
            tokenModel = System.Text.Json.JsonSerializer.Deserialize<UserTokenModel>(model.Token.Base64Decode());
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
            var emailResponse = await userManager.ConfirmEmailAsync(user, tokenModel.Token);
            if (!emailResponse.Succeeded)
            {
                ModelState.AddIdentityErrors(emailResponse.Errors);
                return BadRequest(ModelState);
            }
        }

        return Ok();
    }
}
