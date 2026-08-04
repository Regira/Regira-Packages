namespace Regira.Security.Authentication.Web.Models;

public record UserTokenModel
{
    public string Token { get; set; } = null!;
    public string Username { get; set; } = null!;
}