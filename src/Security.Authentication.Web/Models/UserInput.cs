using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record UserInput
{
    [Required, MaxLength(AccountInputLimits.UserName)]
    public string Username { get; set; } = null!;
    [Required, MaxLength(AccountInputLimits.Password)]
    public string Password { get; set; } = null!;
    /// <summary>
    /// The page a confirmation link opens, e.g. <c>https://app.example.com/confirm-email</c>. With it, a new user is
    /// mailed that page with its query string set to <c>?token=…</c>; it must be on one of
    /// <see cref="AccountEmailOptions.AllowedOrigins"/>, or the request answers <c>400</c>.
    /// </summary>
    [MaxLength(AccountInputLimits.Url)]
    public string? ConfirmEmailUrl { get; set; }
}