namespace Regira.DAL.MongoDB.Services;

internal static class ToolOutput
{
    private const int MaxLength = 4000;

    /// <summary>
    /// The end of what a tool wrote, where its diagnosis is: a progress log can run to megabytes, which no exception
    /// message should carry whole.
    /// </summary>
    public static string Tail(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        return trimmed.Length <= MaxLength ? trimmed : "…" + trimmed[^MaxLength..];
    }
}
