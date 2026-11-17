using Regira.Security.Authentication.Web.Constants;
using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record RecoverPasswordInput
{
    [Required, MaxLength(AccountInputLimits.UserName)]
    public string Username { get; set; } = null!;
    /// <summary>
    /// The page the reset link opens, e.g. <c>https://app.example.com/reset-password</c>; the link replaces its query
    /// string with <c>?token=…</c>. It must be on one of <see cref="AccountEmailOptions.AllowedOrigins"/>, or the request
    /// answers <c>400</c>.
    /// </summary>
    [Required, MaxLength(AccountInputLimits.Url)]
    public string SiteUrl { get; set; } = null!;
    /// <summary>Ignored: the subject names <see cref="AccountEmailOptions.SiteName"/>, which the application sets.</summary>
    [Obsolete("Ignored: the reset mail's subject names AccountEmailOptions.SiteName, which the application sets.")]
    [MaxLength(AccountInputLimits.UserName)]
    public string? SiteName { get; set; }
}