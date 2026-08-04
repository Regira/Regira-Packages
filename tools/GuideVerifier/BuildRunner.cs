using System.Diagnostics;
using System.Text.RegularExpressions;

namespace Regira.GuideVerifier;

/// <summary>Runs <c>dotnet build</c> on the generated project and parses the compiler diagnostics.</summary>
public static class BuildRunner
{
    // e.g.  C:\tmp\gen\create-attachment_3.cs(12,9): error CS0103: The name 'x' does not exist ...
    // Capture the file (basename without extension = snippet Id), and the "error CS...: message" tail.
    private static readonly Regex ErrorRegex = new(
        @"^(?<path>.+?\.cs)\((?<line>\d+),(?<col>\d+)\):\s*error\s+(?<code>CS\d+):\s*(?<message>.+?)(?:\s*\[[^\]]*\])?$",
        RegexOptions.Compiled);

    public record BuildError(string SnippetId, string Code, string Message);

    public record BuildOutcome(int ExitCode, IReadOnlyList<BuildError> Errors, string RawOutput);

    public static BuildOutcome Build(string csprojPath)
    {
        var psi = new ProcessStartInfo("dotnet", $"build \"{csprojPath}\" -c Debug --nologo -clp:NoSummary")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet build.");
        // Read both streams concurrently before waiting: reading one to completion before the other can
        // deadlock if the child fills the other pipe's buffer (a build that floods stderr).
        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        var raw = stdoutTask.GetAwaiter().GetResult() + "\n" + stderrTask.GetAwaiter().GetResult();
        var errors = new List<BuildError>();
        var seen = new HashSet<string>();
        foreach (var line in raw.Replace("\r\n", "\n").Split('\n'))
        {
            var m = ErrorRegex.Match(line.Trim());
            if (!m.Success) continue;
            var id = Path.GetFileNameWithoutExtension(m.Groups["path"].Value);
            var code = m.Groups["code"].Value;
            var message = m.Groups["message"].Value.Trim();
            // De-dupe identical (id, code, message) — MSBuild echoes some diagnostics twice.
            if (seen.Add($"{id}|{code}|{message}"))
                errors.Add(new BuildError(id, code, message));
        }

        return new BuildOutcome(process.ExitCode, errors, raw);
    }
}
