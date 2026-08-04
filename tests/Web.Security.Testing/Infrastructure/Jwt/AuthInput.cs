namespace Web.Security.Testing.Infrastructure.Jwt;

public class AuthInput
{
    public string? Username { get; set; }
    public string? Password { get; set; } = "AcceptAnyPassword";
}