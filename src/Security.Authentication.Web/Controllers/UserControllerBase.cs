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
    /// Creates the user, and with <see cref="UserInput.ConfirmEmailUrl"/> mails it a confirmation link: that page with
    /// its query string set to <c>?token=…</c>. A name that is taken, like any refusal of Identity's, answers <c>400</c> with
    /// Identity's error code (<c>DuplicateUserName</c>) as the key, and the existing user is left alone.
    /// <para>
    /// The page must be on one of <see cref="AccountEmailOptions.AllowedOrigins"/>, or the answer is <c>400</c> and
    /// nothing is created or sent. Throws <see cref="InvalidOperationException"/> when a page is given and no origin is
    /// configured.
    /// </para>
    /// <para>
    /// Saves <c>new TUser { UserName, Email }</c>, both the input's <see cref="UserInput.Username"/>. A user type with a
    /// required column that has no default calls the protected
    /// <see cref="Create(TUser, string, string?, IEmailSender)"/> overload with a user it filled itself.
    /// </para>
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(AccountInputLimits.RequestBody)]
    public virtual Task<IActionResult> Create(UserInput model, [FromServices] IEmailSender mailer)
        => Create(new TUser { UserName = model.Username, Email = model.Username }, model.Password, model.ConfirmEmailUrl, mailer);

    /// <summary>
    /// <see cref="Create(UserInput, IEmailSender)"/> for a user the caller built: a sign-up action that fills the user
    /// type's own columns before it is saved. <paramref name="user"/> carries at least its <c>UserName</c> and
    /// <c>Email</c>; the confirmation mail goes to that address, and the answers are those of the public action.
    /// </summary>
    protected async Task<IActionResult> Create(TUser user, string password, string? confirmEmailUrl, IEmailSender mailer)
    {
        Uri? confirmPage = null;
        if (!string.IsNullOrWhiteSpace(confirmEmailUrl))
        {
            confirmPage = HttpContext.GetAccountEmailOptions().FindAllowedPage(confirmEmailUrl);
            if (confirmPage == null)
            {
                ModelState.AddModelError(nameof(UserInput.ConfirmEmailUrl), AccountLinks.NotAllowed);
                return BadRequest(ModelState);
            }
        }

        var response = await userManager.CreateAsync(user, password);
        if (!response.Succeeded)
        {
            ModelState.AddIdentityErrors(response.Errors);
            return BadRequest(ModelState);
        }
        if (confirmPage != null)
        {
            var confirmToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var token = System.Text.Json.JsonSerializer.Serialize(new UserTokenModel { Token = confirmToken, Username = user.UserName! }).Base64Encode();
            var body = $@"Welcome {user.UserName},
Please follow link below to confirm email
{confirmPage.WithToken(token)}

Token: {token}
";
            await mailer.SendEmailAsync(user.Email!, "Welcome", body);
        }

        return Ok();
    }

    [AllowAnonymous]
    [HttpPost("confirm-email", Name = RouteNames.ConfirmEmail)]
    [RequestSizeLimit(AccountInputLimits.RequestBody)]
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
        // a payload without a user name or a token is malformed too
        if (tokenModel?.Username == null || tokenModel.Token == null)
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
