namespace Regira.GuideVerifier;

/// <summary>
/// Pulls fenced <c>```csharp</c> blocks out of a markdown guide. A block fenced <c>```csharp no-compile</c>
/// (any info string after <c>csharp</c> that includes the token <c>no-compile</c>) is skipped — that is the
/// escape hatch for genuinely partial fragments that can't stand on their own. Each returned snippet
/// remembers its nearest preceding heading so a compiler failure can be reported as <c>file.md § heading</c>.
/// </summary>
public static class SnippetExtractor
{
    public static IEnumerable<Snippet> Extract(string relativeFile, string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var heading = "(intro)";
        var i = 0;
        while (i < lines.Length)
        {
            var line = lines[i];
            var trimmed = line.TrimStart();

            // Track the nearest heading (outside fences; the loop only sees this line when not in a fence).
            if (IsHeading(trimmed))
            {
                heading = trimmed.TrimStart('#').Trim();
                i++;
                continue;
            }

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var info = trimmed[3..].Trim();
                var isCsharp = info.Equals("csharp", StringComparison.OrdinalIgnoreCase) ||
                               info.StartsWith("csharp ", StringComparison.OrdinalIgnoreCase) ||
                               info.Equals("cs", StringComparison.OrdinalIgnoreCase) ||
                               info.StartsWith("cs ", StringComparison.OrdinalIgnoreCase);
                var noCompile = info.Contains("no-compile", StringComparison.OrdinalIgnoreCase);

                var fenceLine = i + 1;
                var body = new List<string>();
                i++;
                while (i < lines.Length && !lines[i].TrimStart().StartsWith("```", StringComparison.Ordinal))
                    body.Add(lines[i++]);
                // Skip the closing fence, if present.
                if (i < lines.Length) i++;

                if (isCsharp && !noCompile)
                {
                    var code = string.Join("\n", body).Trim();
                    if (code.Length > 0)
                        yield return new Snippet(relativeFile, heading, fenceLine, code, SnippetKind.Statements);
                }
                continue;
            }

            i++;
        }
    }

    private static bool IsHeading(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == '#') count++;
        return count >= 1 && count < line.Length && line[count] == ' ';
    }
}
