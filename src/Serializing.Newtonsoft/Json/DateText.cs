namespace Regira.Serializing.Newtonsoft.Json;

internal static class DateText
{
    /// <summary>
    /// Whether <paramref name="text"/> starts as an ISO 8601 date (<c>yyyy-MM-dd</c>). ISO text reads in the invariant
    /// culture; any other date text reads in the current culture, as it always has.
    /// </summary>
    public static bool IsIso(string text)
        => text.Length >= 10
           && char.IsAsciiDigit(text[0]) && char.IsAsciiDigit(text[1]) && char.IsAsciiDigit(text[2]) && char.IsAsciiDigit(text[3])
           && text[4] == '-' && text[7] == '-';
}
