using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record ChangePasswordInput
{
    [Required, MaxLength(AccountInputLimits.Password)]
    public string NewPassword { get; set; } = null!;
    [Required, MaxLength(AccountInputLimits.Password)]
    public string CurrentPassword { get; set; } = null!;
}