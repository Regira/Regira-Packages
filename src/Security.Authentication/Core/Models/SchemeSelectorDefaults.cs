namespace Regira.Security.Authentication.Core.Models;

public static class SchemeSelectorDefaults
{
    /// <summary>Name of the policy scheme that forwards to whichever scheme matches the request.</summary>
    public const string AuthenticationScheme = "Smart";

    public const string DisplayName = "Selects the authentication scheme from the request";

    /// <summary>Order of the built-in rules, spaced so a custom rule can be slotted between them.</summary>
    public const int BearerOrder = 20;

    public const int BasicOrder = 30;
    public const int ApiKeyOrder = 50;
    public const int CookieOrder = 60;
}
