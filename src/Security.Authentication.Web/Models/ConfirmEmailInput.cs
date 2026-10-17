using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record ConfirmEmailInput
{
    [Required]
    public string Token { get; set; } = null!;
    /// <summary>Not read: the user comes from the token.</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }
}