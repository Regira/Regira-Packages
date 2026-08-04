namespace Web.Security.Testing.Infrastructure.Jwt;

public class TokenResult
{
    public bool IsAuthenticated { get; set; }
    public string? Token { get; set; }
}