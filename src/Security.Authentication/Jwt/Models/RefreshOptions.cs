namespace Regira.Security.Authentication.Jwt.Models;

[Obsolete("Only the obsolete AccessTokenHelper reads it. Will be removed in v7.")]
public record RefreshOptions
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string AuthenticationScheme { get; set; } = null!;
}