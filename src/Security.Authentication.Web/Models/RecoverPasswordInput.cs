using System.ComponentModel.DataAnnotations;

namespace Regira.Security.Authentication.Web.Models;

public record RecoverPasswordInput
{
    [Required]
    public string Username { get; set; } = null!;
    [Required]
    public string SiteUrl { get; set; } = null!;
    public string? SiteName { get; set; }
}