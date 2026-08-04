namespace Regira.Security.Authentication.ApiKey.Models;

public static class ApiKeyDefaults
{
    public static string AuthenticationScheme { get; set; } = "ApiKey";
    public static string HeaderName { get; set; } = "X-Api-Key";
}