namespace Regira.Security.Authentication.ApiKey.Models;

public class ApiKeyOwner
{
    public record Claim(string Type, string Value);

    public string OwnerId { get; set; } = null!;
    public string Key { get; set; } = null!;

    public ICollection<string> Roles { get; set; } = new List<string>();
    public ICollection<Claim> Claims { get; set; } = new List<Claim>();
}