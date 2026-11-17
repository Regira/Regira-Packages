using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record ResetPasswordInput
{
    [Required, MaxLength(AccountInputLimits.Token)]
    public string Token { get; set; } = null!;
    [Required, MaxLength(AccountInputLimits.Password)]
    public string Password { get; set; } = null!;
}
