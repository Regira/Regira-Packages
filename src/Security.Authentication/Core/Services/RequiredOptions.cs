namespace Regira.Security.Authentication.Core.Services;

internal static class RequiredOptions
{
    /// <summary>Refuses a registration without a value it cannot do without, naming the option and where its value comes from.</summary>
    public static void Require(string? value, string option, string? source = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(source == null ? $"{option} is required." : $"{option} is required — it comes from {source}.");
        }
    }
}
