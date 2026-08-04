using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record AuthenticateInput
{
    [Required]
    public string Username { get; set; } = null!;
    [Required]
    public string Password { get; set; } = null!;
}