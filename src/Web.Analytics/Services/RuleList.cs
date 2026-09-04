namespace Regira.Web.Analytics.Services;

/// <summary>
/// Shared handling for the hand-edited rule lists behind Analytics:BotDetection. Normalising once, when
/// configuration is (re)read, is what lets the matcher compare Ordinal on every request; entries typed
/// in any case still work, and a duplicated line costs nothing.
/// </summary>
internal static class RuleList
{
    public static string[] Normalize(string[]? values) => values == null
        ? []
        : values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
}