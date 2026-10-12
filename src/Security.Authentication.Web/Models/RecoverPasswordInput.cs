using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record RecoverPasswordInput
{
    [Required]
    public string Username { get; set; } = null!;
    /// <summary>
    /// The page the reset link opens, e.g. <c>https://app.example.com/reset-password</c>; the token becomes its query
    /// string. It must be on one of <see cref="AccountEmailOptions.AllowedOrigins"/>, or the request answers <c>400</c>.
    /// </summary>
    [Required]
    public string SiteUrl { get; set; } = null!;
    /// <summary>Ignored: the subject names <see cref="AccountEmailOptions.SiteName"/>, which the application sets.</summary>
    [Obsolete("Ignored: the reset mail's subject names AccountEmailOptions.SiteName, which the application sets.")]
    public string? SiteName { get; set; }
}