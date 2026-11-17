using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public class RefreshTokenInput
{
    [Required, MaxLength(AccountInputLimits.Token)]
    public string RefreshToken { get; set; } = null!;
}
