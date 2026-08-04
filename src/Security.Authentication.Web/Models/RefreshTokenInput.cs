using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public class RefreshTokenInput
{
    [Required]
    public string RefreshToken { get; set; } = null!;
}
