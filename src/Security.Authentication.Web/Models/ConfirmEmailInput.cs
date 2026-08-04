using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record ConfirmEmailInput
{
    [Required]
    public string Token { get; set; } = null!;
    [Required]
    public string UserName { get; set; } = null!;
    public string? Password { get; set; }
}