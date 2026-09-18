namespace Regira.GuideVerifier;

/// <summary>
/// Pulls fenced <c>```csharp</c> blocks out of a markdown guide. A block preceded by an
/// <c>&lt;!-- no-compile --&gt;</c> comment line is skipped — that is the escape hatch for genuinely partial
/// fragments that can't stand on their own. The marker sits on its own line above the fence rather than in
/// the fence's info string, because Kramdown (Jekyll/GitHub Pages) only accepts a single-token info string:
/// <c>```csharp no-compile</c> is not a fence to it, so the marker rendered as literal text and the
/// mis-paired fences swallowed following prose and headings into code blocks. Each returned snippet
/// remembers its nearest preceding heading so a compiler failure can be reported as <c>file.md § heading</c>.
/// </summary>
public static class SnippetExtractor
{
    public static IEnumerable<Snippet> Extract(string relativeFile, string content)
    {
        var lines = content.Replace("\r\n", "\n").Split('\n');
        var heading = "(intro)";
        var pendingNoCompile = false;
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

            // `<!-- no-compile -->` on its own line marks the NEXT fenced block as a partial fragment.
            if (IsNoCompileMarker(trimmed))
            {
                pendingNoCompile = true;
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
                var noCompile = pendingNoCompile;
                pendingNoCompile = false;

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

    /// <summary>
    /// A standalone <c>&lt;!-- no-compile --&gt;</c> line. Tolerates surrounding whitespace so the marker can be
    /// indented with the block it precedes.
    /// </summary>
    private static bool IsNoCompileMarker(string line)
    {
        var t = line.Trim();
        if (!t.StartsWith("<!--", StringComparison.Ordinal) || !t.EndsWith("-->", StringComparison.Ordinal))
            return false;
        return t[4..^3].Trim().Equals("no-compile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHeading(string line)
    {
        var count = 0;
        while (count < line.Length && line[count] == '#') count++;
        return count >= 1 && count < line.Length && line[count] == ' ';
    }
}
