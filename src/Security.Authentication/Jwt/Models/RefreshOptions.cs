namespace Regira.Security.Authentication.Jwt.Models;

public record RefreshOptions
{
    public string ClientId { get; set; } = null!;
    public string ClientSecret { get; set; } = null!;
    public string AuthenticationScheme { get; set; } = null!;
}